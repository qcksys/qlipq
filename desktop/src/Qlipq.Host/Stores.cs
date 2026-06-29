using Qlipq.Core;

namespace Qlipq.Host;

/// <summary>Load/save <c>config.json</c> (lenient parse + pretty save with $schema). Mirrors get_config/set_config.</summary>
public sealed class ConfigStore(AppPaths paths)
{
    /// <summary>Absolute path to the persisted config.json (for "open config file" in the UI).</summary>
    public string ConfigFilePath => paths.ConfigPath;

    public async Task<AppConfig> LoadAsync()
    {
        try
        {
            if (!File.Exists(paths.ConfigPath)) return new AppConfig();
            return ConfigJson.Parse(await File.ReadAllTextAsync(paths.ConfigPath));
        }
        catch (IOException) { return new AppConfig(); }
        catch (UnauthorizedAccessException) { return new AppConfig(); }
    }

    public async Task SaveAsync(AppConfig config)
    {
        Directory.CreateDirectory(paths.DataDir);
        await File.WriteAllTextAsync(paths.ConfigPath, ConfigJson.Serialize(config));
    }
}

/// <summary>Read/write named files in the data dir (e.g. <c>edits.json</c>), with a path-traversal guard.</summary>
public sealed class AppDataStore(AppPaths paths)
{
    private static void Guard(string name)
    {
        if (name.Contains('/') || name.Contains('\\') || name.Contains(".."))
            throw new ArgumentException("invalid file name", nameof(name));
    }

    public async Task<string?> ReadAsync(string name)
    {
        Guard(name);
        var path = Path.Combine(paths.DataDir, name);
        if (!File.Exists(path)) return null;
        try { return await File.ReadAllTextAsync(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public async Task WriteAsync(string name, string contents)
    {
        Guard(name);
        Directory.CreateDirectory(paths.DataDir);
        await File.WriteAllTextAsync(Path.Combine(paths.DataDir, name), contents);
    }
}
