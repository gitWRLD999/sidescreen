using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("SideScreen")]
[assembly: AssemblyProduct("SideScreen")]
[assembly: AssemblyDescription("On-demand virtual display tray control")]
[assembly: AssemblyVersion("0.4.0.0")]

internal static class Program {
    internal static void Log(string message) {
        try {
            var path=Path.Combine(AppPaths.State,"lifecycle.log");
            if(File.Exists(path)&&new FileInfo(path).Length>262144) File.WriteAllText(path,"");
            File.AppendAllText(path,DateTime.Now.ToString("o")+" "+message+Environment.NewLine);
        } catch { }
    }
    [STAThread] static void Main(string[] args) {
        bool first;
        using(var mutex = new Mutex(true, "Local\\SideScreen.Tray", out first)) {
            using(var openSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\SideScreen.Tray.Open")) {
                if(!first) { openSignal.Set(); return; }
                Log("Started version 0.4.0; process "+Process.GetCurrentProcess().Id);
                AppDomain.CurrentDomain.UnhandledException+=delegate(object sender,UnhandledExceptionEventArgs e) { Log("Unhandled exception: "+e.ExceptionObject); };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { using(var app = new TrayApp(openSignal,args.Contains("--show"))) { Application.Run(app); } }
                finally { Log("Application exited."); }
            }
        }
    }
}

internal sealed class TrayApp : ApplicationContext {
    readonly NotifyIcon tray;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem statusItem, onItem, offItem, quitItem, viewItem, rescueItem;
    readonly System.Windows.Forms.Timer refresh;
    readonly Form panel;
    readonly Label heading, details;
    readonly Button toggle, viewButton, rescueButton;
    VirtualScreenViewer viewer;
    readonly Icon offIcon, onIcon, busyIcon;
    readonly string scriptPath, logPath;
    bool enabled, busy, known, exiting, mainAvailable;
    string lastDiagnostic;

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [StructLayout(LayoutKind.Sequential)] struct TrayIdentifier { public uint Size; public IntPtr Window; public uint Id; public Guid Guid; }
    [StructLayout(LayoutKind.Sequential)] struct TrayRect { public int Left,Top,Right,Bottom; }
    [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref TrayIdentifier icon,out TrayRect rect);
    static Icon MakeIcon(Color color, bool active) {
        using(var bmp=new Bitmap(32,32)) {
            using(var g=Graphics.FromImage(bmp)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using(var p=new Pen(Color.FromArgb(245,245,245),2.5f)) {
                    g.DrawRectangle(p,3,4,25,18); g.DrawLine(p,15,23,15,27); g.DrawLine(p,9,28,22,28);
                }
                using(var b=new SolidBrush(color)) { g.FillRectangle(b,6,7,19,12); g.FillEllipse(b,21,20,10,10); }
                using(var p=new Pen(Color.White,1.7f)) {
                    if(active) { g.DrawLine(p,23,25,25,27); g.DrawLine(p,25,27,29,23); }
                    else { g.DrawLine(p,24,25,28,25); }
                }
            }
            var handle=bmp.GetHicon();
            try { return (Icon)Icon.FromHandle(handle).Clone(); }
            finally { DestroyIcon(handle); }
        }
    }

    public TrayApp(EventWaitHandle openSignal,bool showPanel) {
        scriptPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"display.ps1");
        logPath=Path.Combine(AppPaths.State,"last-operation.log");
        offIcon=MakeIcon(Color.FromArgb(105,119,139),false);
        onIcon=MakeIcon(Color.FromArgb(20,180,115),true);
        busyIcon=MakeIcon(Color.FromArgb(235,162,35),false);

        panel=new Form { Text="SideScreen", ClientSize=new Size(560,310), FormBorderStyle=FormBorderStyle.FixedDialog, MaximizeBox=false, MinimizeBox=false, StartPosition=FormStartPosition.CenterScreen, BackColor=Color.FromArgb(248,250,252), Font=new Font("Segoe UI",10), Icon=offIcon };
        heading=new Label { Location=new Point(24,22), Size=new Size(512,34), Font=new Font("Segoe UI",17,FontStyle.Bold) };
        details=new Label { Location=new Point(24,66), Size=new Size(512,62), ForeColor=Color.FromArgb(65,75,90) };
        toggle=new Button { Location=new Point(24,144), Size=new Size(140,42), FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(27,91,175), ForeColor=Color.White, AccessibleName="Turn virtual display on" };
        viewButton=new Button { Text="View screen",Location=new Point(180,144),Size=new Size(170,42),AccessibleName="View virtual screen" };
        rescueButton=new Button { Text="Bring windows back",Location=new Point(366,144),Size=new Size(170,42),AccessibleName="Bring virtual display windows back" };
        var hint=new Label { Text="View screen shows a live preview on your main.\r\nSelect a window there to bring it back individually.",Location=new Point(24,204),Size=new Size(512,48),ForeColor=Color.FromArgb(65,75,90) };
        var hide=new Button { Text="Close", Location=new Point(440,264), Size=new Size(96,34) };
        toggle.Click += async delegate { await Change(!enabled); };
        viewButton.Click += async delegate { await OpenViewer(); };
        rescueButton.Click += delegate { RescueWindows(); };
        hide.Click += delegate { panel.Hide(); };
        panel.Controls.AddRange(new Control[]{heading,details,toggle,viewButton,rescueButton,hint,hide});
        panel.FormClosing += delegate(object sender,FormClosingEventArgs e) { if(!exiting) { e.Cancel=true; panel.Hide(); } };

