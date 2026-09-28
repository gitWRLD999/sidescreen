using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class DisplayAudit {
    [StructLayout(LayoutKind.Sequential)] public struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] public struct Rational { public uint Num, Den; }
    [StructLayout(LayoutKind.Sequential)] public struct Source { public Luid Adapter; public uint Id, Mode, Status; }
    [StructLayout(LayoutKind.Sequential)] public struct Target {
        public Luid Adapter; public uint Id, Mode, Technology, Rotation, Scaling;
        public Rational Refresh; public uint Scan; public int Available; public uint Status;
    }
    [StructLayout(LayoutKind.Sequential)] public struct Path { public Source Source; public Target Target; public uint Flags; }
    [StructLayout(LayoutKind.Explicit, Size=64)] public struct Mode {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(4)] public uint Id;
        [FieldOffset(8)] public Luid Adapter;
        [FieldOffset(16)] public uint Width;
        [FieldOffset(20)] public uint Height;
        [FieldOffset(28)] public int X;
        [FieldOffset(32)] public int Y;
        [FieldOffset(40)] public uint SignalWidth;
        [FieldOffset(44)] public uint SignalHeight;
    }
    [StructLayout(LayoutKind.Sequential)] public struct Header { public uint Type, Size; public Luid Adapter; public uint Id; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct TargetName {
        public Header Header; public uint Flags, Technology; public ushort Manufacturer, Product; public uint Connector;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string FriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string DevicePath;
    }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct SourceName {
        public Header Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string GdiName;
    }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct AdapterName {
        public Header Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string DevicePath;
    }
    [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] Path[] path, ref uint modes, [Out] Mode[] mode, IntPtr topology);
    [DllImport("user32.dll", EntryPoint="DisplayConfigGetDeviceInfo")] static extern int GetTarget(ref TargetName name);
    [DllImport("user32.dll", EntryPoint="DisplayConfigGetDeviceInfo")] static extern int GetSource(ref SourceName name);
    [DllImport("user32.dll", EntryPoint="DisplayConfigGetDeviceInfo")] static extern int GetAdapter(ref AdapterName name);
    [DllImport("user32.dll")] static extern int SetDisplayConfig(uint paths, IntPtr path, uint modes, IntPtr mode, uint flags);
    public static void Extend() {
        int err=SetDisplayConfig(0,IntPtr.Zero,0,IntPtr.Zero,0x84);
        if(err!=0) throw new Win32Exception(err,"SetDisplayConfig extend");
    }
    public class Record {
        public string GdiName, FriendlyName, DevicePath;
        public string MonitorHardwareId, IdentitySource, AdapterDevicePath;
        public uint TargetNameFlags;
        public int AdapterNameError;
        public bool IsMainDisplay, IsVirtualMonitor;
        // A forced placeholder with no monitor interface is not sufficient to
        // remove the only remote display. A real active monitor path is usable
        // even when Windows retains the forced-friendly-name flag.
        public bool IsUsableMainDisplay { get { return IsMainDisplay &&
            (!String.IsNullOrEmpty(DevicePath) || (TargetNameFlags & 2)==0); } }
        public uint Width, Height, SignalWidth, SignalHeight, RefreshNumerator, RefreshDenominator, Technology;
        public double RefreshHz;
        public int X, Y;
    }
    static string EdidHardwareId(TargetName name) {
        if((name.Flags & 4)==0) return "";
        // EDID stores the EISA manufacturer word in big-endian byte order.
        uint word=(uint)(((name.Manufacturer & 255)<<8) | (name.Manufacturer>>8));
        int a=(int)((word>>10)&31), b=(int)((word>>5)&31), c=(int)(word&31);
        if((word & 0x8000)!=0 || a<1 || a>26 || b<1 || b>26 || c<1 || c>26) return "";
        return new string(new char[] { (char)('A'+a-1), (char)('A'+b-1), (char)('A'+c-1) }) + name.Product.ToString("X4");
    }
    static bool Contains(string value, string part) {
        return !String.IsNullOrEmpty(value) && value.IndexOf(part,StringComparison.OrdinalIgnoreCase)>=0;
    }
    static void Identify(Record record, TargetName name) {
        record.MonitorHardwareId=EdidHardwareId(name);
        record.IdentitySource="Unknown";
        record.IsVirtualMonitor=Contains(record.DevicePath,"#MTT1337#") ||
            (record.MonitorHardwareId=="MTT1337" && Contains(record.AdapterDevicePath,"ROOT#DISPLAY#"));
        if(record.IsVirtualMonitor) record.IdentitySource="MTT virtual monitor";
        // Require a real active monitor interface and a conventional physical
        // connector before offering it as a recovery destination.
        uint tech=record.Technology;
        record.IsMainDisplay=!record.IsVirtualMonitor && !String.IsNullOrEmpty(record.DevicePath) &&
            (tech==0 || tech==4 || tech==5 || tech==6 || tech==10 || tech==11 || tech==12 || tech==13 || tech==0x80000000);
        if(record.IsMainDisplay) record.IdentitySource="Active physical connector";
    }
    public static Record[] Read() {
        for (int attempt=0; attempt<5; attempt++) {
            uint pc, mc;
            int err=GetDisplayConfigBufferSizes(2,out pc,out mc);
            if(err!=0) throw new Win32Exception(err,"GetDisplayConfigBufferSizes");
            Path[] paths=new Path[pc]; Mode[] modes=new Mode[mc];
            err=QueryDisplayConfig(2,ref pc,paths,ref mc,modes,IntPtr.Zero);
            if(err==122) continue;
            if(err!=0) throw new Win32Exception(err,"QueryDisplayConfig");
            var result=new List<Record>();
            for(int i=0;i<pc;i++) {
                var p=paths[i];
                var tn=new TargetName(); tn.Header.Type=2; tn.Header.Size=(uint)Marshal.SizeOf(typeof(TargetName)); tn.Header.Adapter=p.Target.Adapter; tn.Header.Id=p.Target.Id;
                err=GetTarget(ref tn); if(err!=0) throw new Win32Exception(err,"GetTargetName");
                var sn=new SourceName(); sn.Header.Type=1; sn.Header.Size=(uint)Marshal.SizeOf(typeof(SourceName)); sn.Header.Adapter=p.Source.Adapter; sn.Header.Id=p.Source.Id;
                err=GetSource(ref sn); if(err!=0) throw new Win32Exception(err,"GetSourceName");
                var an=new AdapterName(); an.Header.Type=4; an.Header.Size=(uint)Marshal.SizeOf(typeof(AdapterName)); an.Header.Adapter=p.Target.Adapter;
                int adapterError=GetAdapter(ref an);
                var r=new Record { GdiName=sn.GdiName, FriendlyName=tn.FriendlyName, DevicePath=tn.DevicePath, TargetNameFlags=tn.Flags, AdapterDevicePath=adapterError==0?an.DevicePath:"", AdapterNameError=adapterError, RefreshNumerator=p.Target.Refresh.Num, RefreshDenominator=p.Target.Refresh.Den, RefreshHz=p.Target.Refresh.Den==0?0:(double)p.Target.Refresh.Num/p.Target.Refresh.Den, Technology=p.Target.Technology };
                Identify(r,tn);
                if(p.Source.Mode<mc && modes[p.Source.Mode].Type==1) { var m=modes[p.Source.Mode]; r.Width=m.Width; r.Height=m.Height; r.X=m.X; r.Y=m.Y; }
                if(p.Target.Mode<mc && modes[p.Target.Mode].Type==2) { var m=modes[p.Target.Mode]; r.SignalWidth=m.SignalWidth; r.SignalHeight=m.SignalHeight; }
                result.Add(r);
            }
            return result.ToArray();
        }
        throw new Exception("Display topology kept changing during audit.");
    }
}
