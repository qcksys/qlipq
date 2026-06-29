using System.Text;
using System.Text.RegularExpressions;

namespace Qlipq.Core;

/// <summary>Values available to a naming template when renaming a clip.</summary>
public sealed record RenameVars
{
    /// <summary>Original base name without extension.</summary>
    public string Name { get; set; } = "";

    /// <summary>Original extension without the leading dot.</summary>
    public string Ext { get; set; } = "";

    public DateTime? RecordedAt { get; set; }
    public string? Source { get; set; }

    /// <summary>1-based position used by the <c>{index}</c> token.</summary>
    public int? Index { get; set; }
}

public static partial class Rename
{
    private const string FallbackBase = "clip";

    // Characters illegal in Windows filenames (the strictest common target).
    // Dashes and spaces are intentionally allowed; date/time tokens rely on dashes.
    [GeneratedRegex("[<>:\"/\\\\|?*]")]
    private static partial Regex IllegalRegex();

    [GeneratedRegex(@"[ .]+$")]
    private static partial Regex TrailingDotsSpacesRegex();

    [GeneratedRegex(@"[_.\s-]{2,}")]
    private static partial Regex SeparatorRunRegex();

    [GeneratedRegex(@"^[_.\s-]+|[_.\s-]+$")]
    private static partial Regex EdgeSeparatorsRegex();

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.ECMAScript)]
    private static partial Regex TokenRegex();

    /// <summary>Replace illegal filename characters (incl. control chars) and trim trailing dots/spaces.</summary>
    public static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            sb.Append(ch < 0x20 ? '_' : ch);
        }
        var withoutControls = sb.ToString();
        var replaced = IllegalRegex().Replace(withoutControls, "_");
        replaced = TrailingDotsSpacesRegex().Replace(replaced, "");
        return replaced.Trim();
    }

    /// <summary>Collapse runs of separators left behind by empty tokens, and trim edge separators.</summary>
    private static string TidySeparators(string value)
    {
        var collapsed = SeparatorRunRegex().Replace(value, m =>
            m.Value.Contains(' ') ? " " : m.Value[0].ToString());
        return EdgeSeparatorsRegex().Replace(collapsed, "");
    }

    /// <summary>
    /// Expand a naming template into a base filename (no extension).
    /// Supported tokens: <c>{name} {source} {date} {time} {datetime} {index} {ext}</c>.
    /// Unknown tokens expand to an empty string; the result is sanitized and falls back to
    /// <c>clip</c> if everything resolved away.
    /// </summary>
    public static string ApplyNamingTemplate(string template, RenameVars vars)
    {
        var expanded = TokenRegex().Replace(template, match =>
        {
            var token = match.Groups[1].Value;
            return token switch
            {
                "name" => vars.Name,
                "source" => vars.Source ?? "",
                "date" => vars.RecordedAt is { } d1 ? DateTimes.FormatDate(d1) : "",
                "time" => vars.RecordedAt is { } d2 ? DateTimes.FormatTime(d2) : "",
                "datetime" => vars.RecordedAt is { } d3 ? DateTimes.FormatDateTime(d3) : "",
                "index" => vars.Index is { } i ? i.ToString(System.Globalization.CultureInfo.InvariantCulture) : "",
                "ext" => vars.Ext,
                _ => "",
            };
        });
        var baseName = TidySeparators(SanitizeFileName(expanded));
        return baseName.Length > 0 ? baseName : FallbackBase;
    }

    /// <summary>Build a full target filename (base + preserved extension) from a template.</summary>
    public static string BuildRenamedFileName(string template, RenameVars vars)
    {
        var baseName = ApplyNamingTemplate(template, vars);
        return vars.Ext.Length > 0 ? $"{baseName}.{vars.Ext}" : baseName;
    }

    /// <summary>Split a filename into its base name and extension (without dot).</summary>
    public static (string Name, string Ext) SplitFileName(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        if (dot <= 0) return (fileName, "");
        return (fileName[..dot], fileName[(dot + 1)..]);
    }
}
