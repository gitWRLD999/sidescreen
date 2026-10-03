using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using SideScreen;

// A separate activation-only build. It cannot deliver keyboard/pointer frames.
class ChromeFocus {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=8)]
    struct Frame {
        public uint Version,Operation;
        public ulong Root,Target;
        public int X,Y;
        public uint Message,Reserved;
        public ulong WParam;
        public long LParam;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public byte[] Keys;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=96)] public string Mapping;
    }
    [DllImport("SideScreen.ChromeFocus.dll",CallingConvention=CallingConvention.StdCall)] static extern int SideInputDispatch(ref Frame frame,out int blocked);
    [DllImport("SideScreen.ChromeFocus.dll",CallingConvention=CallingConvention.StdCall)] static extern void SideInputClose();
    [DllImport("kernel32.dll")] static extern bool IsWow64Process(IntPtr process,out bool wow);
    class Request {public long WindowHandle;public string ExpectedDisplayId,ExpectedProcessStartTicks;public uint ExpectedProcessId;public bool Release;}
    static int Main() {
        Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
        var json=new JavaScriptSerializer();
        try {
            var r=json.Deserialize<Request>(Console.In.ReadToEnd());
            var w=BackgroundInput.Scope(r.WindowHandle,r.ExpectedDisplayId);
            using(var p=Process.GetProcessById((int)w.ProcessId)) {
                bool wow;
                if(w.ProcessId!=r.ExpectedProcessId||p.StartTime.ToUniversalTime().Ticks.ToString()!=r.ExpectedProcessStartTicks||p.ProcessName!="chrome")throw new InvalidOperationException("Chrome focus identity changed.");
                if(!IsWow64Process(p.Handle,out wow)||wow)throw new InvalidOperationException("Chrome focus guard currently requires 64-bit Chrome.");
                if(!p.MainModule.FileVersionInfo.ProductName.Contains("Chrome"))throw new InvalidOperationException("Target is not a corroborated Chrome executable.");
                using(var guard=new InputGuard(0)) {
                    var frame=new Frame {Version=1,Operation=r.Release?2u:3u,Root=(ulong)w.Handle,Target=(ulong)w.Handle,Keys=new byte[256],Mapping=""};int blocked;
                    int status=SideInputDispatch(ref frame,out blocked);var focus=guard.Finish();
                    var after=BackgroundInput.Scope(w.Handle,r.ExpectedDisplayId);
                    bool ok=status==1&&focus.Preserved&&after.ProcessId==w.ProcessId&&after.Bounds==w.Bounds;
                    Console.WriteLine(json.Serialize(new {ok=ok,stop=!ok,status=status,focus=focus,route="chrome-window-activation-guard",expiresInSeconds=r.Release?0:120,inputInjection=false}));return ok?0:1;
                }
            }
        }catch(Exception e){Console.WriteLine(json.Serialize(new {ok=false,stop=true,error=e.Message}));return 1;}
        finally{try{SideInputClose();}catch{}}
    }
}
