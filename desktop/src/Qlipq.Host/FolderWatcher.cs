namespace Qlipq.Host;

/// <summary>
/// Watches capture folders (recursively) for newly created video files and raises
/// <see cref="FileAdded"/> with the absolute path. One <see cref="FileSystemWatcher"/> per folder;
/// the instance is held for the app's lifetime (the Rust <c>WatcherState</c> Mutex equivalent).
/// </summary>
public sealed class FolderWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];

    public event Action<string>? FileAdded;

    public void Start(IEnumerable<string> folders, IReadOnlyList<string> extensions)
    {
        Stop();
        var exts = extensions.Select(e => e.ToLowerInvariant()).ToList();

        foreach (var folder in folders)
        {
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                };
                watcher.Created += (_, e) =>
                {
                    if (File.Exists(e.FullPath) && HasExt(e.FullPath, exts)) FileAdded?.Invoke(e.FullPath);
                };
                _watchers.Add(watcher);
            }
            catch (Exception e) when (e is ArgumentException or FileNotFoundException or DirectoryNotFoundException)
            {
                // Ignore individual folder failures (e.g. a missing path), matching the Rust host.
            }
        }
    }

    public void Stop()
    {
        foreach (var w in _watchers)
        {
            try { w.Dispose(); } catch (ObjectDisposedException) { }
        }
        _watchers.Clear();
    }

    public void Dispose() => Stop();

    private static bool HasExt(string path, List<string> exts)
    {
        var ext = Path.GetExtension(path);
        if (ext.Length == 0) return false;
        return exts.Contains(ext[1..].ToLowerInvariant());
    }
}
