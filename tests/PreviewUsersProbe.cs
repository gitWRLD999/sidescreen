using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
class PreviewUsersProbe {
    static int Main(string[] args) {
        try {
            var display=SideScreen.Layout.Select(DisplayAudit.Read(),true);var area=new Rectangle(display.X,display.Y,(int)display.Width,(int)display.Height);
            var method=Assembly.LoadFrom(args[0]).GetType("VirtualScreenViewer").GetMethod("DrawAgentCursor",BindingFlags.NonPublic|BindingFlags.Static);
            using(var bitmap=new Bitmap(area.Width,area.Height)) {
                using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Black);method.Invoke(null,new object[]{g,area});}
                for(int n=1;n<args.Length;n++) {
                    Guid id;if(!Guid.TryParseExact(args[n],"N",out id))throw new Exception("Invalid test cursor ID");
                    string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AgentTools","SideScreen","state","agent-cursors");
                    var marker=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(directory,args[n]+".json")));
                    int x=Convert.ToInt32(marker["x"])-area.X,y=Convert.ToInt32(marker["y"])-area.Y,count=0;
                    for(int a=Math.Max(0,x-14);a<Math.Min(bitmap.Width,x+15);a++)for(int b=Math.Max(0,y-14);b<Math.Min(bitmap.Height,y+15);b++){var p=bitmap.GetPixel(a,b);if(p.B>180&&p.G>100&&p.R<100)count++;}
                    if(count<20||String.IsNullOrEmpty(Convert.ToString(marker["label"])))throw new Exception("A session cursor was not rendered by the production preview");
                }
            }
            Console.WriteLine("{\"ok\":true,\"cursors\":"+(args.Length-1)+"}");return 0;
        }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
    }
}
