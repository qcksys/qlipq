namespace Qlipq.App.ViewModels;

/// <summary>Forward-slash path helpers matching the web app's queue.ts (basename/dirname/join).</summary>
public static class AppPath
{
    public static string BaseName(string path)
    {
        var n = path.Replace('\\', '/');
        return n[(n.LastIndexOf('/') + 1)..];
    }

    public static string DirName(string path)
    {
        var n = path.Replace('\\', '/');
        var idx = n.LastIndexOf('/');
        return idx <= 0 ? "" : path[..idx];
    }

    public static string Join(string dir, string name) =>
        string.IsNullOrEmpty(dir) ? name : $"{dir.TrimEnd('/', '\\')}/{name}";
}
