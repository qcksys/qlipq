using System.Globalization;
using System.Text.RegularExpressions;

namespace Qlipq.Ffmpeg;

public sealed record ProgressUpdate
{
    /// <summary>Output timestamp reached so far, in seconds, or null if unknown.</summary>
    public double? OutTimeSec { get; set; }

    /// <summary>True once ffmpeg reports <c>progress=end</c>.</summary>
    public bool Done { get; set; }
}

public static partial class Progress
{
    [GeneratedRegex(@"^(\d+):(\d{2}):(\d{2}(?:\.\d+)?)$", RegexOptions.ECMAScript)]
    private static partial Regex TimecodeRegex();

    [GeneratedRegex(@"\r?\n")]
    private static partial Regex LineSplitRegex();

    /// <summary>Parse a <c>HH:MM:SS.micro</c> timecode into seconds, or null if unparseable.</summary>
    public static double? ParseTimecode(string value)
    {
        var match = TimecodeRegex().Match(value.Trim());
        if (!match.Success) return null;
        var hours = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minutes = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        return hours * 3600 + minutes * 60 + seconds;
    }

    /// <summary>
    /// Parse one or more <c>-progress pipe:1</c> chunks. ffmpeg emits <c>key=value</c> lines in
    /// blocks terminated by <c>progress=continue|end</c>; we return the latest timestamp seen and
    /// whether the run finished. Note: ffmpeg's <c>out_time_ms</c> is actually microseconds — both
    /// it and <c>out_time_us</c> are divided by 1,000,000 (do not "fix" this).
    /// </summary>
    public static ProgressUpdate ParseProgress(string text)
    {
        double? outTimeSec = null;
        var done = false;

        foreach (var raw in LineSplitRegex().Split(text))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq];
            var value = line[(eq + 1)..];

            if (key is "out_time_us" or "out_time_ms")
            {
                if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var micros)
                    && double.IsFinite(micros) && micros >= 0)
                {
                    outTimeSec = micros / 1_000_000;
                }
            }
            else if (key == "out_time")
            {
                var parsed = ParseTimecode(value);
                if (parsed is not null) outTimeSec = parsed;
            }
            else if (key == "progress")
            {
                done = value == "end";
            }
        }

        return new ProgressUpdate { OutTimeSec = outTimeSec, Done = done };
    }

    /// <summary>Clamp an export progress fraction (0..1) from the current and total seconds.</summary>
    public static double ProgressFraction(double? outTimeSec, double durationSec)
    {
        if (outTimeSec is null || durationSec <= 0) return 0;
        return Math.Min(1, Math.Max(0, outTimeSec.Value / durationSec));
    }
}
