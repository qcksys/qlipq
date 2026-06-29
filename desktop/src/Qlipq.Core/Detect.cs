using System.Text.RegularExpressions;

namespace Qlipq.Core;

/// <summary>Raw OBS config files, read by the host and parsed here (no I/O in this module).</summary>
public sealed record ObsConfigFiles
{
    /// <summary>Contents of <c>obs-studio/user.ini</c>, or null if absent.</summary>
    public string? UserIni { get; set; }

    /// <summary>Map of profile directory name → contents of its <c>basic.ini</c>.</summary>
    public Dictionary<string, string> Profiles { get; set; } = [];
}

public static partial class Detect
{
    [GeneratedRegex(@"\r?\n")]
    private static partial Regex LineSplitRegex();

    [GeneratedRegex(@"^\[(.+)\]$")]
    private static partial Regex SectionHeaderRegex();

    /// <summary>
    /// Read a single <c>key=value</c> from an INI section. Section and key match
    /// case-insensitively; tolerant of a UTF-8 BOM and CRLF line endings, both of
    /// which OBS writes.
    /// </summary>
    private static string? GetIniValue(string text, string section, string key)
    {
        string? current = null;
        foreach (var raw in LineSplitRegex().Split(text))
        {
            var line = (raw.StartsWith('﻿') ? raw[1..] : raw).Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            var header = SectionHeaderRegex().Match(line);
            if (header.Success)
            {
                current = header.Groups[1].Value.ToLowerInvariant();
                continue;
            }
            if (current != section.ToLowerInvariant()) continue;
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            if (line[..eq].Trim().ToLowerInvariant() == key.ToLowerInvariant())
            {
                return line[(eq + 1)..].Trim();
            }
        }
        return null;
    }

    /// <summary>
    /// Resolve the folder OBS records into, from its config files.
    /// Picks the active profile (<c>user.ini</c> <c>[Basic] ProfileDir</c>, falling back to
    /// <c>Profile</c>, then the sole/first profile present), then reads that profile's
    /// <c>basic.ini</c>: <c>[Output] Mode = Advanced</c> uses <c>[AdvOut] RecFilePath</c>,
    /// otherwise <c>[SimpleOutput] FilePath</c>. Returns <c>null</c> when nothing usable is found.
    /// </summary>
    public static string? DetectObsRecordingFolder(ObsConfigFiles files)
    {
        var profileNames = files.Profiles.Keys.ToList();
        if (profileNames.Count == 0) return null;

        string? active = null;
        if (!string.IsNullOrEmpty(files.UserIni))
        {
            active = GetIniValue(files.UserIni, "Basic", "ProfileDir");
            if (string.IsNullOrEmpty(active)) active = GetIniValue(files.UserIni, "Basic", "Profile");
        }

        string? basicIni = null;
        if (!string.IsNullOrEmpty(active))
        {
            basicIni = files.Profiles.TryGetValue(active, out var exact)
                ? exact
                : MatchProfileCaseInsensitive(files.Profiles, active);
        }
        basicIni ??= files.Profiles[profileNames[0]];

        if (basicIni is null) return null;

        var mode = GetIniValue(basicIni, "Output", "Mode");
        var folder = string.Equals(mode, "advanced", StringComparison.OrdinalIgnoreCase)
            ? GetIniValue(basicIni, "AdvOut", "RecFilePath")
            : GetIniValue(basicIni, "SimpleOutput", "FilePath");

        var trimmed = folder?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? MatchProfileCaseInsensitive(Dictionary<string, string> profiles, string name)
    {
        foreach (var (key, value) in profiles)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) return value;
        }
        return null;
    }
}
