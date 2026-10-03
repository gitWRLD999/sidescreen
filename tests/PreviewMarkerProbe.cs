using System;
using System.Drawing;
using System.Reflection;
class PreviewMarkerProbe {
    static int Main(string[] args) {
        try {
            var display=SideScreen.Layout.Select(DisplayAudit.Read(),true);
            var area=new Rectangle(display.X,display.Y,(int)display.Width,(int)display.Height);
            var method=Assembly.LoadFrom(args[0]).GetType("VirtualScreenViewer").GetMethod("DrawAgentCursor",BindingFlags.NonPublic|BindingFlags.Static);
            using(var image=new Bitmap(area.Width,area.Height)) {
                using(var g=Graphics.FromImage(image)){g.Clear(Color.Black);method.Invoke(null,new object[]{g,area});}
                int colored=0;
                for(int x=0;x<image.Width;x++)for(int y=0;y<image.Height;y++){var p=image.GetPixel(x,y);if(p.B>180&&p.G>100&&p.R<100)colored++;}
                Console.WriteLine("{\"ok\":"+(colored>20?"true":"false")+",\"agentMarkerPixels\":"+colored+"}");
                return colored>20?0:1;
            }
        }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
    }
}
