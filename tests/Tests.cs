using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SideScreen;

class PassiveForm:Form { protected override bool ShowWithoutActivation { get{return true;} } }
class Tests {
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Reject(Action action,string message){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,message);}
    [STAThread] static int Main(string[] args){try{
        var fitted=Layout.Fit(new Rectangle(0,0,2200,1500),new Rectangle(-1920,0,1920,1080));
        Check(fitted.Left>=-1920 && fitted.Right<=0 && fitted.Bottom<=1080,"Negative-origin monitor containment");
        var empty=new DisplayAudit.Record[0];
        Reject(()=>Layout.Select(empty,true),"Missing agent display must be refused");
        var v=new DisplayAudit.Record{IsVirtualMonitor=true};
        Reject(()=>Layout.Select(new[]{v,v},true),"Ambiguous agent display must be refused");
        Check(Layout.Select(new[]{v},true)==v,"Single agent display selected");
        Check(Layout.BelongsTo(new Rectangle(-1900,20,300,200),new Rectangle(-1920,0,1920,1080)),"Window recognized on negative-origin agent screen");
        Check(!Layout.BelongsTo(new Rectangle(20,20,300,200),new Rectangle(-1920,0,1920,1080)),"Human-screen window excluded from agent listing");
        Reject(()=>Layout.Select(new[]{v},false),"Virtual display cannot be a physical recovery destination");
        var physical=new DisplayAudit.Record{IsMainDisplay=true,DevicePath="monitor-interface"};
        Check(Layout.Select(new[]{v,physical},false)==physical,"Physical recovery destination selected");
        var identify=typeof(DisplayAudit).GetMethod("Identify",BindingFlags.NonPublic|BindingFlags.Static);
        var unknown=new DisplayAudit.Record{DevicePath="",Technology=0x80000000,TargetNameFlags=2};
        identify.Invoke(null,new object[]{unknown,new DisplayAudit.TargetName()});
        Check(!unknown.IsUsableMainDisplay,"Forced placeholder must not allow display removal");
        var external=new DisplayAudit.Record{DevicePath="\\\\?\\DISPLAY#OTHER123#abc",Technology=5};
        identify.Invoke(null,new object[]{external,new DisplayAudit.TargetName()});
        Check(external.IsUsableMainDisplay,"Generic HDMI display supported without laptop-specific EDID");
        var indirect=new DisplayAudit.Record{DevicePath="\\\\?\\DISPLAY#UNKNOWN#abc",Technology=17};
        identify.Invoke(null,new object[]{indirect,new DisplayAudit.TargetName()});
        Check(!indirect.IsUsableMainDisplay,"Unknown indirect display is not a physical recovery destination");
        Check(BackgroundInput.Inside(new Rectangle(-1800,30,400,200),new Rectangle(-1920,0,1920,1080)),"Background control accepts full negative-origin containment");
        Check(!BackgroundInput.Inside(new Rectangle(-100,30,400,200),new Rectangle(-1920,0,1920,1080)),"Background control rejects partial monitor overlap");
        Check(!BackgroundInput.Inside(new Rectangle(0,0,0,0),new Rectangle(0,0,1920,1080)),"Background control rejects empty bounds");
        Check(!BackgroundInput.Inside(new Rectangle(20,20,400,200),new Rectangle(-1920,0,1920,1080)),"Background control rejects human-screen bounds");
        Reject(()=>CuaBridge.ValidateArguments("click",new Dictionary<string,object>{{"delivery_mode","foreground"}}),"CUA refuses delivery override");
        Reject(()=>CuaBridge.ValidateArguments("click",new Dictionary<string,object>{{"target",new object()}}),"CUA refuses target override");
        Reject(()=>CuaBridge.ValidateArguments("bring_to_front",new Dictionary<string,object>()),"CUA refuses activation tools");
        Reject(()=>CuaBridge.ValidateArguments("click",new Dictionary<string,object>{{"x",1}}),"CUA refuses incomplete coordinates");
        Reject(()=>CuaBridge.ValidateArguments("type_text",new Dictionary<string,object>{{"text","hello"}}),"CUA refuses unbound typing");
        Reject(()=>CuaBridge.ValidateArguments("click",new Dictionary<string,object>{{"element_token","token"},{"x",1},{"y",2}}),"CUA refuses mixed grounding");
        Reject(()=>CuaBridge.ValidateArguments("set_value",new Dictionary<string,object>{{"element_token","token"},{"value","a\0b"}}),"CUA refuses NUL text");
        if(args.Contains("--live-placement")) {
            using(var form=new PassiveForm{Text="SideScreen disposable placement test",Width=320,Height=100,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(100,100)}) {
                form.Show();Application.DoEvents();
                var receipt=SideScreen.Windows.MoveWithin(form.Handle.ToInt64(),Screen.PrimaryScreen.WorkingArea);
                Check(receipt.FocusPreserved,"Live placement changed foreground window");
                Check(receipt.CursorPreserved,"Live placement moved the cursor");
                Check(Screen.PrimaryScreen.WorkingArea.IntersectsWith(receipt.Bounds),"Live placement left the selected display");
                var agentDisplays=DisplayAudit.Read().Where(d=>d.IsVirtualMonitor).ToArray();
                if(agentDisplays.Length==1) {
                    var agent=agentDisplays[0];
                    var agentReceipt=SideScreen.Windows.Move(form.Handle.ToInt64(),true,Layout.Id(agent));
                    Check(agentReceipt.FocusPreserved,"Agent-screen placement changed foreground window");
                    Check(agentReceipt.CursorPreserved,"Agent-screen placement moved the cursor");
                    Check(Layout.BelongsTo(agentReceipt.Bounds,new Rectangle(agent.X,agent.Y,(int)agent.Width,(int)agent.Height)),"Agent-screen placement missed the agent display");
                }
            }
        }
        Console.WriteLine("PASS: "+checks+" checks"+(args.Contains("--live-placement")?" including live focus/cursor preservation":""));return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
