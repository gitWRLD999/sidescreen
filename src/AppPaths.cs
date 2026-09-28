using System;
using System.IO;
internal static class AppPaths {
    internal static string State {
        get {
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SideScreen");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
