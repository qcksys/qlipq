using System.Text.Json;
using Qlipq.Host;

namespace Qlipq.App.Services;

/// <summary>
/// Remembers playback position per file (so reopening a clip resumes where you left off),
/// persisted to <c>playback.json</c> in the data dir. Replaces the web app's localStorage use.
/// </summary>
public sealed class PlaybackStore
{
    private readonly string _path;
    private Dictionary<string, double> _map = new();
    private long _lastSaveTick;

    public PlaybackStore(AppPaths paths)
    {
        _path = Path.Combine(paths.DataDir, "playback.json");
        try
        {
            if (File.Exists(_path))
                _map = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(_path)) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException) { }
    }

    public double Get(string path) => _map.TryGetValue(path, out var v) ? v : 0;

    /// <summary>Record a position; writes to disk at most every couple of seconds to avoid thrash.</summary>
    public void Set(string path, double seconds)
    {
        _map[path] = seconds;
        var now = Environment.TickCount64;
        if (now - _lastSaveTick < 2000) return;
        _lastSaveTick = now;
        Flush();
    }

    public void Flush()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_map));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
