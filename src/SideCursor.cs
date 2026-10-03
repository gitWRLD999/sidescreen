using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace SideScreen {
    // An independent software pointer, not a virtual HID or input-session driver.
    public static class SideCursor {
        [StructLayout(LayoutKind.Sequential)] struct Point {public int X,Y;}
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h,ref Point p);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h,out Rect r);
        [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
        [DllImport("user32.dll")] static extern IntPtr ChildWindowFromPointEx(IntPtr h,Point p,uint flags);
        [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsChild(IntPtr parent,IntPtr child);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
        static void Send(IntPtr h,uint m,int button,Point p) {
            IntPtr result;
            if(SendMessageTimeout(h,m,new IntPtr(button),new IntPtr((p.Y<<16)|(p.X&0xffff)),2,1000,out result)==IntPtr.Zero)
                throw new InvalidOperationException("Pointer message timed out/refused; outcome unknown. Reobserve, never replay automatically.");
        }
        public static System.Drawing.Point ResolvePoint(CuaObservation observation,WindowRecord window,double x,double y) {
            if(observation.ScreenshotPath==null || observation.ImageWidth<1 || observation.ImageHeight<1 ||
                Double.IsNaN(x)||Double.IsInfinity(x)||Double.IsNaN(y)||Double.IsInfinity(y)||x<0||y<0||x>=observation.ImageWidth||y>=observation.ImageHeight)
                throw new InvalidOperationException("SideCursor requires pixels from a fresh scoped screenshot.");
            if(!observation.HasCaptureTransform)throw new InvalidOperationException("Capture has no corroborated image-to-screen transform. Observe with the accessibility tree; no guessed border/DPI conversion.");
            var p=new System.Drawing.Point((int)Math.Round(observation.CaptureOriginX+x*observation.CaptureScaleX),(int)Math.Round(observation.CaptureOriginY+y*observation.CaptureScaleY));
            if(!window.Bounds.Contains(p))throw new InvalidOperationException("Pointer pixels map outside the observed window.");
            return p;
        }
        public static void Mark(CuaObservation observation,int sx,int sy) {
            var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AgentTools","SideScreen","state");
            Directory.CreateDirectory(directory);
            var marker=new JavaScriptSerializer().Serialize(new {x=sx,y=sy,displayId=observation.DisplayId,windowHandle=observation.WindowHandle,expiresUtc=DateTime.UtcNow.AddSeconds(120).ToString("o")});
            File.WriteAllText(Path.Combine(directory,"agent-cursor.json"),marker);
        }
        public static object Dispatch(CuaObservation observation,WindowRecord window,double x,double y,bool click,string button) {
            var location=ResolvePoint(observation,window,x,y);
            if(button!="left" && button!="right" && button!="middle")throw new InvalidOperationException("Invalid pointer button.");
            int sx=location.X,sy=location.Y;
            IntPtr root=new IntPtr(window.Handle),target=root;
            Point point=new Point {X=sx,Y=sy};
            for(int depth=0;depth<32;depth++) {
                point=new Point {X=sx,Y=sy};Rect rect;
                if(!ScreenToClient(target,ref point)||!GetClientRect(target,out rect)||point.X<0||point.Y<0||point.X>=rect.Right||point.Y>=rect.Bottom)
                    throw new InvalidOperationException("Only client-area pointer messages are supported; no titlebar/desktop route.");
                IntPtr child=ChildWindowFromPointEx(target,point,7);
                if(child==IntPtr.Zero||child==target)break;
                target=child;
                if(depth==31)throw new InvalidOperationException("Window child hierarchy exceeds the pointer scope bound.");
            }
            uint pid;GetWindowThreadProcessId(target,out pid);
            if(pid!=window.ProcessId || !IsWindowEnabled(target) || (target!=root&&!IsChild(root,target)))
                throw new InvalidOperationException("Pointer destination escaped the scoped process/window.");
            bool dispatched=false;string error=null;FocusReceipt focus;
            using(var guard=new InputGuard(window.Handle)) {
                try {
                    var current=BackgroundInput.Scope(window.Handle,observation.DisplayId);
                    if(current.Bounds!=window.Bounds || current.ProcessId!=window.ProcessId || !guard.Quiet)throw new InvalidOperationException("Scope/focus changed during pointer preparation.");
                    dispatched=true;Send(target,0x200,0,point);
                    if(click) {
                        uint down=button=="left"?0x201u:button=="right"?0x204u:0x207u;
                        int mask=button=="left"?1:button=="right"?2:16;
                        // Release is attempted even when down reports a timeout.
                        try{Send(target,down,mask,point);}finally{Send(target,down+1,0,point);}
                    }
                    Mark(observation,sx,sy);
                }catch(Exception e){error=e.Message;}
                focus=guard.Finish();
            }
            return new {ok=error==null&&focus.Preserved,stop=error!=null||!focus.Preserved,dispatched=dispatched,backend="sidecursor-window-messages",effect="message-delivery-only; verify fresh application state",universalIsolation=false,focus=focus,error=error,screenX=sx,screenY=sy};
        }
    }
}
