using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SideScreen {
    public static class Layout {
        public static string Id(DisplayAudit.Record display) {
            return display.GdiName+"|"+display.MonitorHardwareId;
        }
        public static bool BelongsTo(Rectangle window, Rectangle display) {
            if(window.Width<=0 || window.Height<=0) return false;
            var overlap=Rectangle.Intersect(window,display);
            return display.Contains(window.Left+window.Width/2,window.Top+window.Height/2) ||
                (long)overlap.Width*overlap.Height>(long)window.Width*window.Height/2;
        }
        public static Rectangle Fit(Rectangle window, Rectangle work) {
            if(work.Width<64 || work.Height<64) throw new ArgumentException("Display has no usable area");
            int width=Math.Min(Math.Max(window.Width,64),work.Width-32);
            int height=Math.Min(Math.Max(window.Height,64),work.Height-32);
            return new Rectangle(work.Left+(work.Width-width)/2,work.Top+(work.Height-height)/2,width,height);
        }
        public static DisplayAudit.Record Select(DisplayAudit.Record[] displays,bool agent) {
            var eligible=displays.Where(p=>agent?p.IsVirtualMonitor:p.IsUsableMainDisplay).ToArray();
            if(eligible.Length!=1) throw new InvalidOperationException(agent?"Exactly one MTT virtual display must be active.":"Exactly one physical display must be active for this command. Use Windows Display Settings to resolve ambiguity.");
            return eligible[0];
        }
    }
    public sealed class WindowRecord { public long Handle; public string Title; public uint ProcessId; public Rectangle Bounds; }
    public sealed class MoveReceipt { public long Handle; public Rectangle Bounds; public bool FocusPreserved, CursorPreserved; }
    public static class Windows {
        [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left,Top,Right,Bottom; public Rectangle Bounds {get{return Rectangle.FromLTRB(Left,Top,Right,Bottom);}} }
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; }
        delegate bool EnumCallback(IntPtr h,IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback,IntPtr data);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder s,int count);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder s,int count);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out int value,int size);
        sealed class Pixels:IDisposable {
            IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
            public void Dispose(){if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
        }
        public static WindowRecord[] List() {
            var items=new List<WindowRecord>();
            using(new Pixels()) EnumWindows(delegate(IntPtr h,IntPtr unused){
                if(!IsWindowVisible(h))return true;
                var windowClass=new StringBuilder(256);GetClassName(h,windowClass,windowClass.Capacity);
                if(windowClass.ToString()=="Progman" || windowClass.ToString()=="WorkerW" ||
                   windowClass.ToString()=="Shell_TrayWnd" || windowClass.ToString()=="Shell_SecondaryTrayWnd")return true;
                int cloaked;if(DwmGetWindowAttribute(h,14,out cloaked,4)==0 && cloaked!=0)return true;
                var title=new StringBuilder(1024);GetWindowText(h,title,title.Capacity);
                if(title.Length==0)return true;
                Rect r;uint pid;GetWindowThreadProcessId(h,out pid);
                if(GetWindowRect(h,out r))items.Add(new WindowRecord{Handle=h.ToInt64(),Title=title.ToString(),ProcessId=pid,Bounds=r.Bounds});
                return true;
            },IntPtr.Zero);
            return items.ToArray();
        }
        public static WindowRecord[] ListOnAgentDisplay() {
            var display=Layout.Select(DisplayAudit.Read(),true);
            var area=new Rectangle(display.X,display.Y,(int)display.Width,(int)display.Height);
            return List().Where(w=>Layout.BelongsTo(w.Bounds,area)).ToArray();
        }
        public static MoveReceipt Move(long handle,bool toAgent,string expectedDisplayId) {
            var display=Layout.Select(DisplayAudit.Read(),toAgent);
            if(Layout.Id(display)!=expectedDisplayId)throw new InvalidOperationException("Display identity changed. Query Status and verify the target before moving a window.");
            return MoveWithin(handle,new Rectangle(display.X,display.Y,(int)display.Width,(int)display.Height));
        }
        public static MoveReceipt MoveWithin(long handle,Rectangle area) {
            IntPtr h=new IntPtr(handle);
            if(!IsWindow(h)||!IsWindowVisible(h))throw new ArgumentException("Window is gone or hidden; enumerate windows again.");
            if(!List().Any(w=>w.Handle==handle))throw new InvalidOperationException("Window is not a listed normal window; enumerate windows again.");
            if(IsIconic(h)||IsZoomed(h))throw new InvalidOperationException("Restore this window first; SideScreen will not activate it to restore it.");
            using(new Pixels()) {
                Rect before; if(!GetWindowRect(h,out before))throw new Win32Exception();
                Rectangle fit=Layout.Fit(before.Bounds,area);
                IntPtr foreground=GetForegroundWindow();Point cursorBefore,cursorAfter;
                if(!GetCursorPos(out cursorBefore))throw new Win32Exception();
                // SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER
                if(!SetWindowPos(h,IntPtr.Zero,fit.X,fit.Y,fit.Width,fit.Height,0x0214))throw new Win32Exception(Marshal.GetLastWin32Error());
                Rect after;if(!GetWindowRect(h,out after))throw new Win32Exception();
                bool gotCursor=GetCursorPos(out cursorAfter);
                return new MoveReceipt{Handle=handle,Bounds=after.Bounds,FocusPreserved=foreground==GetForegroundWindow(),CursorPreserved=gotCursor&&cursorBefore.X==cursorAfter.X&&cursorBefore.Y==cursorAfter.Y};
            }
        }
        public static void Capture(string output,string expectedDisplayId) {
            var display=Layout.Select(DisplayAudit.Read(),true);
            if(Layout.Id(display)!=expectedDisplayId)throw new InvalidOperationException("Display identity changed. Query Status and verify the target before capture.");
            if(display.Width==0||display.Height==0)throw new InvalidOperationException("Display has no capture area.");
            using(new Pixels())
            using(var image=new Bitmap((int)display.Width,(int)display.Height)) {
                using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(display.X,display.Y,0,0,image.Size,CopyPixelOperation.SourceCopy);
                image.Save(output,ImageFormat.Png);
            }
        }
    }
}
