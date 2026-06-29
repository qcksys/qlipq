using System.Text.RegularExpressions;

namespace Qlipq.Core;

/// <summary>Metadata recovered from an OBS recording/replay-buffer filename.</summary>
public sealed record ParsedRecording
{
    /// <summary>Local timestamp parsed from the filename, if present.</summary>
    public DateTime? RecordedAt { get; set; }

    /// <summary>A leading label (OBS scene/profile or game name) before the timestamp, if present.</summary>
    public string? Source { get; set; }

    /// <summary>Whether the filename looks like a replay-buffer clip ("Replay ...").</summary>
    public bool IsReplay { get; set; }
}

public static partial class Obs
{
    // Matches the date/time portion OBS writes with its default and common custom
    // formats, e.g. "2024-01-31 18-09-05", "2024-01-31_18-09-05",
    // "2024-01-31 18.09.05". Capture groups: y m d H M S. ECMAScript keeps \d ASCII
    // to match the source TypeScript regex semantics exactly.
    [GeneratedRegex(@"(\d{4})-(\d{2})-(\d{2})[ _T-](\d{2})[-.:](\d{2})[-.:](\d{2})", RegexOptions.ECMAScript)]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"\breplay\b", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)]
    private static partial Regex ReplayWordRegex();

    [GeneratedRegex(@"[_\-.]+$")]
    private static partial Regex TrailingSeparatorsRegex();

    [GeneratedRegex(@"^replay$", RegexOptions.IgnoreCase)]
    private static partial Regex OnlyReplayRegex();

    [GeneratedRegex(@"^replay[_\-. ]+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingReplayRegex();

    [GeneratedRegex(@"^replay\b", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)]
    private static partial Regex StartsWithReplayRegex();

    /// <summary>
    /// Parse an OBS recording filename into a timestamp and optional source label.
    /// OBS filenames are driven by the user's "Filename Formatting" setting; the default
    /// is <c>%CCYY-%MM-%DD %hh-%mm-%ss</c>, and the replay buffer prefixes <c>Replay </c>.
    /// Many users prepend a scene or game name; we extract whatever timestamp we can find
    /// and treat text before it as the source label.
    /// </summary>
    public static ParsedRecording ParseObsFilename(string fileName)
    {
        var baseName = StripExtension(fileName);
        var match = TimestampRegex().Match(baseName);
        var isReplay = ReplayWordRegex().IsMatch(baseName);

        if (!match.Success)
        {
            return new ParsedRecording { IsReplay = isReplay };
        }

        DateTime? recordedAt = TryBuildLocalDate(
            match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value,
            match.Groups[4].Value, match.Groups[5].Value, match.Groups[6].Value);

        var lead = baseName[..match.Index].Trim();
        var cleanedLead = TrailingSeparatorsRegex().Replace(lead, "").Trim();
        string? source = null;
        if (cleanedLead.Length > 0 && !OnlyReplayRegex().IsMatch(cleanedLead))
        {
            var stripped = LeadingReplayRegex().Replace(cleanedLead, "").Trim();
            source = stripped.Length > 0 ? stripped : null;
        }
        if (StartsWithReplayRegex().IsMatch(lead)) isReplay = true;

        return new ParsedRecording { RecordedAt = recordedAt, Source = source, IsReplay = isReplay };
    }

    /// <summary>
    /// Infer a game name from a recording's path relative to a watched root. NVIDIA Share
    /// nests clips in per-game folders (e.g. <c>E:/Shadowplay/Counter-strike 2/clip.mp4</c>),
    /// so the first path segment under the root is the game. Returns <c>null</c> when the
    /// file sits directly in the root (e.g. OBS's flat output) or isn't under the root.
    /// </summary>
    public static string? InferGameFromPath(string root, string filePath)
    {
        var normRoot = root.Replace('\\', '/').TrimEnd('/');
        var normFile = filePath.Replace('\\', '/');
        if (normRoot.Length == 0 ||
            !normFile.ToLowerInvariant().StartsWith(normRoot.ToLowerInvariant() + "/", StringComparison.Ordinal))
        {
            return null;
        }
        var segments = normFile[(normRoot.Length + 1)..]
            .Split('/')
            .Where(s => s.Length > 0)
            .ToArray();
        return segments.Length >= 2 ? segments[0] : null;
    }

    private static DateTime? TryBuildLocalDate(string y, string mo, string d, string h, string mi, string s)
    {
        try
        {
            return new DateTime(int.Parse(y), int.Parse(mo), int.Parse(d),
                int.Parse(h), int.Parse(mi), int.Parse(s), DateTimeKind.Local);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string StripExtension(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }
}
