using System.Globalization;

namespace Qlipq.Core;

/// <summary>Probed information about a single audio stream within a media file.</summary>
public sealed record AudioStreamInfo
{
    /// <summary>Absolute stream index in the container (as ffmpeg's <c>0:N</c>).</summary>
    public int StreamIndex { get; set; }

    /// <summary>Audio-relative index used by ffmpeg's <c>0:a:N</c> selector.</summary>
    public int Index { get; set; }

    public string Codec { get; set; } = "unknown";
    public int Channels { get; set; }
    public string? Language { get; set; }
    public string? Title { get; set; }
}

/// <summary>Probed information about a media file, derived from ffprobe.</summary>
public sealed record MediaInfo
{
    public double DurationSec { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string VideoCodec { get; set; } = "unknown";
    public double Fps { get; set; }
    public List<AudioStreamInfo> AudioStreams { get; set; } = [];
    public long? SizeBytes { get; set; }
}

public static class MediaFormat
{
    /// <summary>A best-effort, human-friendly label for an audio stream.</summary>
    public static string AudioStreamLabel(AudioStreamInfo stream)
    {
        if (!string.IsNullOrEmpty(stream.Title)) return stream.Title;
        if (!string.IsNullOrEmpty(stream.Language)) return $"Track {stream.Index + 1} ({stream.Language})";
        return $"Track {stream.Index + 1}";
    }

    /// <summary>Human-friendly file size, e.g. <c>1.4 GB</c>, using binary units (1024).</summary>
    public static string FormatBytes(double bytes)
    {
        if (!double.IsFinite(bytes) || bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var exp = Math.Min((int)Math.Floor(Math.Log(bytes) / Math.Log(1024)), units.Length - 1);
        var value = bytes / Math.Pow(1024, exp);
        return $"{value.ToString(exp == 0 ? "F0" : "F1", CultureInfo.InvariantCulture)} {units[exp]}";
    }
}
