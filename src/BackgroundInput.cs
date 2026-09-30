using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using System.Windows.Forms;

namespace SideScreen {
    public sealed class InputRequest {
        public string Action, ExpectedDisplayId, ObservationId, Operation, Value;
        public long WindowHandle;
        public int ElementId;
    }
    public sealed class ElementInfo {
        public int Id;
        public string RuntimeId, Name, AutomationId, ControlType, ClassName;
        public long NativeHandle;
        public string[] Actions;
        public bool Enabled, Offscreen, IsPassword, IsReadOnly;
        public Rectangle Bounds;
    }
    public sealed class Observation {
        public string Id, DisplayId, RootRuntimeId;
        public long WindowHandle, ProcessStartTicks, ExpiresUtcTicks;
        public uint ProcessId;
        public ElementInfo[] Elements;
    }
    public sealed class FocusReceipt {
        public bool ForegroundPreserved, KeyboardFocusPreserved, CursorPreserved;
        public int ForegroundChanges;
        public bool TargetActivated;
        public bool Preserved { get { return ForegroundPreserved && KeyboardFocusPreserved; } }
    }
    // This observes interference; it never tries to restore focus or move the pointer.
    public sealed class InputGuard:IDisposable {
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] struct GuiInfo {
            public uint Size,Flags;
            public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;
            public int Left,Top,Right,Bottom;
        }
        delegate void WinEvent(IntPtr hook,uint evt,IntPtr hwnd,int obj,int child,uint thread,uint time);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point p);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
        [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEvent callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint id,uint message,IntPtr w,IntPtr l);
        readonly Thread observer;
        readonly ManualResetEvent ready=new ManualResetEvent(false);
        readonly FocusReceipt receipt=new FocusReceipt { ForegroundPreserved=true,KeyboardFocusPreserved=true,CursorPreserved=true };
        IntPtr foreground,keyboard; Point cursor; uint observerId,foregroundThread;
        Exception failure;
        static IntPtr Focus(uint thread) {
            var info=new GuiInfo {Size=(uint)Marshal.SizeOf(typeof(GuiInfo))};
            if(!GetGUIThreadInfo(thread,ref info))throw new InvalidOperationException("Cannot observe keyboard focus.");
            return info.Focus;
        }
        public InputGuard(long target) {
            observer=new Thread(delegate(){
                IntPtr hook=IntPtr.Zero;
                WinEvent callback=delegate(IntPtr h,uint e,IntPtr window,int o,int c,uint t,uint time){
                    if(window!=foreground){receipt.ForegroundPreserved=false;receipt.ForegroundChanges++;}
                    if(window.ToInt64()==target)receipt.TargetActivated=true;
                };
                try {
                    foreground=GetForegroundWindow();
                    if(foreground==IntPtr.Zero || foreground.ToInt64()==target)throw new InvalidOperationException("Background input requires another foreground window on an unlocked desktop.");
                    uint pid;foregroundThread=GetWindowThreadProcessId(foreground,out pid);
                    keyboard=Focus(foregroundThread);
                    if(!GetCursorPos(out cursor))throw new InvalidOperationException("Cannot observe the pointer.");
                    observerId=GetCurrentThreadId();
                    hook=SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,0);
                    if(hook==IntPtr.Zero)throw new InvalidOperationException("Cannot monitor foreground events.");
                    using(var timer=new System.Windows.Forms.Timer {Interval=15}) {
                        timer.Tick+=delegate {Sample();}; timer.Start(); ready.Set(); Application.Run();
                    }
                    Sample();
                } catch(Exception ex){failure=ex;ready.Set();}
                finally {if(hook!=IntPtr.Zero)UnhookWinEvent(hook);GC.KeepAlive(callback);}
            });
            observer.IsBackground=true;observer.SetApartmentState(ApartmentState.STA);observer.Start();
            if(!ready.WaitOne(3000))throw new InvalidOperationException("Focus observer did not start.");
            if(failure!=null)throw failure;
        }
        void Sample() {
            try {
                if(GetForegroundWindow()!=foreground)receipt.ForegroundPreserved=false;
                if(Focus(foregroundThread)!=keyboard)receipt.KeyboardFocusPreserved=false;
                Point current;if(!GetCursorPos(out current)||current.X!=cursor.X||current.Y!=cursor.Y)receipt.CursorPreserved=false;
            } catch {receipt.KeyboardFocusPreserved=false;}
        }
        public bool Quiet {get {Sample();return receipt.Preserved;}}
        public FocusReceipt Finish() {
            Thread.Sleep(250);Sample();Dispose();return receipt;
        }
        public void Dispose() {
            if(observer.IsAlive){PostThreadMessage(observerId,0x12,IntPtr.Zero,IntPtr.Zero);observer.Join(1500);}
        }
    }
    public static class BackgroundInput {
        static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=1048576};
        static readonly string State=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SideScreen","observations");
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsChild(IntPtr parent,IntPtr child);
        [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr h);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,System.Text.StringBuilder name,int length);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr h,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="SendMessageTimeoutW")] static extern IntPtr SendText(IntPtr h,uint message,IntPtr w,string text,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="SendMessageTimeoutW")] static extern IntPtr ReadText(IntPtr h,uint message,IntPtr w,System.Text.StringBuilder text,uint flags,uint timeout,out IntPtr result);
        static string Class(IntPtr h){var value=new System.Text.StringBuilder(256);GetClassName(h,value,value.Capacity);return value.ToString();}
        static bool Kind(IntPtr h,string kind){var name=Class(h);return name.Equals(kind,StringComparison.OrdinalIgnoreCase)||name.StartsWith("WindowsForms10."+kind+".",StringComparison.OrdinalIgnoreCase);}
        static long Send(IntPtr h,uint message,long w,long l){IntPtr result;if(SendMessageTimeout(h,message,new IntPtr(w),new IntPtr(l),2,1000,out result)==IntPtr.Zero)throw new InvalidOperationException("Target message failed/timed out; outcome unknown.");return result.ToInt64();}
        static string Read(IntPtr h,uint message,int capacity,long w){var value=new System.Text.StringBuilder(capacity);IntPtr result;if(ReadText(h,message,new IntPtr(w),value,2,1000,out result)==IntPtr.Zero)throw new InvalidOperationException("Target read failed/timed out.");return value.ToString();}
        static void Notify(IntPtr h,int notification){Send(GetParent(h),0x111,(long)(ushort)GetDlgCtrlID(h)|((long)(ushort)notification<<16),h.ToInt64());}
        static IntPtr Native(AutomationElement element){return new IntPtr(element.Current.NativeWindowHandle);}
        static IntPtr ListParent(AutomationElement element){var parent=TreeWalker.ControlViewWalker.GetParent(element);return parent==null?IntPtr.Zero:Native(parent);}
        static string Runtime(AutomationElement element){return String.Join(".",element.GetRuntimeId().Select(i=>i.ToString()).ToArray());}
        public static bool Inside(Rectangle window,Rectangle display) {return window.Width>0 && window.Height>0 && display.Contains(window);}
        public static WindowRecord Scope(long handle,string expected) {
            if(String.IsNullOrEmpty(expected))throw new InvalidOperationException("ExpectedDisplayId is required from fresh Status.");
            var displays=DisplayAudit.Read();var agent=Layout.Select(displays,true);
            if(Layout.Id(agent)!=expected)throw new InvalidOperationException("Agent display changed; query Status again.");
            var area=new Rectangle(agent.X,agent.Y,(int)agent.Width,(int)agent.Height);
            if(displays.Any(d=>!d.IsVirtualMonitor && area.IntersectsWith(new Rectangle(d.X,d.Y,(int)d.Width,(int)d.Height))))throw new InvalidOperationException("Agent display overlaps another active display. Use extended displays.");
            var window=Windows.List().SingleOrDefault(w=>w.Handle==handle);
            if(window==null || IsIconic(new IntPtr(handle)) || !IsWindowEnabled(new IntPtr(handle)) || !Inside(window.Bounds,area))throw new InvalidOperationException("Target must be a visible, enabled window fully inside the agent display.");
            return window;
        }
        static IEnumerable<AutomationElement> Walk(AutomationElement root) {
            var pending=new Stack<AutomationElement>();pending.Push(root);int count=0;
            while(pending.Count>0 && count++<512) {
                var element=pending.Pop();yield return element;
                var children=new List<AutomationElement>();var child=TreeWalker.ControlViewWalker.GetFirstChild(element);
                while(child!=null && children.Count<512){children.Add(child);child=TreeWalker.ControlViewWalker.GetNextSibling(child);}
                for(int i=children.Count-1;i>=0;i--)pending.Push(children[i]);
            }
        }
        static string[] Actions(AutomationElement element) {
            var actions=new List<string>();
            if(!element.Current.IsEnabled || element.Current.IsPassword || element.Current.IsOffscreen)return actions.ToArray();
            var handle=Native(element);long style=handle==IntPtr.Zero?0:GetWindowLongPtr(handle,-16).ToInt64();
            if(Kind(handle,"Edit") && (style&0x820)==0)actions.Add("SetValue");
            if(Kind(handle,"Button")) {
                int type=(int)(style&15);
                if(type==0||type==1)actions.Add("Invoke");
                if(type==2||type==3||type==5||type==6)actions.Add("Toggle");
                if(type==11 && Class(handle).StartsWith("WindowsForms10.",StringComparison.OrdinalIgnoreCase)) {
                    if(element.Current.ControlType==ControlType.Button)actions.Add("Invoke");
                }
            }
            if(element.Current.ControlType==ControlType.ListItem && Kind(ListParent(element),"ListBox")) {
                long listStyle=GetWindowLongPtr(ListParent(element),-16).ToInt64();
                if((listStyle&0x808)==0 && ((listStyle&0x30)==0 || (listStyle&0x40)!=0))actions.Add("Select");
            }
            return actions.ToArray();
        }
        static bool ReadOnly(AutomationElement element) {
            object pattern;
            return element.TryGetCurrentPattern(ValuePattern.Pattern,out pattern) && ((ValuePattern)pattern).Current.IsReadOnly;
        }
        static string Apply(AutomationElement target,InputRequest request) {
            var handle=request.Operation=="Select"?ListParent(target):Native(target);
            if(handle==IntPtr.Zero || !IsChild(new IntPtr(request.WindowHandle),handle))throw new InvalidOperationException("Control is not a child of the scoped window.");
            switch(request.Operation) {
                case "SetValue":
                    IntPtr result;
                    if(SendText(handle,0xC,IntPtr.Zero,request.Value,2,1000,out result)==IntPtr.Zero || result==IntPtr.Zero)throw new InvalidOperationException("Text message failed/timed out; outcome unknown.");
                    int length=(int)Send(handle,0xE,0,0);
                    if(length<0||length>32768||Read(handle,0xD,length+1,length+1)!=request.Value)throw new InvalidOperationException("Text readback differs; do not retry automatically.");
                    return "value-readback-matched";
                case "Invoke":
                    Notify(handle,0);return "button-notified; inspect resulting UI";
                case "Toggle":
                    bool forms=Class(handle).StartsWith("WindowsForms10.",StringComparison.OrdinalIgnoreCase);
                    long before=forms?(long)((TogglePattern)target.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState:Send(handle,0xF0,0,0);
                    // WinForms manages its checkbox model in the click notification.
                    if(!forms){int type=(int)(GetWindowLongPtr(handle,-16).ToInt64()&15);Send(handle,0xF1,(before+1)%((type==5||type==6)?3:2),0);}
                    Notify(handle,0);
                    long after=forms?(long)((TogglePattern)target.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState:Send(handle,0xF0,0,0);
                    if(after==before)throw new InvalidOperationException("Checkbox state did not change.");
                    return "toggle-state-changed";
                case "Select":
                    int count=(int)Send(handle,0x18B,0,0),index=-1;
                    if(count<0||count>10000)throw new InvalidOperationException("Unsupported list size.");
                    for(int i=0;i<count;i++) {
                        int size=(int)Send(handle,0x18A,i,0);
                        if(size<0||size>32768)throw new InvalidOperationException("Unsupported list item.");
                        if(Read(handle,0x189,size+1,i)==target.Current.Name){if(index!=-1)throw new InvalidOperationException("List label is ambiguous.");index=i;}
                    }
                    if(index<0)throw new InvalidOperationException("List item changed.");
                    if(Send(handle,0x186,index,0)<0)throw new InvalidOperationException("Selection refused.");
                    Notify(handle,1);
                    if(Send(handle,0x188,0,0)!=index)throw new InvalidOperationException("Selection not confirmed.");
                    return "selected";
                default:throw new InvalidOperationException("Unsupported operation; no global-input fallback.");
            }
        }
        static void Cleanup() {
            Directory.CreateDirectory(State);
            foreach(var path in Directory.GetFiles(State,"*.json"))if(File.GetLastWriteTimeUtc(path)<DateTime.UtcNow.AddMinutes(-5))try{File.Delete(path);}catch(IOException){}
        }
        public static object Inspect(InputRequest request) {
            var window=Scope(request.WindowHandle,request.ExpectedDisplayId);
            var root=AutomationElement.FromHandle(new IntPtr(window.Handle));
            var info=new List<ElementInfo>();
            foreach(var element in Walk(root)) {
                var current=element.Current;
                var rect=current.BoundingRectangle;
                info.Add(new ElementInfo {Id=info.Count,RuntimeId=Runtime(element),Name=current.Name,AutomationId=current.AutomationId,ControlType=current.ControlType.ProgrammaticName,ClassName=current.ClassName,NativeHandle=current.NativeWindowHandle,Enabled=current.IsEnabled,Offscreen=current.IsOffscreen,IsPassword=current.IsPassword,IsReadOnly=ReadOnly(element),Bounds=rect.IsEmpty?Rectangle.Empty:Rectangle.FromLTRB((int)Math.Floor(rect.Left),(int)Math.Floor(rect.Top),(int)Math.Ceiling(rect.Right),(int)Math.Ceiling(rect.Bottom)),Actions=Actions(element)});
            }
            var observation=new Observation {Id=Guid.NewGuid().ToString("N"),DisplayId=request.ExpectedDisplayId,WindowHandle=window.Handle,ProcessId=window.ProcessId,ProcessStartTicks=Process.GetProcessById((int)window.ProcessId).StartTime.ToUniversalTime().Ticks,RootRuntimeId=Runtime(root),ExpiresUtcTicks=DateTime.UtcNow.AddMinutes(2).Ticks,Elements=info.ToArray()};
            Cleanup();File.WriteAllText(Path.Combine(State,observation.Id+".json"),Json.Serialize(observation));
            return new {ok=true,backend="native-control-messages",observation=observation,truncated=info.Count>=512,expiresInSeconds=120};
        }
        public static object Act(InputRequest request) {
            Guid parsed;
            if(!Guid.TryParseExact(request.ObservationId,"N",out parsed))throw new InvalidOperationException("Provide ObservationId from Inspect.");
            var path=Path.Combine(State,parsed.ToString("N")+".json");
            if(!File.Exists(path))throw new InvalidOperationException("Observation is missing or already consumed; inspect again.");
            var observation=Json.Deserialize<Observation>(File.ReadAllText(path));
            File.Delete(path); // One attempt per observation, including refusals or unknown outcomes.
            if(observation.ExpiresUtcTicks<DateTime.UtcNow.Ticks)throw new InvalidOperationException("Observation expired; inspect again.");
            if(request.ExpectedDisplayId!=observation.DisplayId || request.WindowHandle!=observation.WindowHandle)throw new InvalidOperationException("Request does not match observation window/display.");
            var window=Scope(request.WindowHandle,request.ExpectedDisplayId);
            if(window.ProcessId!=observation.ProcessId || Process.GetProcessById((int)window.ProcessId).StartTime.ToUniversalTime().Ticks!=observation.ProcessStartTicks)throw new InvalidOperationException("Window process changed; inspect again.");
            var observed=observation.Elements.SingleOrDefault(e=>e.Id==request.ElementId);
            if(observed==null || !observed.Actions.Contains(request.Operation))throw new InvalidOperationException("Operation was not supported by the inspected element. No foreground fallback.");
            bool dispatched=false;string verification=null;Exception error=null;FocusReceipt receipt;
            using(var guard=new InputGuard(window.Handle)) {
                try {
                    var root=AutomationElement.FromHandle(new IntPtr(window.Handle));
                    if(Runtime(root)!=observation.RootRuntimeId)throw new InvalidOperationException("Window identity changed.");
                    var target=Walk(root).SingleOrDefault(e=>Runtime(e)==observed.RuntimeId);
                    if(target==null || target.Current.AutomationId!=observed.AutomationId || target.Current.ControlType.ProgrammaticName!=observed.ControlType || target.Current.Name!=observed.Name || target.Current.NativeWindowHandle!=observed.NativeHandle || target.Current.ClassName!=observed.ClassName)throw new InvalidOperationException("Element changed; inspect again.");
                    if(!Actions(target).Contains(request.Operation))throw new InvalidOperationException("Element no longer supports this operation.");
                    var bounds=target.Current.BoundingRectangle;
                    if(bounds.IsEmpty || !Inside(Rectangle.FromLTRB((int)Math.Floor(bounds.Left),(int)Math.Floor(bounds.Top),(int)Math.Ceiling(bounds.Right),(int)Math.Ceiling(bounds.Bottom)),window.Bounds))throw new InvalidOperationException("Element is outside the scoped window.");
                    Scope(window.Handle,request.ExpectedDisplayId);
                    if(!guard.Quiet)throw new InvalidOperationException("Human focus changed during preparation; action refused.");
                    if(request.Operation=="SetValue" && (request.Value==null || request.Value.Length>32768 || request.Value.IndexOf('\0')>=0))throw new InvalidOperationException("Value is required, cannot contain NUL, and is limited to 32768 characters.");
                    dispatched=true;verification=Apply(target,request);
                } catch(Exception ex){error=ex;}
                receipt=guard.Finish();
            }
            return new {ok=error==null && receipt.Preserved,backend="native-control-messages",globalInputUsed=false,dispatched=dispatched,verification=verification,focus=receipt,error=error==null?null:error.Message,stop=!receipt.Preserved || error!=null,warning=receipt.Preserved?null:"Focus change observed. Stop; the operation may have completed. SideScreen did not restore focus."};
        }
        [MTAThread] public static int Main() {
            Console.InputEncoding=new System.Text.UTF8Encoding(false);
            Console.OutputEncoding=new System.Text.UTF8Encoding(false);
            object response;
            using(var mutex=new Mutex(false,"Local\\SideScreen.BackgroundInput")) {
                bool held=false;
                try {
                    try {held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}
                    if(!held)throw new InvalidOperationException("Another background operation is running.");
                    SetThreadDpiAwarenessContext(new IntPtr(-4));
                    var request=Json.Deserialize<InputRequest>(Console.In.ReadToEnd());
                    if(request==null)throw new InvalidOperationException("JSON request is required.");
                    if(request.Action=="Inspect")response=Inspect(request);
                    else if(request.Action=="Act")response=Act(request);
                    else throw new InvalidOperationException("Action must be Inspect or Act.");
                }catch(Exception ex){response=new {ok=false,error=ex.Message,stop=true};}
                finally{if(held)mutex.ReleaseMutex();}
            }
            var json=Json.Serialize(response);Console.WriteLine(json);
            return Json.Deserialize<Dictionary<string,object>>(json)["ok"].Equals(true)?0:1;
        }
    }
}
