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
        public bool IncludeAccessibilityTree=true;
        public int MaxElements=256, MaxDepth=20, MaxImageDimension=1280;
        public string Query;
        public Dictionary<string,object> Arguments;
    }
    public sealed class CuaObservation {
        public string Id, DisplayId, NativeId, CaptureId, ScreenshotPath;
        public long WindowHandle, ProcessStartTicks, ExpiresUtcTicks;
        public uint ProcessId;
        public Rectangle Bounds;
        public int ImageWidth, ImageHeight;
        public bool HasCaptureTransform;
        public double CaptureOriginX,CaptureOriginY,CaptureScaleX,CaptureScaleY;
        public Dictionary<string,object>[] Elements;
        public ElementInfo[] NativeElements;
    }
    public static class CuaBridge {
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=8388608};
        static readonly string State=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AgentTools","SideScreen","state","cua-observations");
        public static string Binary {get {
            string configured=Environment.GetEnvironmentVariable("SIDESCREEN_CUA_BINARY");if(!String.IsNullOrEmpty(configured))return configured;
            string shared=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AgentTools","Cua","bin","cua-driver.exe");if(File.Exists(shared))return shared;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Cua","cua-driver","bin","cua-driver.exe");
        }}
        public static string Socket {get {return @"\\.\pipe\SideScreen.Cua."+Process.GetCurrentProcess().SessionId;}}
        static string Text(Dictionary<string,object> value,string key) {object item;return value.TryGetValue(key,out item)?Convert.ToString(item):null;}
        static Dictionary<string,object> Object(object item) {return item as Dictionary<string,object>;}
        static Dictionary<string,object>[] Objects(object item) {var array=item as IEnumerable;return array==null?new Dictionary<string,object>[0]:array.Cast<object>().Select(Object).Where(v=>v!=null).ToArray();}
        static bool Failed(Dictionary<string,object> value) {return (value.ContainsKey("isError") && Convert.ToBoolean(value["isError"])) || (value.ContainsKey("ok") && !Convert.ToBoolean(value["ok"])) || value.ContainsKey("error");}
        static Process driverProcess;
        static bool resident;
        static System.Threading.Tasks.Task<string> driverLine;
        static int rpcId;
        static readonly Dictionary<string,InputGuard> guards=new Dictionary<string,InputGuard>();
        static void WriteDriver(object message) {
            byte[] bytes=Encoding.UTF8.GetBytes(Json.Serialize(message)+"\n");
            driverProcess.StandardInput.BaseStream.Write(bytes,0,bytes.Length);driverProcess.StandardInput.BaseStream.Flush();
        }
        static Dictionary<string,object> Rpc(string method,object parameters,int timeout) {
            int id=++rpcId;
            WriteDriver(new {jsonrpc="2.0",id=id,method=method,@params=parameters});
            var timer=Stopwatch.StartNew();
            while(timer.ElapsedMilliseconds<timeout) {
                if(driverLine==null)driverLine=driverProcess.StandardOutput.ReadLineAsync();
                if(!driverLine.Wait(Math.Max(1,timeout-(int)timer.ElapsedMilliseconds)))throw new InvalidOperationException("CUA timed out; outcome unknown. Observe before continuing; never retry automatically.");
                string line=driverLine.Result;driverLine=null;
                if(line==null)throw new InvalidOperationException("CUA connection closed; observe before continuing.");
                var response=Json.Deserialize<Dictionary<string,object>>(line);
                if(!response.ContainsKey("id") || Convert.ToInt32(response["id"])!=id)continue;
                if(response.ContainsKey("error"))throw new InvalidOperationException(Json.Serialize(response["error"]));
                return Object(response["result"]);
            }
            throw new InvalidOperationException("CUA response deadline exceeded; outcome unknown.");
        }
        static void CloseDriver() {if(driverProcess!=null){try{if(!driverProcess.HasExited)driverProcess.Kill();}catch{}driverProcess.Dispose();driverProcess=null;driverLine=null;}}
        static Dictionary<string,object> Driver(string tool,Dictionary<string,object> args,int timeout=20000) {
            if(!File.Exists(Binary))throw new InvalidOperationException("Install the optional official CUA Driver first; see docs/cua.md.");
            // A one-shot MCP client's close ends its sessions. Preserve the CLI
            // transport for legacy separate observe/act processes; Muse keeps MCP alive.
            if(!resident) {
                var cli=new ProcessStartInfo(Binary,"call "+tool+" --socket "+Socket) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)};
                cli.EnvironmentVariables["CUA_DRIVER_RS_TELEMETRY_ENABLED"]="false";
                using(var process=Process.Start(cli)) {
                    var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
                    byte[] bytes=Encoding.UTF8.GetBytes(Json.Serialize(args));process.StandardInput.BaseStream.Write(bytes,0,bytes.Length);process.StandardInput.Close();
                    if(!process.WaitForExit(timeout)){process.Kill();throw new InvalidOperationException("CUA timed out; outcome unknown. Observe before continuing; never retry automatically.");}
                    var result=Json.Deserialize<Dictionary<string,object>>(output.Result);
                    if(result==null)throw new InvalidOperationException("Empty CUA response. "+errors.Result.Trim());
                    if(process.ExitCode!=0 && !Failed(result))result["isError"]=true;return result;
                }
            }
            try {
                if(driverProcess==null || driverProcess.HasExited) {
                    CloseDriver();
                    var info=new ProcessStartInfo(Binary,"mcp --socket "+Socket) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)};
                    info.EnvironmentVariables["CUA_DRIVER_RS_TELEMETRY_ENABLED"]="false";
                    driverProcess=Process.Start(info);driverProcess.ErrorDataReceived+=delegate{};driverProcess.BeginErrorReadLine();
                    Rpc("initialize",new {protocolVersion="2024-11-05",capabilities=new {},clientInfo=new {name="SideScreen",version="0.4.0"}},15000);
                    WriteDriver(new {jsonrpc="2.0",method="notifications/initialized"});
                }
                var result=Rpc("tools/call",new {name=tool,arguments=args},timeout);
                if(result.ContainsKey("structuredContent")) {
                    var structured=Object(result["structuredContent"]);
                    if(Failed(result)){structured["isError"]=true;structured["messages"]=Objects(result.ContainsKey("content")?result["content"]:null).Select(c=>Text(c,"text")).Where(t=>t!=null).ToArray();}
                    return structured;
                }
                foreach(var content in Objects(result.ContainsKey("content")?result["content"]:null)) {
                    string text=Text(content,"text");
                    if(text!=null && text.TrimStart().StartsWith("{")){var parsed=Json.Deserialize<Dictionary<string,object>>(text);if(Failed(result))parsed["isError"]=true;return parsed;}
                }
                return result;
            }catch{CloseDriver();throw;}
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
            var clock=Stopwatch.StartNew();
            var args=Target(observation);args["max_elements"]=Math.Max(1,Math.Min(512,request.MaxElements));args["max_depth"]=Math.Max(1,Math.Min(50,request.MaxDepth));args["timeout_ms"]=5000;args["include_screenshot"]=request.IncludeScreenshot;args["include_accessibility_tree"]=request.IncludeAccessibilityTree;
            if(!String.IsNullOrEmpty(request.Query))args["query"]=request.Query;
            if(request.IncludeScreenshot){observation.ScreenshotPath=Path.Combine(State,observation.Id+".png");args["screenshot_out_file"]=observation.ScreenshotPath;args["max_image_dimension"]=Math.Max(0,Math.Min(4096,request.MaxImageDimension));}
            var result=Driver("get_window_state",args);
            long driverMs=clock.ElapsedMilliseconds;
            if(Failed(result))return new {ok=false,stop=true,driver=result};
            // CUA's accessibility advertisement alone is not proof of safe input.
            observation.NativeElements=new ElementInfo[0];
            if(request.IncludeAccessibilityTree) {
                var native=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(BackgroundInput.Inspect(new InputRequest {Action="Inspect",WindowHandle=window.Handle,ExpectedDisplayId=request.ExpectedDisplayId})));
                var nativeObservation=Json.Deserialize<Observation>(Json.Serialize(native["observation"]));
                observation.NativeId=nativeObservation.Id;observation.NativeElements=nativeObservation.Elements;
            }
            observation.Elements=Objects(result.ContainsKey("elements")?result["elements"]:null);observation.CaptureId=Text(result,"capture_id");
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
                // Bind image pixels to physical pixels from a corroborated CUA/UIA
                // anchor. GetWindowRect includes invisible DWM borders and cannot
                // safely substitute for the actual capture origin/scale.
                foreach(var e in observation.Elements) {
                    var match=NativeMatch(e,observation.NativeElements);
                    var frame=e.ContainsKey("frame")?Object(e["frame"]):null;
                    var imageFrame=e.ContainsKey("screenshot_frame")?Object(e["screenshot_frame"]):null;
                    if(match==null||frame==null||imageFrame==null)continue;
                    double w=Convert.ToDouble(frame["w"]),h=Convert.ToDouble(frame["h"]),iw=Convert.ToDouble(imageFrame["w"]),ih=Convert.ToDouble(imageFrame["h"]);
                    if(w<=0||h<=0||iw<=0||ih<=0||Math.Abs(Convert.ToDouble(frame["x"])-match.Bounds.X)>2||Math.Abs(Convert.ToDouble(frame["y"])-match.Bounds.Y)>2)continue;
                    observation.CaptureScaleX=w/iw;observation.CaptureScaleY=h/ih;
                    observation.CaptureOriginX=Convert.ToDouble(frame["x"])-Convert.ToDouble(imageFrame["x"])*observation.CaptureScaleX;
                    observation.CaptureOriginY=Convert.ToDouble(frame["y"])-Convert.ToDouble(imageFrame["y"])*observation.CaptureScaleY;
                    observation.HasCaptureTransform=true;break;
                }
            }
            var current=BackgroundInput.Scope(window.Handle,request.ExpectedDisplayId);
            if(current.ProcessId!=window.ProcessId || current.Bounds!=window.Bounds)throw new InvalidOperationException("Window changed during observation; observe again.");
            File.WriteAllText(Path.Combine(State,observation.Id+".json"),Json.Serialize(observation));
            return new {ok=true,backend="cua-driver",observationId=observation.Id,expiresInSeconds=120,windowHandle=window.Handle,displayId=observation.DisplayId,state=result,screenshotPath=observation.ScreenshotPath,timing=new {driverMs=driverMs,corroborationMs=clock.ElapsedMilliseconds-driverMs,totalMs=clock.ElapsedMilliseconds},image=new {width=observation.ImageWidth,height=observation.ImageHeight,windowWidth=observation.Bounds.Width,windowHeight=observation.Bounds.Height,coordinates="capture pixels; capture_id required"}};
        }
        static object Act(CuaRequest request) {
            Guid id;if(!Guid.TryParseExact(request.ObservationId,"N",out id))throw new InvalidOperationException("Provide ObservationId from CuaObserve.");
            string path=Path.Combine(State,id.ToString("N")+".json");
            if(!File.Exists(path))throw new InvalidOperationException("Observation missing or consumed; observe again.");
            var observation=Json.Deserialize<CuaObservation>(File.ReadAllText(path));File.Delete(path);
            if(observation.ExpiresUtcTicks<DateTime.UtcNow.Ticks || observation.DisplayId!=request.ExpectedDisplayId || observation.WindowHandle!=request.WindowHandle)throw new InvalidOperationException("Expired or mismatched observation.");
            var window=BackgroundInput.Scope(request.WindowHandle,request.ExpectedDisplayId);
            if(window.ProcessId!=observation.ProcessId || Process.GetProcessById((int)window.ProcessId).StartTime.ToUniversalTime().Ticks!=observation.ProcessStartTicks || window.Bounds!=observation.Bounds)throw new InvalidOperationException("Window process/geometry changed; observe again.");
            if(request.Action=="SideCursorAct") {
                var p=request.Arguments??new Dictionary<string,object>();
                if(p.Keys.Any(k=>!new[]{"x","y","button"}.Contains(k)) || !p.ContainsKey("x") || !p.ContainsKey("y") || (request.Tool!="move" && request.Tool!="click"))throw new InvalidOperationException("Invalid scoped pointer request.");
                double px=Convert.ToDouble(p["x"]),py=Convert.ToDouble(p["y"]);
                var point=SideCursor.ResolvePoint(observation,window,px,py);
                if(request.Tool=="click" && (Text(p,"button")??"left")=="left") {
                    var hits=observation.Elements.Where(e=> {
                        var f=e.ContainsKey("screenshot_frame")?Object(e["screenshot_frame"]):null;
                        string role=Text(e,"role");
                        return f!=null && (role=="Button"||role=="CheckBox"||role=="RadioButton"||role=="ListItem") && Convert.ToBoolean(e["enabled"]) && px>=Convert.ToDouble(f["x"]) && py>=Convert.ToDouble(f["y"]) && px<Convert.ToDouble(f["x"])+Convert.ToDouble(f["w"]) && py<Convert.ToDouble(f["y"])+Convert.ToDouble(f["h"]);
                    }).ToArray();
                    if(hits.Length==1) {
                        SideCursor.Mark(observation,point.X,point.Y);
                        request.Tool="click";request.Arguments=new Dictionary<string,object>{{"element_token",Text(hits[0],"element_token")}};
                    } else return SideCursor.Dispatch(observation,window,px,py,true,Text(p,"button")??"left");
                }else return SideCursor.Dispatch(observation,window,px,py,request.Tool=="click",Text(p,"button")??"left");
            }
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
            if(request.Action=="Scope") {
                var w=BackgroundInput.Scope(request.WindowHandle,request.ExpectedDisplayId);
                return new {ok=true,windowHandle=w.Handle,processId=w.ProcessId,processStartTicks=Process.GetProcessById((int)w.ProcessId).StartTime.ToUniversalTime().Ticks.ToString(),bounds=w.Bounds,displayId=request.ExpectedDisplayId};
            }
            if(request.Action=="FocusBegin") {
                string id=Guid.NewGuid().ToString("N");guards.Add(id,new InputGuard(0));return new {ok=true,guardId=id};
            }
            if(request.Action=="FocusEnd") {
                InputGuard guard;if(!guards.TryGetValue(request.ObservationId??"",out guard))throw new InvalidOperationException("Focus guard missing.");
                guards.Remove(request.ObservationId);var receipt=guard.Finish();return new {ok=receipt.Preserved,stop=!receipt.Preserved,focus=receipt};
            }
            if(request.Action=="Status") {
                var displays=DisplayAudit.Read();var virtualDisplays=displays.Where(d=>d.IsVirtualMonitor).ToArray();
                bool safeLayout=virtualDisplays.Length==1 && !displays.Any(d=>!d.IsVirtualMonitor && new Rectangle(d.X,d.Y,(int)d.Width,(int)d.Height).IntersectsWith(new Rectangle(virtualDisplays[0].X,virtualDisplays[0].Y,(int)virtualDisplays[0].Width,(int)virtualDisplays[0].Height)));
                object screen=null;
                if(virtualDisplays.Length==1){var d=virtualDisplays[0];screen=new {id=Layout.Id(d),deviceName=d.GdiName,x=d.X,y=d.Y,width=d.Width,height=d.Height};}
                bool ready=false;string healthError=null;
                if(File.Exists(Binary)){try{var probe=Driver("list_windows",new Dictionary<string,object>{{"pid",Process.GetCurrentProcess().Id}},5000);ready=!Failed(probe);if(!ready)healthError="CUA refused readiness probe";}catch(Exception e){healthError=e.Message;}}
                return new {ok=true,available=safeLayout,nonOverlapping=safeLayout,agentScreen=screen,installed=File.Exists(Binary),ready=ready,healthError=healthError,binary=Binary,socket=Socket,inputIsolation=false,deliveryMode="background",tools=new[]{"click","set_value","type_text","scroll","press_key","hotkey"},version="0.5.0"};
            }
            if(request.Action=="Windows")return new {ok=true,windows=Windows.ListOnAgentDisplay()};
            if(request.Action=="CuaObserve")return Observe(request);
            if(request.Action=="CuaAct")return Act(request);
            if(request.Action=="SideCursorAct")return Act(request);
            throw new InvalidOperationException("Unknown SideScreen CUA action.");
        }
        static object ExecuteLocked(CuaRequest request) {
            using(var mutex=new Mutex(false,"Local\\SideScreen.BackgroundInput")) {
                bool held=false;
                try {
                    try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}
                    if(!held)throw new InvalidOperationException("Another SideScreen action is running.");
                    try{return Execute(request);}finally {
                        Guid id;
                        if(request!=null && (request.Action=="CuaAct" || request.Action=="SideCursorAct") && Guid.TryParseExact(request.ObservationId,"N",out id)) {
                            try{Driver("end_session",new Dictionary<string,object>{{"session","sidescreen-"+id.ToString("N")}},1500);}catch{}
                        }
                    }
                }finally{if(held)mutex.ReleaseMutex();}
            }
        }
        [MTAThread] public static int Main(string[] arguments) {
            Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
            SetThreadDpiAwarenessContext(new IntPtr(-4));
            resident=arguments.Contains("--server");
            try {
                if(arguments.Contains("--server")) {
                    string line;
                    while((line=Console.ReadLine())!=null) {
                        try{Console.WriteLine(Json.Serialize(ExecuteLocked(Json.Deserialize<CuaRequest>(line))));}
                        catch(Exception ex){Console.WriteLine(Json.Serialize(new {ok=false,stop=true,error=ex.Message}));}
                    }
                    return 0;
                }
                string output=Json.Serialize(ExecuteLocked(Json.Deserialize<CuaRequest>(Console.In.ReadToEnd())));Console.WriteLine(output);
                return Convert.ToBoolean(Json.Deserialize<Dictionary<string,object>>(output)["ok"])?0:1;
            }catch(Exception ex){Console.WriteLine(Json.Serialize(new {ok=false,stop=true,error=ex.Message}));return 1;}finally{foreach(var guard in guards.Values)guard.Dispose();CloseDriver();}
        }
    }
}
