namespace Qlipq.Host;

/// <summary>
/// qlipq's data directory and one-time migration, mirroring the Rust host: config and
/// edits live in a dotfolder under the user's home (<c>~/.com.qcksys.qlipq</c>), and a
/// one-time copy brings settings over from the old Roaming AppData location.
/// </summary>
public sealed class AppPaths
{
    /// <summary><c>~/.com.qcksys.qlipq</c> — keep this exact location for config/edits continuity.</summary>
    public string DataDir { get; }

    public string ConfigPath => Path.Combine(DataDir, "config.json");

    public AppPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        DataDir = Path.Combine(home, ".com.qcksys.qlipq");
    }

    /// <summary>One-time copy of config.json + edits.json from the old Roaming AppData location.</summary>
    public void MigrateLegacyData()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var oldDir = Path.Combine(roaming, "com.qcksys.qlipq");
        if (string.Equals(oldDir, DataDir, StringComparison.OrdinalIgnoreCase)) return;

        foreach (var name in (string[])["config.json", "edits.json"])
        {
            var newPath = Path.Combine(DataDir, name);
            var oldPath = Path.Combine(oldDir, name);
            if (!File.Exists(newPath) && File.Exists(oldPath))
            {
                try
                {
                    Directory.CreateDirectory(DataDir);
                    File.Copy(oldPath, newPath);
                }
                catch (IOException) { /* best-effort, matches Rust */ }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
