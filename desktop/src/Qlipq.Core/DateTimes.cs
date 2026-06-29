using System.Globalization;

namespace Qlipq.Core;

/// <summary>Zero-dependency date/time formatting helpers used by filename parsing and rename templating.</summary>
public static class DateTimes
{
    private static string Pad(int value, int length = 2) =>
        value.ToString(CultureInfo.InvariantCulture).PadLeft(length, '0');

    /// <summary><c>YYYY-MM-DD</c> in local time.</summary>
    public static string FormatDate(DateTime date) =>
        $"{date.Year}-{Pad(date.Month)}-{Pad(date.Day)}";

    /// <summary><c>HH-MM-SS</c> in local time (dashes are filesystem-safe, unlike colons).</summary>
    public static string FormatTime(DateTime date) =>
        $"{Pad(date.Hour)}-{Pad(date.Minute)}-{Pad(date.Second)}";

    /// <summary><c>YYYY-MM-DD_HH-MM-SS</c> in local time.</summary>
    public static string FormatDateTime(DateTime date) =>
        $"{FormatDate(date)}_{FormatTime(date)}";

    /// <summary>Human duration like <c>1:02:03</c> or <c>2:05</c> from a number of seconds.</summary>
    public static string FormatDuration(double totalSeconds)
    {
        var safe = Math.Max(0, (long)Math.Floor(totalSeconds));
        var hours = safe / 3600;
        var minutes = safe % 3600 / 60;
        var seconds = safe % 60;
        if (hours > 0)
        {
            return $"{hours}:{Pad((int)minutes)}:{Pad((int)seconds)}";
        }
        return $"{minutes}:{Pad((int)seconds)}";
    }
}
