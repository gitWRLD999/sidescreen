using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.IO;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal static class VirtualScreenWindows {
    [StructLayout(LayoutKind.Sequential)] internal struct Rect {
        public int Left,Top,Right,Bottom;
        public Rectangle Rectangle { get { return Rectangle.FromLTRB(Left,Top,Right,Bottom); } }
    }
    [StructLayout(LayoutKind.Sequential)] struct Point2 { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] struct Placement { public int Length,Flags,ShowCommand; public Point2 Min,Max; public Rect Normal; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
    delegate bool EnumCallback(IntPtr window,IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback,IntPtr data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder text,int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr window,ref Placement placement);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point2 point,uint flags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window,int command);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);

    internal sealed class PhysicalPixels : IDisposable {
        readonly IntPtr previous;
        public PhysicalPixels() { previous=SetThreadDpiAwarenessContext(new IntPtr(-4)); }
        public void Dispose() { if(previous!=IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
    }
    internal sealed class WindowEntry {
        public IntPtr Handle;
        public string Title,Application;
        public Rectangle Bounds;
    }
    internal static Rectangle? VirtualBounds() {
        var candidates=DisplayAudit.Read().Where(p=>p.IsVirtualMonitor).ToArray();
        if(candidates.Length>1) throw new InvalidOperationException("Multiple MTT virtual displays are active. This version requires exactly one.");
        var path=candidates.FirstOrDefault();
        if(path==null) return null;
        return new Rectangle(path.X,path.Y,(int)path.Width,(int)path.Height);
    }
    internal static Rectangle MainWorkArea() {
        using(new PhysicalPixels()) {
            var paths=DisplayAudit.Read().Where(p=>p.IsUsableMainDisplay).ToArray();
            var primary=Screen.PrimaryScreen;
            var path=paths.FirstOrDefault(p=>primary!=null && p.GdiName==primary.DeviceName) ?? paths.FirstOrDefault();
            if(path==null) throw new InvalidOperationException("No physical display is active. Enable one in Windows Display Settings before previewing or recovering windows.");
            var info=new MonitorInfo { Size=Marshal.SizeOf(typeof(MonitorInfo)) };
            var monitor=MonitorFromPoint(new Point2 { X=path.X+(int)path.Width/2,Y=path.Y+(int)path.Height/2 },2);
            if(!GetMonitorInfo(monitor,ref info)) throw new Win32Exception();
            return info.Work.Rectangle;
        }
    }
    internal static Rectangle BoundsOf(IntPtr window,bool normal) {
        using(new PhysicalPixels()) {
            if(normal) {
                var p=new Placement { Length=Marshal.SizeOf(typeof(Placement)) };
                if(GetWindowPlacement(window,ref p)) {
                    var r=p.Normal.Rectangle;
                    var info=new MonitorInfo { Size=Marshal.SizeOf(typeof(MonitorInfo)) };
                    if(GetMonitorInfo(MonitorFromWindow(window,2),ref info)) r.Offset(info.Work.Left-info.Monitor.Left,info.Work.Top-info.Monitor.Top);
                    return r;
                }
            }
            Rect raw;
            if(!GetWindowRect(window,out raw)) return Rectangle.Empty;
            return raw.Rectangle;
        }
    }
    static bool BelongsToVirtual(Rectangle bounds,Rectangle virtualArea) {
        if(bounds.Width<=0||bounds.Height<=0) return false;
        var overlap=Rectangle.Intersect(bounds,virtualArea);
        return virtualArea.Contains(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2)
            || ((long)overlap.Width*overlap.Height>(long)bounds.Width*bounds.Height/2);
    }
    internal static List<WindowEntry> List() {
        var result=new List<WindowEntry>();
        var area=VirtualBounds();
        if(area==null) return result;
        using(new PhysicalPixels()) {
            EnumWindows(delegate(IntPtr h,IntPtr unused) {
                if(!IsWindowVisible(h)) return true;
                // Passive overlays and tool palettes are not stranded app windows.
                long style=GetWindowLongPtr(h,-20).ToInt64();
                if((style&0x08000080L)!=0) return true;
                uint pid; GetWindowThreadProcessId(h,out pid);
                if(pid==(uint)Process.GetCurrentProcess().Id) return true;
                var title=new StringBuilder(1024); GetWindowText(h,title,title.Capacity);
                if(title.Length==0) return true;
                var name=new StringBuilder(128); GetClassName(h,name,name.Capacity);
                if(name.ToString()=="Progman"||name.ToString()=="WorkerW"||name.ToString()=="Shell_TrayWnd") return true;
                int cloaked; if(DwmGetWindowAttribute(h,14,out cloaked,4)==0&&cloaked!=0) return true;
                var bounds=BoundsOf(h,IsIconic(h));
                if(!BelongsToVirtual(bounds,area.Value)) return true;
                string application="";
                try { using(var p=Process.GetProcessById((int)pid)) application=p.ProcessName; } catch {}
                result.Add(new WindowEntry { Handle=h,Title=title.ToString(),Application=application,Bounds=bounds });
                return true;
            },IntPtr.Zero);
        }
        return result.OrderBy(w=>w.Title,StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    internal static void BringBack(WindowEntry entry,int index) {
        var area=VirtualBounds();
        if(!IsWindow(entry.Handle)) throw new InvalidOperationException("That window has already closed.");
        if(area==null||!BelongsToVirtual(BoundsOf(entry.Handle,IsIconic(entry.Handle)),area.Value)) return;
        var work=MainWorkArea();
        var maximized=IsZoomed(entry.Handle);
        var minimized=IsIconic(entry.Handle);
        var normal=BoundsOf(entry.Handle,maximized||minimized);
        int width=Math.Min(Math.Max(normal.Width,1),work.Width-48);
        int height=Math.Min(Math.Max(normal.Height,1),work.Height-48);
        int x=work.Left+Math.Min(24+(index%6)*28,work.Width-width);
        int y=work.Top+Math.Min(24+(index%6)*28,work.Height-height);
        using(new PhysicalPixels()) {
            if(maximized||minimized) throw new InvalidOperationException("Restore this window before moving it without activation.");
            if(!SetWindowPos(entry.Handle,IntPtr.Zero,x,y,width,height,0x4014)) throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows could not move "+entry.Title);

        }
    }
    internal static int BringAllBack() {
        var windows=List(); var failed=new List<string>(); int count=0;
        foreach(var window in windows) {
            try { BringBack(window,count); count++; } catch(Exception ex) { failed.Add(ex.Message); }
        }
        if(failed.Count>0) throw new InvalidOperationException(count+" windows moved.\r\n"+string.Join("\r\n",failed));
        return count;
    }
    internal static void ShowOnMain(Form form,bool viewer) {
        // The first show consumes any hidden startup-window flag; then show the UI.
        if(form.WindowState!=FormWindowState.Normal) form.WindowState=FormWindowState.Normal;
        form.Show(); ShowWindow(form.Handle,5);
        using(new PhysicalPixels()) {
            var area=MainWorkArea(); var bounds=BoundsOf(form.Handle,false);
            int width=viewer?Math.Min(1320,area.Width-64):Math.Min(bounds.Width,area.Width-32);
            int height=viewer?Math.Min(1000,area.Height-64):Math.Min(bounds.Height,area.Height-32);
            SetWindowPos(form.Handle,IntPtr.Zero,area.Left+(area.Width-width)/2,area.Top+(area.Height-height)/2,width,height,0x0014);
        }
        form.Activate();
    }
}

internal sealed class VirtualScreenViewer : Form {
    [StructLayout(LayoutKind.Sequential)] struct CursorInfo {public int Size,Flags;public IntPtr Cursor;public Point Position;}
    [StructLayout(LayoutKind.Sequential)] struct IconInfo {public bool IsIcon;public int XHotspot,YHotspot;public IntPtr Mask,Color;}
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] static extern IntPtr CopyIcon(IntPtr icon);
    [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr icon,out IconInfo info);
    [DllImport("user32.dll")] static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr icon,int width,int height,int step,IntPtr brush,int flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll",SetLastError=true)] static extern bool BitBlt(IntPtr destination,int x,int y,int width,int height,IntPtr source,int sourceX,int sourceY,uint operation);
    static void CaptureScreen(Graphics graphics,Rectangle area) {
        IntPtr source=GetDC(IntPtr.Zero);if(source==IntPtr.Zero)throw new Win32Exception("Desktop capture unavailable");
        try {
            IntPtr destination=graphics.GetHdc();
            try {if(!BitBlt(destination,0,0,area.Width,area.Height,source,area.X,area.Y,0x40CC0020))throw new Win32Exception(Marshal.GetLastWin32Error());}
            finally {graphics.ReleaseHdc(destination);}
        }finally {ReleaseDC(IntPtr.Zero,source);}
    }
    static void DrawHumanCursor(Graphics graphics,Rectangle area) {
        var cursor=new CursorInfo {Size=Marshal.SizeOf(typeof(CursorInfo))};
        if(!GetCursorInfo(ref cursor) || (cursor.Flags&1)==0 || !area.Contains(cursor.Position))return;
        IntPtr copy=CopyIcon(cursor.Cursor);if(copy==IntPtr.Zero)return;
        var icon=new IconInfo();
        try {
            if(!GetIconInfo(copy,out icon))return;
            IntPtr dc=graphics.GetHdc();
            try{DrawIconEx(dc,cursor.Position.X-area.X-icon.XHotspot,cursor.Position.Y-area.Y-icon.YHotspot,copy,0,0,0,IntPtr.Zero,3);}finally{graphics.ReleaseHdc(dc);}
        }finally {if(icon.Mask!=IntPtr.Zero)DeleteObject(icon.Mask);if(icon.Color!=IntPtr.Zero)DeleteObject(icon.Color);DestroyIcon(copy);}
    }
    sealed class AgentPointer {public int x{get;set;}public int y{get;set;}public long windowHandle{get;set;}public string displayId{get;set;}public string expiresUtc{get;set;}}
    static void DrawAgentCursor(Graphics graphics,Rectangle area) {
        try {
            string file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AgentTools","SideScreen","state","agent-cursor.json");
            if(!File.Exists(file))return;
            var p=new JavaScriptSerializer().Deserialize<AgentPointer>(File.ReadAllText(file));
            var display=DisplayAudit.Read().SingleOrDefault(d=>d.IsVirtualMonitor);
            if(p==null||display==null||p.displayId!=SideScreen.Layout.Id(display)||DateTime.Parse(p.expiresUtc).ToUniversalTime()<DateTime.UtcNow||!area.Contains(p.x,p.y))return;
            var window=SideScreen.Windows.List().SingleOrDefault(w=>w.Handle==p.windowHandle);
            if(window==null||!window.Bounds.Contains(p.x,p.y)||!area.Contains(window.Bounds))return;
            int x=p.x-area.X,y=p.y-area.Y;
            using(var pen=new Pen(Color.DeepSkyBlue,3)) {
                graphics.DrawEllipse(pen,x-7,y-7,14,14);
                graphics.DrawLine(pen,x-12,y,x+12,y);graphics.DrawLine(pen,x,y-12,x,y+12);
            }
            using(var font=new Font("Segoe UI",9,FontStyle.Bold))graphics.DrawString("Agent",font,Brushes.DeepSkyBlue,x+12,y+8);
        }catch { /* Expired/partially written markers must not interrupt preview. */ }
    }
    readonly PreviewSurface surface;
    readonly ListView windows;
    readonly Label status;
    readonly Button selected,all;
    readonly System.Windows.Forms.Timer timer;
    Bitmap frame;
    string listFingerprint="";
    int ticks;

    public VirtualScreenViewer(Icon icon) {
        Text="SideScreen - Live view"; Icon=icon; Font=new Font("Segoe UI",10);
        BackColor=Color.FromArgb(246,248,251); ClientSize=new Size(860,650); MinimumSize=new Size(580,440);
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,142));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
        status=new Label { Dock=DockStyle.Fill,Text="Loading the virtual screen...",AutoEllipsis=true };
        surface=new PreviewSurface { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,10) };
        windows=new ListView { Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,AccessibleName="Windows on virtual display" };
        windows.Columns.Add("Window on virtual display",540); windows.Columns.Add("Application",180);
        windows.SelectedIndexChanged+=delegate { selected.Enabled=windows.SelectedItems.Count>0; };
        windows.DoubleClick+=delegate { RescueSelected(); };
        var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Padding=new Padding(0,8,0,0) };
        selected=new Button { Text="Bring selected to main",AutoSize=true,Enabled=false,AccessibleName="Bring selected window to main" };
        all=new Button { Text="Bring all to main",AutoSize=true,AccessibleName="Bring all windows to main" };
        var close=new Button { Text="Close view",AutoSize=true };
        selected.Click+=delegate { RescueSelected(); };
        all.Click+=delegate { RescueAll(); };
        close.Click+=delegate { Close(); };
        buttons.Controls.AddRange(new Control[]{selected,all,close});
        layout.Controls.Add(status,0,0); layout.Controls.Add(surface,0,1); layout.Controls.Add(windows,0,2); layout.Controls.Add(buttons,0,3);
        Controls.Add(layout);
        timer=new System.Windows.Forms.Timer { Interval=100 };
        timer.Tick+=delegate { RefreshPreview(); };
        Shown+=delegate { RefreshPreview(); timer.Start(); };
        Resize+=delegate { if(WindowState==FormWindowState.Minimized) timer.Stop(); else if(Visible) timer.Start(); };
        FormClosed+=delegate { timer.Stop(); timer.Dispose(); surface.Frame=null; if(frame!=null)frame.Dispose(); };
    }
    void RefreshPreview() {
        if(!Visible||WindowState==FormWindowState.Minimized) return;
        try {
            var area=VirtualScreenWindows.VirtualBounds();
            if(area==null) {
                surface.Frame=null; surface.Message="The virtual display is off."; surface.Invalidate();
                status.Text="Virtual display is off. Close this view and turn it on from the tray controls.";
                UpdateList(); return;
            }
            if(VirtualScreenWindows.BoundsOf(Handle,false).IntersectsWith(area.Value)) {
                surface.Frame=null; surface.Message="Move this viewer back to the main screen to resume."; surface.Invalidate(); return;
            }
            if(frame==null||frame.Size!=area.Value.Size) {
                surface.Frame=null; if(frame!=null)frame.Dispose(); frame=new Bitmap(area.Value.Width,area.Value.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }
            using(new VirtualScreenWindows.PhysicalPixels())
            using(var g=Graphics.FromImage(frame)) {
                CaptureScreen(g,area.Value);
                DrawHumanCursor(g,area.Value);
                DrawAgentCursor(g,area.Value);
            }
            surface.Frame=frame; surface.Message=""; surface.Invalidate();
            status.Text="Live view  ·  "+area.Value.Width+" × "+area.Value.Height+"  ·  "+DateTime.Now.ToString("HH:mm:ss.fff")+"  ·  Human cursor included";
            if(ticks++%20==0) UpdateList();
        } catch(Exception ex) { surface.Frame=null; surface.Message="Preview unavailable"; surface.Invalidate(); status.Text=ex.Message; }
    }
    void UpdateList() {
        var entries=VirtualScreenWindows.List();
        var fingerprint=string.Join("|",entries.Select(w=>w.Handle.ToInt64()+":"+w.Title));
        all.Enabled=entries.Count>0;
        if(fingerprint==listFingerprint)return;
        listFingerprint=fingerprint;
        IntPtr old=windows.SelectedItems.Count==0?IntPtr.Zero:((VirtualScreenWindows.WindowEntry)windows.SelectedItems[0].Tag).Handle;
        windows.BeginUpdate(); windows.Items.Clear();
        foreach(var entry in entries) { var item=new ListViewItem(entry.Title); item.SubItems.Add(entry.Application); item.Tag=entry; windows.Items.Add(item); if(entry.Handle==old)item.Selected=true; }
        windows.EndUpdate(); selected.Enabled=windows.SelectedItems.Count>0;
    }
    void RescueSelected() {
        if(windows.SelectedItems.Count==0)return;
        try { VirtualScreenWindows.BringBack((VirtualScreenWindows.WindowEntry)windows.SelectedItems[0].Tag,0); UpdateList(); }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"Bring window back",MessageBoxButtons.OK,MessageBoxIcon.Information); }
    }
    void RescueAll() {
        try { var count=VirtualScreenWindows.BringAllBack(); status.Text=count+" window(s) brought back to the main."; UpdateList(); }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"Bring windows back",MessageBoxButtons.OK,MessageBoxIcon.Information); }
    }
    sealed class PreviewSurface : Control {
        public Bitmap Frame; public string Message="Loading...";
        public PreviewSurface() { DoubleBuffered=true; BackColor=Color.FromArgb(20,24,32); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            if(Frame==null) { TextRenderer.DrawText(e.Graphics,Message,Font,ClientRectangle,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);return; }
            double scale=Math.Min((double)Width/Frame.Width,(double)Height/Frame.Height);
            int w=(int)(Frame.Width*scale),h=(int)(Frame.Height*scale);
            e.Graphics.InterpolationMode=InterpolationMode.HighQualityBilinear;
            e.Graphics.DrawImage(Frame,new Rectangle((Width-w)/2,(Height-h)/2,w,h));
        }
    }
}
