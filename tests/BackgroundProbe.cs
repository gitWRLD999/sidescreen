using System;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using SideScreen;
class BackgroundProbe:Form {
    readonly string output;
    readonly TextBox text=new TextBox {Name="AgentText",AccessibleName="Agent text",Location=new Point(25,25),Width=450};
    readonly Button button=new Button {Name="AgentButton",Text="Increment counter",Location=new Point(25,75),Width=180};
    readonly CheckBox check=new CheckBox {Name="AgentCheck",Text="Agent checkbox",FlatStyle=FlatStyle.System,Location=new Point(25,125),Width=180};
    readonly ListBox list=new ListBox {Name="AgentList",Location=new Point(25,175),Size=new Size(220,100)};
    int clicks;
    protected override bool ShowWithoutActivation {get{return true;}}
    BackgroundProbe(string path) {
        output=path;var display=SideScreen.Layout.Select(DisplayAudit.Read(),true);
        Text="SideScreen background input test";StartPosition=FormStartPosition.Manual;
        Location=new Point(display.X+80,display.Y+80);Size=new Size(600,400);
        list.Items.AddRange(new object[]{"First choice","Second choice"});
        Controls.AddRange(new Control[]{text,button,check,list});
        Controls.Add(new TextBox {Name="ReadOnlyText",AccessibleName="Read only",ReadOnly=true,Text="unchanged",Location=new Point(280,175)});
        Controls.Add(new TextBox {Name="PasswordText",AccessibleName="Password",UseSystemPasswordChar=true,Location=new Point(280,210)});
        Controls.Add(new CheckBox {Name="CustomCheck",Text="Custom checkbox",Location=new Point(280,245),Width=180});
        text.TextChanged+=delegate{Save();};button.Click+=delegate{clicks++;Save();};
        check.CheckedChanged+=delegate{Save();};list.SelectedIndexChanged+=delegate{Save();};
        Shown+=delegate{Save();};
    }
    void Save() {File.WriteAllText(output,new JavaScriptSerializer().Serialize(new {handle=Handle.ToInt64(),text=text.Text,clicks=clicks,check=check.Checked,selection=list.SelectedIndex}));}
    [STAThread] static void Main(string[] args){Application.EnableVisualStyles();Application.Run(new BackgroundProbe(args[0]));}
}
