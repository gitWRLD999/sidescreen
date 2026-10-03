using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace SideScreen {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=8)]
    public struct NativeFrame {
        public uint Version,Operation;
        public ulong Root,Target;
        public int X,Y;
        public uint Message,Reserved;
        public ulong WParam;
        public long LParam;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=256)] public byte[] Keys;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=96)] public string Mapping;
    }
    public static class NativeWire {
#if NATIVE_HOST
        const string Library="SideScreen.VirtualInput32.dll";
#else
        const string Library="SideScreen.VirtualInput.dll";
#endif
#if NATIVE_HOST
        [DllImport(Library,EntryPoint="SideInputDispatch@8",CallingConvention=CallingConvention.StdCall,ExactSpelling=true)] static extern int SideInputDispatch(ref NativeFrame frame,out int blocked);
        [DllImport(Library,EntryPoint="SideInputClose@0",CallingConvention=CallingConvention.StdCall,ExactSpelling=true)] static extern void SideInputClose();
#else
        [DllImport(Library,CallingConvention=CallingConvention.StdCall)] static extern int SideInputDispatch(ref NativeFrame frame,out int blocked);
        [DllImport(Library,CallingConvention=CallingConvention.StdCall)] static extern void SideInputClose();
#endif
        public static int Send(ref NativeFrame frame,out int blocked) {
            if(frame.Keys==null||frame.Keys.Length!=256)throw new InvalidOperationException("A complete virtual keyboard state is required.");
            frame.Mapping="";return SideInputDispatch(ref frame,out blocked);
        }
        public static void Close(){SideInputClose();}
#if NATIVE_HOST
        static int Main() {
            var json=new JavaScriptSerializer {MaxJsonLength=1048576};
            try {
                string line;while((line=Console.ReadLine())!=null) {
                    try {var frame=json.Deserialize<NativeFrame>(line);int blocked;int status=Send(ref frame,out blocked);Console.WriteLine(json.Serialize(new {status=status,blocked=blocked}));}
                    catch(Exception ex){Console.WriteLine(json.Serialize(new {status=-9999,error=ex.Message}));}
                }
                return 0;
            }finally{try{Close();}catch{}}
        }
#endif
    }
}
