using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using Qlipq.Core;

namespace Qlipq.Host;

/// <summary>Detected capture-app recording folders, offered as one-click watch-folder presets.</summary>
public sealed record CapturePresets
{
    /// <summary>OBS recording folder, from its profile's <c>basic.ini</c>.</summary>
    public string? Obs { get; set; }

    /// <summary>NVIDIA Share (ShadowPlay) recording folder, from the registry.</summary>
    public string? NvidiaShare { get; set; }
}

/// <summary>
/// Reads OBS config files and the NVIDIA Share recording folder, mirroring the Rust host. Parsing
/// of the OBS files themselves lives in <see cref="Detect"/> (pure, in Qlipq.Core).
/// </summary>
public sealed class CaptureDetect
{
    /// <summary>Raw OBS <c>user.ini</c> + each profile's <c>basic.ini</c> from <c>%APPDATA%/obs-studio</c>.</summary>
    public ObsConfigFiles ReadObsConfig()
    {
        var files = new ObsConfigFiles();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appData, "obs-studio");

        TryRead(Path.Combine(baseDir, "user.ini"), text => files.UserIni = text);

        var profilesDir = Path.Combine(baseDir, "basic", "profiles");
        try
        {
            if (Directory.Exists(profilesDir))
            {
                foreach (var dir in Directory.EnumerateDirectories(profilesDir))
                {
                    var name = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(name)) continue;
                    TryRead(Path.Combine(dir, "basic.ini"), text => files.Profiles[name] = text);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

        return files;

        static void TryRead(string path, Action<string> use)
        {
            try { if (File.Exists(path)) use(File.ReadAllText(path)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// The folder NVIDIA Share (ShadowPlay) records into, stored only as a REG_BINARY UTF-16LE
    /// string at <c>HKCU\Software\NVIDIA Corporation\Global\ShadowPlay\NVSPCAPS\DefaultPathW</c>.
    /// Returns null off Windows or when not present.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public string? DetectNvidiaRecordingDir()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\NVIDIA Corporation\Global\ShadowPlay\NVSPCAPS");
            if (key?.GetValue("DefaultPathW") is byte[] raw)
            {
                var decoded = Encoding.Unicode.GetString(raw).TrimEnd('\0').Trim();
                return decoded.Length > 0 ? decoded : null;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return null;
    }

    /// <summary>Detect OBS and NVIDIA Share folders; each source is probed independently.</summary>
    public CapturePresets DetectCapturePresets()
    {
        var presets = new CapturePresets();
        try
        {
            var obs = Detect.DetectObsRecordingFolder(ReadObsConfig());
            if (obs is not null) presets.Obs = PathUtil.ToPosix(obs);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var nvidia = DetectNvidiaRecordingDir();
                if (nvidia is not null) presets.NvidiaShare = PathUtil.ToPosix(nvidia);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }

        return presets;
    }
}
