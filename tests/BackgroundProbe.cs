using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using SideScreen;
class BackgroundProbe:Form {
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    readonly string output;
    readonly TextBox text=new TextBox {Name="AgentText",AccessibleName="Agent text",Location=new Point(25,25),Width=450};
    readonly Button button=new Button {Name="AgentButton",Text="Increment counter",Location=new Point(25,75),Width=180};
    readonly CheckBox check=new CheckBox {Name="AgentCheck",Text="Agent checkbox",FlatStyle=FlatStyle.System,Location=new Point(25,125),Width=180};
    readonly ListBox list=new ListBox {Name="AgentList",Location=new Point(25,175),Size=new Size(220,100)};
    readonly Panel canvas=new Panel {Name="AgentCanvas",AccessibleName="Agent canvas",Location=new Point(25,290),Size=new Size(220,50),BackColor=Color.LightBlue};
    int clicks;
    int canvasClicks;
    Point canvasPointer=new Point(-1,-1);
    protected override bool ShowWithoutActivation {get{return true;}}
    BackgroundProbe(string path) {
        output=path;var display=SideScreen.Layout.Select(DisplayAudit.Read(),true);
        Text="SideScreen background input test";StartPosition=FormStartPosition.Manual;
        Location=new Point(display.X+80,display.Y+80);Size=new Size(600,400);
        list.Items.AddRange(new object[]{"First choice","Second choice"});
        Controls.AddRange(new Control[]{text,button,check,list,canvas});
        Controls.Add(new TextBox {Name="ReadOnlyText",AccessibleName="Read only",ReadOnly=true,Text="unchanged",Location=new Point(280,175)});
        Controls.Add(new TextBox {Name="PasswordText",AccessibleName="Password",UseSystemPasswordChar=true,Location=new Point(280,210)});
        Controls.Add(new CheckBox {Name="CustomCheck",Text="Custom checkbox",Location=new Point(280,245),Width=180});
        text.TextChanged+=delegate{Save();};button.Click+=delegate{clicks++;Save();};
        check.CheckedChanged+=delegate{Save();};list.SelectedIndexChanged+=delegate{Save();};
        canvas.MouseMove+=delegate(object sender,MouseEventArgs e){canvasPointer=e.Location;Save();};
        canvas.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){canvasClicks++;Save();}};
        Shown+=delegate{Save();};
    }
    void Save() {var center=canvas.PointToScreen(new Point(canvas.Width/2,canvas.Height/2));File.WriteAllText(output,new JavaScriptSerializer().Serialize(new {handle=Handle.ToInt64(),text=text.Text,clicks=clicks,check=check.Checked,selection=list.SelectedIndex,canvasClicks=canvasClicks,canvasPointer=new {x=canvasPointer.X,y=canvasPointer.Y},canvasScreenCenter=new {x=center.X,y=center.Y},canvasWidth=canvas.Width,canvasHeight=canvas.Height}));}
    [STAThread] static void Main(string[] args){SetThreadDpiAwarenessContext(new IntPtr(-4));Application.EnableVisualStyles();Application.Run(new BackgroundProbe(args[0]));}
}
