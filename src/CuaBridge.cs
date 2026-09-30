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
    public sealed class CuaRequest {
        public string Action, ExpectedDisplayId, ObservationId, Tool;
        public long WindowHandle;
        public bool IncludeScreenshot=true;
        public Dictionary<string,object> Arguments;
    }
    public sealed class CuaObservation {
        public string Id, DisplayId, NativeId, CaptureId, ScreenshotPath;
        public long WindowHandle, ProcessStartTicks, ExpiresUtcTicks;
        public uint ProcessId;
        public Rectangle Bounds;
        public int ImageWidth, ImageHeight;
        public Dictionary<string,object>[] Elements;
        public ElementInfo[] NativeElements;
    }
    public static class CuaBridge {
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=8388608};
        static readonly string State=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SideScreen","cua-observations");
        public static string Binary {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Cua","cua-driver","bin","cua-driver.exe");}}
        public static string Socket {get {return @"\\.\pipe\SideScreen.Cua."+Process.GetCurrentProcess().SessionId;}}
        static string Text(Dictionary<string,object> value,string key) {object item;return value.TryGetValue(key,out item)?Convert.ToString(item):null;}
        static Dictionary<string,object> Object(object item) {return item as Dictionary<string,object>;}
        static Dictionary<string,object>[] Objects(object item) {var array=item as IEnumerable;return array==null?new Dictionary<string,object>[0]:array.Cast<object>().Select(Object).Where(v=>v!=null).ToArray();}
        static bool Failed(Dictionary<string,object> value) {return (value.ContainsKey("isError") && Convert.ToBoolean(value["isError"])) || (value.ContainsKey("ok") && !Convert.ToBoolean(value["ok"])) || value.ContainsKey("error");}
        static Dictionary<string,object> Driver(string tool,Dictionary<string,object> args,int timeout=20000) {
            if(!File.Exists(Binary))throw new InvalidOperationException("Install the optional official CUA Driver first; see docs/cua.md.");
            var info=new ProcessStartInfo(Binary,"call "+tool+" --socket "+Socket) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)};
            info.EnvironmentVariables["CUA_DRIVER_RS_TELEMETRY_ENABLED"]="false";
            using(var process=Process.Start(info)) {
                var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
                byte[] bytes=Encoding.UTF8.GetBytes(Json.Serialize(args));process.StandardInput.BaseStream.Write(bytes,0,bytes.Length);process.StandardInput.Close();
                if(!process.WaitForExit(timeout)){process.Kill();throw new InvalidOperationException("CUA timed out; outcome unknown. Observe before continuing; never retry automatically.");}
                Dictionary<string,object> result;
                try {result=Json.Deserialize<Dictionary<string,object>>(output.Result);} catch {throw new InvalidOperationException("CUA did not return JSON. Ensure the SideScreen CUA service is running. "+errors.Result.Trim());}
                if(result==null)throw new InvalidOperationException("Empty CUA response. Ensure SideScreen's CUA supervisor is running. "+errors.Result.Trim());
                if(process.ExitCode!=0 && !Failed(result))result["isError"]=true;
                return result;
            }
        }
        static Dictionary<string,object> Target(CuaObservation observation) {return new Dictionary<string,object>{{"pid",observation.ProcessId},{"window_id",observation.WindowHandle},{"session","sidescreen-"+observation.Id}};}
        static string[] Strings(object value) {var items=value as IEnumerable;return items==null?new string[0]:items.Cast<object>().Select(Convert.ToString).ToArray();}
        static ElementInfo NativeMatch(Dictionary<string,object> element,ElementInfo[] native) {
            string role=Text(element,"role"),name=Text(element,"label");
            var frame=element.ContainsKey("frame")?Object(element["frame"]):null;
            if(frame!=null) {
                var byFrame=native.Where(e=>e.ControlType=="ControlType."+role && e.Bounds.Width>0 && Math.Abs(e.Bounds.X-Convert.ToDouble(frame["x"]))<=2 && Math.Abs(e.Bounds.Y-Convert.ToDouble(frame["y"]))<=2 && Math.Abs(e.Bounds.Width-Convert.ToDouble(frame["w"]))<=2 && Math.Abs(e.Bounds.Height-Convert.ToDouble(frame["h"]))<=2).ToArray();
                if(byFrame.Length==1)return byFrame[0];
            }
            var matches=native.Where(e=>e.Name==name && e.ControlType=="ControlType."+role).ToArray();
            return matches.Length==1?matches[0]:null;
        }
        public static void ValidateArguments(string tool,Dictionary<string,object> args) {
            var fields=new Dictionary<string,string[]> {
                {"click",new[]{"element_token","x","y","button","count","action"}},
                {"set_value",new[]{"element_token","value"}},
                {"type_text",new[]{"element_token","text","delay_ms"}},
                {"scroll",new[]{"element_token","direction","amount"}},
                {"press_key",new[]{"element_token","key","modifiers"}},
                {"hotkey",new[]{"element_token","keys"}}
            };
            if(tool==null || !fields.ContainsKey(tool))throw new InvalidOperationException("Unsupported CUA tool; no foreground, desktop, launch, shell or clipboard route is exposed.");
            if(args.Keys.Any(k=>!fields[tool].Contains(k)))throw new InvalidOperationException("Unsupported argument. Targets, session and background delivery are fixed by SideScreen.");
            if(args.ContainsKey("x")!=args.ContainsKey("y"))throw new InvalidOperationException("Provide both x and y.");
            if(args.ContainsKey("element_token") && args.ContainsKey("x"))throw new InvalidOperationException("Choose element_token or pixels, not both.");
            if(tool=="click" && !args.ContainsKey("element_token") && !args.ContainsKey("x"))throw new InvalidOperationException("Click requires a returned element_token or screenshot pixels.");
            if((tool=="type_text" || tool=="set_value" || tool=="press_key" || tool=="hotkey") && !args.ContainsKey("element_token"))throw new InvalidOperationException("Text and keyboard actions require a returned element_token.");
            foreach(string key in new[]{"text","value"})if(args.ContainsKey(key) && (!(args[key] is string) || ((string)args[key]).Length>32768 || ((string)args[key]).Contains("\0")))throw new InvalidOperationException("Invalid text value.");
        }
        static object Observe(CuaRequest request) {
            var window=BackgroundInput.Scope(request.WindowHandle,request.ExpectedDisplayId);
            Directory.CreateDirectory(State);
            foreach(string old in Directory.GetFiles(State,"*.json"))if(File.GetLastWriteTimeUtc(old)<DateTime.UtcNow.AddMinutes(-5))File.Delete(old);
            var observation=new CuaObservation {Id=Guid.NewGuid().ToString("N"),DisplayId=request.ExpectedDisplayId,WindowHandle=window.Handle,ProcessId=window.ProcessId,ProcessStartTicks=Process.GetProcessById((int)window.ProcessId).StartTime.ToUniversalTime().Ticks,ExpiresUtcTicks=DateTime.UtcNow.AddMinutes(2).Ticks,Bounds=window.Bounds};
            var args=Target(observation);args["max_elements"]=512;args["timeout_ms"]=5000;args["include_screenshot"]=request.IncludeScreenshot;
            if(request.IncludeScreenshot){observation.ScreenshotPath=Path.Combine(State,observation.Id+".png");args["screenshot_out_file"]=observation.ScreenshotPath;args["max_image_dimension"]=0;}
            var result=Driver("get_window_state",args);
            if(Failed(result))return new {ok=false,stop=true,driver=result};
            // CUA's accessibility advertisement alone is not proof of safe input.
            var native=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(BackgroundInput.Inspect(new InputRequest {Action="Inspect",WindowHandle=window.Handle,ExpectedDisplayId=request.ExpectedDisplayId})));
            var nativeObservation=Json.Deserialize<Observation>(Json.Serialize(native["observation"]));
            observation.NativeId=nativeObservation.Id;observation.NativeElements=nativeObservation.Elements;
            observation.Elements=Objects(result["elements"]);observation.CaptureId=Text(result,"capture_id");
            foreach(var element in observation.Elements) {
                var match=NativeMatch(element,observation.NativeElements);
                // Password/read-only native elements are excluded by the native backend.
                bool classic=match!=null && match.NativeHandle!=0 && (match.ClassName=="Edit" || (match.ClassName??"").StartsWith("WindowsForms10.EDIT.",StringComparison.OrdinalIgnoreCase));
                element["sidescreen_native_set_value"]=classic && match.Actions.Contains("SetValue");
                element["sidescreen_text_refused"]=(match!=null && (match.IsPassword || match.IsReadOnly)) || (classic && !match.Actions.Contains("SetValue"));
                element["sidescreen_route"]=classic?"native-control-messages":"cua-background-experimental";
            }
            if(request.IncludeScreenshot) {
                using(var image=Image.FromFile(observation.ScreenshotPath)){observation.ImageWidth=image.Width;observation.ImageHeight=image.Height;}
            }
            var current=BackgroundInput.Scope(window.Handle,request.ExpectedDisplayId);
            if(current.ProcessId!=window.ProcessId || current.Bounds!=window.Bounds)throw new InvalidOperationException("Window changed during observation; observe again.");
            File.WriteAllText(Path.Combine(State,observation.Id+".json"),Json.Serialize(observation));
            return new {ok=true,backend="cua-driver",observationId=observation.Id,expiresInSeconds=120,windowHandle=window.Handle,displayId=observation.DisplayId,state=result,screenshotPath=observation.ScreenshotPath};
        }
        static object Act(CuaRequest request) {
            Guid id;if(!Guid.TryParseExact(request.ObservationId,"N",out id))throw new InvalidOperationException("Provide ObservationId from CuaObserve.");
            string path=Path.Combine(State,id.ToString("N")+".json");
            if(!File.Exists(path))throw new InvalidOperationException("Observation missing or consumed; observe again.");
            var observation=Json.Deserialize<CuaObservation>(File.ReadAllText(path));File.Delete(path);
            if(observation.ExpiresUtcTicks<DateTime.UtcNow.Ticks || observation.DisplayId!=request.ExpectedDisplayId || observation.WindowHandle!=request.WindowHandle)throw new InvalidOperationException("Expired or mismatched observation.");
            var window=BackgroundInput.Scope(request.WindowHandle,request.ExpectedDisplayId);
            if(window.ProcessId!=observation.ProcessId || Process.GetProcessById((int)window.ProcessId).StartTime.ToUniversalTime().Ticks!=observation.ProcessStartTicks || window.Bounds!=observation.Bounds)throw new InvalidOperationException("Window process/geometry changed; observe again.");
            var args=request.Arguments??new Dictionary<string,object>();ValidateArguments(request.Tool,args);
            Dictionary<string,object> element=null;
            if(args.ContainsKey("element_token")) {
                string token=Text(args,"element_token");element=observation.Elements.SingleOrDefault(e=>Text(e,"element_token")==token);
                if(element==null || !element.ContainsKey("enabled") || !Convert.ToBoolean(element["enabled"]))throw new InvalidOperationException("Token not in this observation or element disabled.");
                string needed=request.Tool=="set_value" || request.Tool=="type_text"?"set_value":request.Tool=="click"?(Text(args,"action")=="expand"?"expand":"invoke"):null;
                if(needed!=null && !Strings(element["actions"]).Contains(needed) && !(request.Tool=="click" && needed=="invoke" && Strings(element["actions"]).Any(a=>a=="toggle" || a=="select")))throw new InvalidOperationException("Element did not advertise this action.");
                if(Convert.ToBoolean(element["sidescreen_text_refused"]))throw new InvalidOperationException("Read-only/password native edit is refused.");
                if(Convert.ToBoolean(element["sidescreen_native_set_value"])) {
                    if(request.Tool!="set_value")throw new InvalidOperationException("Classic edit supports set_value replacement through SideScreen native messages only; no UIA/keyboard fallback.");
                    var match=NativeMatch(element,observation.NativeElements);
                    return BackgroundInput.Act(new InputRequest {Action="Act",WindowHandle=window.Handle,ExpectedDisplayId=request.ExpectedDisplayId,ObservationId=observation.NativeId,ElementId=match.Id,Operation="SetValue",Value=Text(args,"value")});
                }
                // Native unsupported edit controls must not gain a UIA typing route.
                var native=NativeMatch(element,observation.NativeElements);
                if((request.Tool=="set_value" || request.Tool=="type_text") && native==null)throw new InvalidOperationException("CUA text element could not be corroborated against the window's native inspection.");
                if(request.Tool=="click" && !args.ContainsKey("button") && !args.ContainsKey("count") && !args.ContainsKey("action") && native!=null) {
                    string operation=new[]{"Invoke","Toggle","Select"}.FirstOrDefault(a=>native.Actions.Contains(a));
                    if(operation!=null)return BackgroundInput.Act(new InputRequest {Action="Act",WindowHandle=window.Handle,ExpectedDisplayId=request.ExpectedDisplayId,ObservationId=observation.NativeId,ElementId=native.Id,Operation=operation});
                }
                if((request.Tool=="set_value" || request.Tool=="type_text") && native!=null && native.NativeHandle!=0)throw new InvalidOperationException("Native text control is unsupported by the safe message route.");
            }
            if(args.ContainsKey("x")) {
                double x=Convert.ToDouble(args["x"]),y=Convert.ToDouble(args["y"]);
                if(observation.ScreenshotPath==null || String.IsNullOrEmpty(observation.CaptureId))throw new InvalidOperationException("Pixel input needs a fresh CUA screenshot with capture_id.");
                if(Double.IsNaN(x)||Double.IsInfinity(x)||Double.IsNaN(y)||Double.IsInfinity(y)||x<0||y<0||x>=observation.ImageWidth||y>=observation.ImageHeight)throw new InvalidOperationException("Pixels are outside the observed screenshot.");
                args["capture_id"]=observation.CaptureId;
            }
            foreach(var entry in Target(observation))args[entry.Key]=entry.Value;
            args["delivery_mode"]="background";
            bool dispatched=false;Dictionary<string,object> result=null;string error=null;FocusReceipt focus;
            using(var guard=new InputGuard(window.Handle)) {
                try {
                    BackgroundInput.Scope(window.Handle,request.ExpectedDisplayId);
                    if(!guard.Quiet)throw new InvalidOperationException("Focus changed during preparation.");
                    dispatched=true;result=Driver(request.Tool,args);
                }catch(Exception ex){error=ex.Message;}
                focus=guard.Finish();
            }
            bool ok=error==null && result!=null && !Failed(result) && focus.Preserved;
            return new {ok=ok,stop=!ok,dispatched=dispatched,backend="cua-driver",deliveryMode="background",universalIsolation=false,driver=result,focus=focus,error=error??(!focus.Preserved?"Foreground or keyboard focus changed; stop and observe.":null)};
        }
        public static object Execute(CuaRequest request) {
            if(request.Action=="Status") {
                var virtualDisplays=DisplayAudit.Read().Where(d=>d.IsVirtualMonitor).ToArray();
                object screen=null;
                if(virtualDisplays.Length==1){var d=virtualDisplays[0];screen=new {id=Layout.Id(d),deviceName=d.GdiName,x=d.X,y=d.Y,width=d.Width,height=d.Height};}
                return new {ok=true,available=virtualDisplays.Length==1,agentScreen=screen,installed=File.Exists(Binary),binary=Binary,socket=Socket,inputIsolation=false,deliveryMode="background",tools=new[]{"click","set_value","type_text","scroll","press_key","hotkey"},version="0.3.0"};
            }
            if(request.Action=="Windows")return new {ok=true,windows=Windows.ListOnAgentDisplay()};
            if(request.Action=="CuaObserve")return Observe(request);
            if(request.Action=="CuaAct")return Act(request);
            throw new InvalidOperationException("Unknown SideScreen CUA action.");
        }
        [MTAThread] public static int Main() {
            Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
            SetThreadDpiAwarenessContext(new IntPtr(-4));
            try {
                using(var mutex=new Mutex(false,"Local\\SideScreen.BackgroundInput")) {
                    if(!mutex.WaitOne(0))throw new InvalidOperationException("Another SideScreen action is running.");
                    try {
                        var request=Json.Deserialize<CuaRequest>(Console.In.ReadToEnd());
                        try {
                            var response=Execute(request);string output=Json.Serialize(response);Console.WriteLine(output);
                            return Convert.ToBoolean(Json.Deserialize<Dictionary<string,object>>(output)["ok"])?0:1;
                        }finally {
                            Guid id;
                            if(request!=null && request.Action=="CuaAct" && Guid.TryParseExact(request.ObservationId,"N",out id)) {
                                try{Driver("end_session",new Dictionary<string,object>{{"session","sidescreen-"+id.ToString("N")}},1500);}catch{}
                            }
                        }
                    }finally{mutex.ReleaseMutex();}
                }
            }catch(Exception ex){Console.WriteLine(Json.Serialize(new {ok=false,stop=true,error=ex.Message}));return 1;}
        }
    }
}
