using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace SideScreen {
    public static class VirtualInput {
        [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h,ref Point p);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h,out Rect r);
        [DllImport("user32.dll")] static extern IntPtr ChildWindowFromPointEx(IntPtr h,Point p,uint flags);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll")] static extern bool IsChild(IntPtr root,IntPtr child);
        [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
        [DllImport("kernel32.dll")] static extern bool IsWow64Process(IntPtr process,out bool wow);
        static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=1048576};
        static Process host32;
        static string DirectoryName {get{return Path.GetDirectoryName(typeof(VirtualInput).Assembly.Location);}}
        public static bool Installed {get{return File.Exists(Path.Combine(DirectoryName,"SideScreen.VirtualInput.dll"));}}
        public static void Close(){try{NativeWire.Close();}catch{}if(host32!=null){try{if(!host32.HasExited)host32.Kill();}catch{}host32.Dispose();host32=null;}}
        static int Send(NativeFrame frame,bool x86,out int blocked) {
            if(!x86)return NativeWire.Send(ref frame,out blocked);
            if(host32==null||host32.HasExited) {
                var info=new ProcessStartInfo(Path.Combine(DirectoryName,"SideScreen.Virtual32Host.exe")) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false)};
                host32=Process.Start(info);host32.ErrorDataReceived+=delegate{};host32.BeginErrorReadLine();
            }
            host32.StandardInput.WriteLine(Json.Serialize(frame));host32.StandardInput.Flush();
            var reply=host32.StandardOutput.ReadLineAsync();
            if(!reply.Wait(5000)){Close();throw new InvalidOperationException("Virtual input host timed out; outcome unknown. Observe before continuing.");}
            var value=Json.Deserialize<Dictionary<string,object>>(reply.Result??"{}");blocked=value.ContainsKey("blocked")?Convert.ToInt32(value["blocked"]):0;
            if(value.ContainsKey("error"))throw new InvalidOperationException("32-bit virtual input host: "+Convert.ToString(value["error"]));
            return value.ContainsKey("status")?Convert.ToInt32(value["status"]):-9999;
        }
        static bool Target32(WindowRecord window){bool wow;using(var process=Process.GetProcessById((int)window.ProcessId)){if(!IsWow64Process(process.Handle,out wow))throw new InvalidOperationException("Cannot identify target architecture.");return wow;}}
        static IntPtr Destination(WindowRecord window,Point screen) {
            var root=new IntPtr(window.Handle);var h=root;
            for(int n=0;n<32;n++) {
                var p=screen;Rect bounds;
                if(!ScreenToClient(h,ref p)||!GetClientRect(h,out bounds)||p.X<0||p.Y<0||p.X>=bounds.Right||p.Y>=bounds.Bottom)throw new InvalidOperationException("Virtual input is restricted to the scoped client area.");
                var child=ChildWindowFromPointEx(h,p,7);if(child==IntPtr.Zero||child==h)break;h=child;
                if(n==31)throw new InvalidOperationException("Input child hierarchy exceeds its bound.");
            }
            ValidateDestination(window,h);return h;
        }
        static void ValidateDestination(WindowRecord window,IntPtr target) {
            uint pid,rootPid;var thread=GetWindowThreadProcessId(target,out pid);var rootThread=GetWindowThreadProcessId(new IntPtr(window.Handle),out rootPid);
            if(pid!=window.ProcessId||thread!=rootThread||!IsWindowEnabled(target)||(target.ToInt64()!=window.Handle&&!IsChild(new IntPtr(window.Handle),target)))throw new InvalidOperationException("Input destination escaped its window/process/thread.");
        }
        static long Packed(Point point){return unchecked((int)((point.Y<<16)|(point.X&0xffff)));}
        static readonly Dictionary<string,int> KeyCodes=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){{"Backspace",8},{"Tab",9},{"Enter",13},{"Escape",27},{"Space",32},{"PageUp",33},{"PageDown",34},{"End",35},{"Home",36},{"Left",37},{"Up",38},{"Right",39},{"Down",40},{"Delete",46}};
        public static int KeyCode(string name){int key;if(KeyCodes.TryGetValue(name??"",out key))return key;if(name!=null&&name.Length==1&&((name[0]>='A'&&name[0]<='Z')||(name[0]>='0'&&name[0]<='9')))return name[0];throw new InvalidOperationException("Unsupported virtual key; no Windows/global shortcut route.");}
        static string Text(Dictionary<string,object> args,string key){object value;return args.TryGetValue(key,out value)?Convert.ToString(value):null;}
        static Dictionary<string,object>[] Points(object value){var items=value as IEnumerable;return items==null?new Dictionary<string,object>[0]:items.Cast<object>().Select(i=>i as Dictionary<string,object>).ToArray();}
        public static object Release(WindowRecord window) {
            int blocked;var frame=new NativeFrame {Version=1,Operation=2,Root=(ulong)window.Handle,Target=(ulong)window.Handle,Keys=new byte[256]};int status=Send(frame,Target32(window),out blocked);
            return new {ok=status==1,stop=status!=1,status=status};
        }
        public static object Act(CuaObservation observation,WindowRecord window,string operation,Dictionary<string,object> args,string cursorId,string label) {
            if(!Installed)throw new InvalidOperationException("Build/install the original virtual input adapter first.");
            using(var process=Process.GetProcessById((int)window.ProcessId))if(new[]{"chrome","msedge","brave"}.Contains(process.ProcessName.ToLowerInvariant()))throw new InvalidOperationException("Use the verified Chrome DOM/CUA route for browser windows; native adapter injection into the account browser is refused.");
            var allowed=new Dictionary<string,string[]> {{"move",new[]{"x","y"}},{"click",new[]{"x","y","button","count"}},{"drag",new[]{"points","button","duration_ms"}},{"scroll",new[]{"x","y","delta","axis"}},{"type",new[]{"element_token","text"}},{"press",new[]{"element_token","key","modifiers"}}};
            if(!allowed.ContainsKey(operation)||args.Keys.Any(k=>!allowed[operation].Contains(k)))throw new InvalidOperationException("Unsupported virtual input fields.");
            bool keyboard=operation=="type"||operation=="press";IntPtr target;Point[] points;
            if(keyboard) {
                string token=Text(args,"element_token");var matches=observation.Elements.Where(e=>Text(e,"element_token")==token&&token!=null).ToArray();
                if(matches.Length!=1||!Convert.ToBoolean(matches[0]["enabled"])||Convert.ToBoolean(matches[0]["sidescreen_text_refused"]))throw new InvalidOperationException("Keyboard target is missing, disabled, password or read-only.");
                var candidate=CuaBridge.NativeMatch(matches[0],observation.NativeElements);
                if(candidate==null||!candidate.Enabled||candidate.IsPassword||candidate.IsReadOnly||candidate.NativeHandle==0||candidate.ControlType!="ControlType.Edit")throw new InvalidOperationException("Virtual keyboard requires one corroborated native edit control; use CUA/DOM for other controls.");
                target=new IntPtr(candidate.NativeHandle);ValidateDestination(window,target);
                points=new[]{new Point(candidate.Bounds.X+candidate.Bounds.Width/2,candidate.Bounds.Y+candidate.Bounds.Height/2)};
                if(operation=="type"&&(Text(args,"text")==null||Text(args,"text").Length>32768||Text(args,"text").Contains("\0")))throw new InvalidOperationException("Invalid virtual keyboard text.");
            }else {
                var source=operation=="drag"?Points(args.ContainsKey("points")?args["points"]:null):new[]{args};
                if(source.Length<1||source.Length>64||(operation=="drag"&&source.Length<2)||source.Any(p=>p==null||!p.ContainsKey("x")||!p.ContainsKey("y")||p.Keys.Any(k=>operation=="drag"&&k!="x"&&k!="y")))throw new InvalidOperationException("Provide 2–64 capture points for a drag, or one pointer position.");
                points=source.Select(p=>SideCursor.ResolvePoint(observation,window,Convert.ToDouble(p["x"]),Convert.ToDouble(p["y"]))).ToArray();target=Destination(window,points[0]);
                foreach(var p in points){var client=p;Rect rectangle;if(!ScreenToClient(target,ref client)||!GetClientRect(target,out rectangle)||client.X<0||client.Y<0||client.X>=rectangle.Right||client.Y>=rectangle.Bottom)throw new InvalidOperationException("Gesture leaves its initial scoped client control.");}
            }
            string button=Text(args,"button")??"left";if(button!="left"&&button!="right"&&button!="middle")throw new InvalidOperationException("Invalid button.");
            int count=args.ContainsKey("count")?Convert.ToInt32(args["count"]):1;if(count<1||count>2)throw new InvalidOperationException("Click count must be 1 or 2.");
            int duration=args.ContainsKey("duration_ms")?Convert.ToInt32(args["duration_ms"]):250;if(duration<0||duration>1500)throw new InvalidOperationException("Drag duration exceeds its bound.");
            var keys=new byte[256];int keyCode=0;
            if(operation=="press") {
                keyCode=KeyCode(Text(args,"key"));var modifiers=args.ContainsKey("modifiers")?args["modifiers"] as IEnumerable:null;
                if(modifiers!=null)foreach(var m in modifiers){string name=Convert.ToString(m);if(name=="Control")keys[17]=keys[162]=128;else if(name=="Shift")keys[16]=keys[160]=128;else if(name=="Alt")keys[18]=keys[164]=128;else throw new InvalidOperationException("Unsupported modifier; Windows/global shortcuts are refused.");}
                if((keys[17]!=0&&(keyCode==67||keyCode==86||keyCode==88))||keys[18]!=0)throw new InvalidOperationException("Use the agent's private clipboard/text route; system clipboard and Alt shortcuts are refused.");
            }
            bool x86=Target32(window);int blockedTotal=0,sent=0;bool dispatched=false;string error=null;FocusReceipt focus;var deadline=Stopwatch.StartNew();Point last=points[0];
            using(var guard=new InputGuard(window.Handle)) {
                Action<uint,ulong,long,Point> send=delegate(uint message,ulong wp,long lp,Point point){
                    if(deadline.ElapsedMilliseconds>20000||!guard.Quiet)throw new InvalidOperationException("Input deadline/focus changed; stop without replay.");
                    if(sent++%16==0){var current=BackgroundInput.Scope(window.Handle,observation.DisplayId);if(current.ProcessId!=window.ProcessId||current.Bounds!=window.Bounds)throw new InvalidOperationException("Scope changed; stop without replay.");}
                    ValidateDestination(window,target);int blocked;dispatched=true;
                    var frame=new NativeFrame {Version=1,Operation=1,Root=(ulong)window.Handle,Target=(ulong)target.ToInt64(),X=point.X,Y=point.Y,Message=message,WParam=wp,LParam=lp,Keys=(byte[])keys.Clone()};
                    int status=Send(frame,x86,out blocked);blockedTotal+=blocked;if(status!=1)throw new InvalidOperationException("Virtual adapter refused/timed out ("+status+"); outcome unknown. Observe before continuing.");
                    last=point;
                };
                Action<uint,int,Point> mouse=delegate(uint message,int buttons,Point point){keys[1]=(byte)((buttons&1)!=0?128:0);keys[2]=(byte)((buttons&2)!=0?128:0);keys[4]=(byte)((buttons&16)!=0?128:0);var client=point;ScreenToClient(target,ref client);send(message,(ulong)buttons,Packed(client),point);};
                uint down=button=="left"?0x201u:button=="right"?0x204u:0x207u;int mask=button=="left"?1:button=="right"?2:16;
                try {
                    if(operation=="move")mouse(0x200,0,points[0]);
                    if(operation=="click")for(int n=0;n<count;n++){mouse(0x200,0,points[0]);try{mouse(n==1?down+2:down,mask,points[0]);}finally{mouse(down+1,0,points[0]);}}
                    if(operation=="drag") {mouse(0x200,0,points[0]);try{mouse(down,mask,points[0]);for(int n=1;n<points.Length;n++){if(duration>0)Thread.Sleep(duration/(points.Length-1));mouse(0x200,mask,points[n]);}}finally{mouse(down+1,0,points[points.Length-1]);}}
                    if(operation=="scroll") {int delta=Convert.ToInt32(args["delta"]);if(delta==0||Math.Abs((long)delta)>12000)throw new InvalidOperationException("Invalid wheel delta.");string axis=Text(args,"axis")??"vertical";if(axis!="vertical"&&axis!="horizontal")throw new InvalidOperationException("Invalid wheel axis.");send(axis=="vertical"?0x20au:0x20eu,unchecked((uint)(delta<<16)),Packed(points[0]),points[0]);}
                    if(operation=="type")foreach(char c in Text(args,"text"))send(0x102,c,1,points[0]);
                    if(operation=="press") {keys[keyCode]=128;try{send(0x100,(ulong)keyCode,1,points[0]);if(keyCode==65&&keys[17]!=0)send(0xb1,0,-1,points[0]);if(keyCode==13||keyCode==9||keyCode==8||keyCode==32)send(0x102,(ulong)keyCode,1,points[0]);}finally{keys[keyCode]=0;send(0x101,(ulong)keyCode,unchecked((int)0xc0000001),points[0]);}}
                    var after=BackgroundInput.Scope(window.Handle,observation.DisplayId);if(after.ProcessId!=window.ProcessId||after.Bounds!=window.Bounds)throw new InvalidOperationException("Scope changed during input; stop and observe.");
                    SideCursor.Mark(observation,last.X,last.Y,cursorId,label);
                }catch(Exception ex){error=ex.Message;try{Release(window);}catch{}}
                focus=guard.Finish();
            }
            return new {ok=error==null&&focus.Preserved,stop=error!=null||!focus.Preserved,dispatched=dispatched,backend="sidescreen-virtual-input",architecture=x86?"x86":"x64",blockedActivationAttempts=blockedTotal,effect=error==null?"input-delivered; verify fresh app state":"unverified; observe before more input",focus=focus,error=error};
        }
    }
}
