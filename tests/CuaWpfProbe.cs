using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using SideScreen;
class CuaWpfProbe {
    [STAThread] static void Main(string[] args) {
        var display=Layout.Select(DisplayAudit.Read(),true);
        var window=new Window {Title="SideScreen CUA WPF test",Width=620,Height=360,Left=display.X+740,Top=display.Y+80,ShowActivated=false};
        var panel=new StackPanel {Margin=new Thickness(25)};
        var text=new TextBox {Name="AgentText",Height=32,Text="initial"};
        var button=new Button {Name="AgentButton",Content="Increment WPF counter",Height=38,Margin=new Thickness(0,12,0,0)};
        var check=new CheckBox {Name="AgentCheck",Content="WPF checkbox",Margin=new Thickness(0,16,0,0)};
        var counter=new TextBlock {Name="AgentCounter",Text="Count: 0",Margin=new Thickness(0,16,0,0)};
        int clicks=0;
        Action save=()=>File.WriteAllText(args[0],new JavaScriptSerializer().Serialize(new {handle=new System.Windows.Interop.WindowInteropHelper(window).Handle.ToInt64(),text=text.Text,clicks=clicks,check=check.IsChecked}));
        text.TextChanged+=delegate{save();};button.Click+=delegate{clicks++;counter.Text="Count: "+clicks;save();};check.Checked+=delegate{save();};check.Unchecked+=delegate{save();};
        panel.Children.Add(text);panel.Children.Add(button);panel.Children.Add(check);panel.Children.Add(counter);window.Content=panel;window.ContentRendered+=delegate{save();};
        new Application().Run(window);
    }
}
