using System.Globalization;

namespace Qlipq.App.ViewModels;

/// <summary>ISO-8601 timestamp helpers matching the web app's <c>Date.toISOString()</c> persistence.</summary>
public static class IsoTime
{
    public static string FromLocal(DateTime local) =>
        new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local)).ToUniversalTime()
            .ToString("o", CultureInfo.InvariantCulture);

    public static string FromUnixMs(long ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).ToString("o", CultureInfo.InvariantCulture);

    public static string UtcNow() => DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    /// <summary>Parse an ISO timestamp back to local time for display; null if unparseable.</summary>
    public static DateTime? ToLocal(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return null;
        return DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto.LocalDateTime
            : null;
    }
}