        menu=new ContextMenuStrip();
        statusItem=new ToolStripMenuItem("Checking virtual display...") { Enabled=false };
        onItem=new ToolStripMenuItem("Turn virtual display on",null,async delegate { await Change(true); });
        offItem=new ToolStripMenuItem("Turn virtual display off",null,async delegate { await Change(false); });
        viewItem=new ToolStripMenuItem("View virtual screen",null,async delegate { await OpenViewer(); });
        rescueItem=new ToolStripMenuItem("Bring windows back to main",null,delegate { RescueWindows(); });
        quitItem=new ToolStripMenuItem("Exit tray control",null,delegate { Program.Log("Exit requested from tray menu."); ExitThread(); });
        menu.Items.Add(statusItem); menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(viewItem); menu.Items.Add(rescueItem); menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(onItem); menu.Items.Add(offItem);
        menu.Items.Add(new ToolStripMenuItem("Open controls",null,delegate { OpenPanel(); }));
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add(quitItem);
        menu.Opening += delegate { RefreshState(); };
        tray=new NotifyIcon { Icon=offIcon, Text="Virtual display: checking", ContextMenuStrip=menu, Visible=true };
        tray.MouseClick += delegate(object sender,MouseEventArgs e) { if(e.Button==MouseButtons.Left) OpenPanel(); };
        refresh=new System.Windows.Forms.Timer { Interval=3000 };
        refresh.Tick += delegate { if(!busy) RefreshState(); if(openSignal.WaitOne(0)) { OpenPanel(); } };
        RefreshState(); refresh.Start();
        if(showPanel) OpenPanel();
    }

    void OpenPanel() {
        RefreshState();
        try { VirtualScreenWindows.ShowOnMain(panel,false); }
        catch { panel.Show(); ShowWindow(panel.Handle,5); panel.Activate(); }
    }
    async Task OpenViewer() {
        if(busy)return;
        try {
            VirtualScreenWindows.MainWorkArea();
            RefreshState();
            if(!enabled && !await Change(true))return;
            if(viewer==null||viewer.IsDisposed) viewer=new VirtualScreenViewer(onIcon);
            VirtualScreenWindows.ShowOnMain(viewer,true);
        } catch(Exception ex) { MessageBox.Show(ex.Message,"SideScreen",MessageBoxButtons.OK,MessageBoxIcon.Information); }
    }
    void RescueWindows() {
        if(busy)return;
        try {
            int count=VirtualScreenWindows.BringAllBack();
            tray.ShowBalloonTip(2500,"SideScreen",count==0?"No windows are on the virtual display.":count+" window(s) brought back to the main.",ToolTipIcon.Info);
        } catch(Exception ex) { MessageBox.Show(ex.Message,"Bring windows back",MessageBoxButtons.OK,MessageBoxIcon.Information); }
    }

    void RefreshState() {
        if(busy) return;
        try {
            var paths=DisplayAudit.Read();
            if(paths.Count(p=>p.IsVirtualMonitor)>1) throw new InvalidOperationException("Multiple virtual displays are active.");
            enabled=paths.Count(p=>p.IsVirtualMonitor)==1;
            mainAvailable=paths.Any(p=>p.IsUsableMainDisplay);
            known=true;
        } catch { known=false; }
        DrawState();
    }
    void DrawState() {
        var state=busy?"Switching...":!known?"Unavailable":enabled?"On":"Off";
        tray.Icon=busy?busyIcon:enabled?onIcon:offIcon;
        panel.Icon=tray.Icon;
        tray.Text="Virtual display: "+state+" - click for controls";
        statusItem.Text="Virtual display: "+state;
        heading.Text="Virtual display is "+state.ToLowerInvariant();
        details.Text=busy?"Applying your display change. This may take a few seconds.":!known?"Windows could not read the displays. Try again after unlocking your desktop.":enabled?(mainAvailable?"A separate display for agent windows.\r\nYou can preview the virtual screen or bring windows back.":"Only the virtual screen is active.\r\nEnable a physical screen in Windows Display Settings to preview or turn this off."):"The virtual monitor is disconnected.\r\nEnable an installed MTT virtual display to place agent windows there.";
        toggle.Text=busy?"Switching...":enabled?"Turn off":"Turn on";
        toggle.AccessibleName=enabled?"Turn virtual display off":"Turn virtual display on";
        toggle.Enabled=!busy&&known&&(!enabled||mainAvailable);
        onItem.Enabled=!busy&&known&&!enabled;
        offItem.Enabled=!busy&&known&&enabled&&mainAvailable;
        viewItem.Enabled=viewButton.Enabled=!busy&&known&&mainAvailable;
        rescueItem.Enabled=rescueButton.Enabled=!busy&&known&&enabled&&mainAvailable;
        quitItem.Enabled=!busy;
        try {
            // Read our own notification icon geometry to verify installation.
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var native=(NativeWindow)typeof(NotifyIcon).GetField("window",flags).GetValue(tray);
            var id=(int)typeof(NotifyIcon).GetField("id",flags).GetValue(tray);
            var ident=new TrayIdentifier { Size=(uint)Marshal.SizeOf(typeof(TrayIdentifier)),Window=native.Handle,Id=(uint)id,Guid=Guid.Empty };
            TrayRect rect; int result=Shell_NotifyIconGetRect(ref ident,out rect);
            WriteDiagnostic("Version=0.3.0\r\nState="+state+"\r\nMainAvailable="+mainAvailable+"\r\nIconVisible="+tray.Visible+"\r\nTooltip="+tray.Text+"\r\nRectResult="+result+"\r\nRect="+rect.Left+","+rect.Top+","+rect.Right+","+rect.Bottom+"\r\nPanelVisible="+(panel.IsHandleCreated&&IsWindowVisible(panel.Handle)));
        } catch(Exception ex) {
            WriteDiagnostic("State="+state+"\r\nDiagnosticError="+ex.Message);
        }
    }
    void WriteDiagnostic(string value) {
        if(value==lastDiagnostic) return;
        try { File.WriteAllText(Path.Combine(AppPaths.State,"tray-status.txt"),"Time="+DateTime.Now.ToString("o")+"\r\n"+value); lastDiagnostic=value; }
        catch { /* A diagnostic file must not interrupt a display operation. */ }
    }
    async Task<bool> Change(bool desired) {
        if(busy) return false;
        RefreshState();
        if(!known) return false;
        // A second click cannot launch a competing device operation.
        if(desired==enabled) return true;
        if(!desired&&!mainAvailable) {
            MessageBox.Show("Enable a physical screen in Windows Display Settings first. The only active display will stay on.","SideScreen",MessageBoxButtons.OK,MessageBoxIcon.Information);
            return false;
        }
        busy=true; DrawState();
        string error=null;
        try {
            if(!File.Exists(scriptPath)) throw new FileNotFoundException("The installed virtual-display control script is missing.",scriptPath);
            var mode=desired?"On":"Off";
            int exitCode=await Task.Run(()=>{
                var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe"),"-NoProfile -NonInteractive -File \""+scriptPath+"\" -Mode "+mode) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true };
                using(var p=Process.Start(start)) {
                    var output=p.StandardOutput.ReadToEndAsync();
                    var errors=p.StandardError.ReadToEndAsync();
                    p.WaitForExit();
                    File.WriteAllText(logPath,DateTime.Now.ToString("o")+"\r\nRequested: "+mode+"\r\nExit: "+p.ExitCode+"\r\n"+output.Result+errors.Result);
                    if(p.ExitCode!=0) throw new Exception(string.IsNullOrWhiteSpace(errors.Result)?"Windows could not complete the display change.":errors.Result.Trim());
                    return p.ExitCode;
                }
            });
        } catch(Exception ex) { error=ex.Message; }
        busy=false; RefreshState();
        if(error==null && (!known||enabled!=desired)) error="Windows did not confirm the requested display state. Please try again.";
        if(error!=null) {
            MessageBox.Show(error+"\r\n\r\nAn active physical display is required before turning off the virtual display.","SideScreen",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        } else {
            tray.ShowBalloonTip(2500,"SideScreen",enabled?"On - ready for remote access.":"Off - virtual monitor disconnected.",ToolTipIcon.Info);
        }
        return error==null;
    }
    protected override void ExitThreadCore() {
        exiting=true; refresh.Stop(); tray.Visible=false; if(viewer!=null&&!viewer.IsDisposed)viewer.Close(); panel.Close(); base.ExitThreadCore();
    }
    protected override void Dispose(bool disposing) {
        if(disposing) { refresh.Dispose(); tray.Dispose(); menu.Dispose(); panel.Dispose(); offIcon.Dispose(); onIcon.Dispose(); busyIcon.Dispose(); }
        base.Dispose(disposing);
    }
}
