using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using SideScreen;
class VirtualProbe:Form {
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr value);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    readonly string output;
    readonly TextBox edit=new TextBox {AccessibleName="Virtual text",Location=new Point(25,25),Width=450};
    readonly Button button=new Button {Text="Virtual button",Location=new Point(25,70),Size=new Size(160,35)};
    readonly Canvas canvas=new Canvas {Location=new Point(25,130),Size=new Size(400,130),BackColor=Color.LightBlue};
    int clicks,keyDowns;bool controlKey,shiftKey,asyncControl,asyncShift;Point pointer;
    protected override bool ShowWithoutActivation {get{return true;}}
    sealed class Canvas:Control {
        public int Down,Up,DragMoves,Double,Wheel,Horizontal;public Point Last;
        public Canvas(){SetStyle(ControlStyles.StandardDoubleClick,true);}
        protected override void OnMouseDown(MouseEventArgs e){Down++;Capture=true;base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){Up++;Capture=false;base.OnMouseUp(e);}
        protected override void OnMouseMove(MouseEventArgs e){Last=e.Location;if(e.Button==MouseButtons.Left)DragMoves++;base.OnMouseMove(e);}
        protected override void OnMouseDoubleClick(MouseEventArgs e){Double++;base.OnMouseDoubleClick(e);}
        protected override void OnMouseWheel(MouseEventArgs e){Wheel+=e.Delta;base.OnMouseWheel(e);}
        protected override void WndProc(ref Message m){if(m.Msg==0x20e)Horizontal+=(short)((m.WParam.ToInt64()>>16)&65535);base.WndProc(ref m);}
    }
    VirtualProbe(string path) {
        output=path;var d=SideScreen.Layout.Select(DisplayAudit.Read(),true);
        Text="SideScreen virtual input fixture "+(IntPtr.Size*8);StartPosition=FormStartPosition.Manual;
        Location=new Point(d.X+700,d.Y+(IntPtr.Size==8?60:490));Size=new Size(560,380);
        Controls.AddRange(new Control[]{edit,button,canvas,new TextBox{AccessibleName="Password",UseSystemPasswordChar=true,Location=new Point(25,280)},new TextBox{AccessibleName="Read only",ReadOnly=true,Text="unchanged",Location=new Point(190,280)}});
        button.Click+=delegate{clicks++;pointer=Cursor.Position;SetForegroundWindow(Handle);edit.Focus();Cursor.Position=new Point(0,0);Save();};
        edit.TextChanged+=delegate{Save();};edit.KeyDown+=delegate(object sender,KeyEventArgs e){keyDowns++;controlKey=e.Control;shiftKey=e.Shift;asyncControl=(GetAsyncKeyState(162)&0x8000)!=0;asyncShift=(GetAsyncKeyState(160)&0x8000)!=0;Save();};
        var timer=new Timer{Interval=50};timer.Tick+=delegate{Save();};timer.Start();Shown+=delegate{Save();};
    }
    void Save(){var p=button.PointToScreen(new Point(button.Width/2,button.Height/2));var c=canvas.PointToScreen(Point.Empty);var last=canvas.Last;
        File.WriteAllText(output+".writing",new JavaScriptSerializer().Serialize(new {handle=Handle.ToInt64(),text=edit.Text,clicks=clicks,keyDowns=keyDowns,controlKey=controlKey,shiftKey=shiftKey,asyncControl=asyncControl,asyncShift=asyncShift,buttonCenter=new{x=p.X,y=p.Y},pointer=new{x=pointer.X,y=pointer.Y},canvasOrigin=new{x=c.X,y=c.Y},down=canvas.Down,up=canvas.Up,dragMoves=canvas.DragMoves,doubleClicks=canvas.Double,wheel=canvas.Wheel,horizontal=canvas.Horizontal,last=new{x=last.X,y=last.Y}}));
        if(File.Exists(output))File.Replace(output+".writing",output,null);else File.Move(output+".writing",output);}
    [STAThread] static void Main(string[] args){SetThreadDpiAwarenessContext(new IntPtr(-4));Application.EnableVisualStyles();Application.Run(new VirtualProbe(args[0]));}
}
