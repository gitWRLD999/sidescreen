using System;
using System.IO;
internal static class AppPaths {
    internal static string State {
        get {
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AgentTools","SideScreen","state");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
