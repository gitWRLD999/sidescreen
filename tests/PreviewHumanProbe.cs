using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
class PreviewHumanProbe {
    [StructLayout(LayoutKind.Sequential)] struct CursorInfo {public int Size,Flags;public IntPtr Cursor;public Point Position;}
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    static int Main(string[] args) {
        try {
            SetThreadDpiAwarenessContext(new IntPtr(-4));var d=SideScreen.Layout.Select(DisplayAudit.Read(),true);var area=new Rectangle(d.X,d.Y,(int)d.Width,(int)d.Height);
            var cursor=new CursorInfo{Size=Marshal.SizeOf(typeof(CursorInfo))};if(!GetCursorInfo(ref cursor))throw new Exception("Cursor state unavailable");
            if((cursor.Flags&1)==0||!area.Contains(cursor.Position)){Console.WriteLine("{\"ok\":true,\"verified\":false,\"reason\":\"Physical cursor is not visible on SideScreen; no input was sent\"}");return 0;}
            var method=Assembly.LoadFrom(args[0]).GetType("VirtualScreenViewer").GetMethod("DrawHumanCursor",BindingFlags.NonPublic|BindingFlags.Static);int pixels=0;
            using(var bitmap=new Bitmap(area.Width,area.Height)) {
                using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Black);method.Invoke(null,new object[]{g,area});}
                for(int x=0;x<bitmap.Width;x++)for(int y=0;y<bitmap.Height;y++){var c=bitmap.GetPixel(x,y);if(c.R+c.G+c.B>30)pixels++;}
            }
            Console.WriteLine("{\"ok\":"+(pixels>5?"true":"false")+",\"verified\":true,\"humanCursorPixels\":"+pixels+"}");return pixels>5?0:1;
        }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
    }
}
