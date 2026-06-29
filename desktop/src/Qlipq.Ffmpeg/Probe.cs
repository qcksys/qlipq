using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qlipq.Core;

namespace Qlipq.Ffmpeg;

/// <summary>A single stream entry as produced by <c>ffprobe -show_streams -print_format json</c>.</summary>
public sealed class FfprobeStream
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("codec_type")] public string? CodecType { get; set; }
    [JsonPropertyName("codec_name")] public string? CodecName { get; set; }
    [JsonPropertyName("width")] public int? Width { get; set; }
    [JsonPropertyName("height")] public int? Height { get; set; }
    [JsonPropertyName("channels")] public int? Channels { get; set; }
    [JsonPropertyName("r_frame_rate")] public string? RFrameRate { get; set; }
    [JsonPropertyName("avg_frame_rate")] public string? AvgFrameRate { get; set; }
    [JsonPropertyName("tags")] public Dictionary<string, string>? Tags { get; set; }
}

public sealed class FfprobeFormat
{
    [JsonPropertyName("duration")] public string? Duration { get; set; }
    [JsonPropertyName("size")] public string? Size { get; set; }
}

/// <summary>The relevant subset of ffprobe's JSON output.</summary>
public sealed class FfprobeOutput
{
    [JsonPropertyName("streams")] public List<FfprobeStream>? Streams { get; set; }
    [JsonPropertyName("format")] public FfprobeFormat? Format { get; set; }
}

public static class Probe
{
    /// <summary>Build the ffprobe argument list that produces parseable JSON for a file.</summary>
    public static List<string> BuildProbeArgs(string inputPath) =>
        ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", inputPath];

    /// <summary>Convert an ffmpeg rational frame rate like <c>30000/1001</c> into fps.</summary>
    public static double ParseFrameRate(string? rate)
    {
        if (string.IsNullOrEmpty(rate)) return 0;
        var parts = rate.Split('/');
        var num = parts.Length > 0 ? JsNumber(parts[0]) : double.NaN;
        var den = parts.Length > 1 ? JsNumber(parts[1]) : double.NaN;
        if (den == 0 || double.IsNaN(den) || double.IsNaN(num))
        {
            return double.IsFinite(num) ? num : 0;
        }
        return Math.Round(num / den * 1000, MidpointRounding.AwayFromZero) / 1000;
    }

    /// <summary>Parse ffprobe JSON text into a <see cref="MediaInfo"/>.</summary>
    public static MediaInfo ParseFfprobe(string json) =>
        ParseFfprobe(JsonSerializer.Deserialize<FfprobeOutput>(json) ?? new FfprobeOutput());

    /// <summary>Parse already-deserialized ffprobe output into a <see cref="MediaInfo"/>.</summary>
    public static MediaInfo ParseFfprobe(FfprobeOutput data)
    {
        var streams = data.Streams ?? [];
        var video = streams.FirstOrDefault(s => s.CodecType == "video");

        var audioStreams = streams
            .Where(s => s.CodecType == "audio")
            .Select((s, i) => new AudioStreamInfo
            {
                StreamIndex = s.Index,
                Index = i,
                Codec = s.CodecName ?? "unknown",
                Channels = s.Channels ?? 0,
                Language = s.Tags is not null && s.Tags.TryGetValue("language", out var lang) ? lang : null,
                Title = s.Tags is not null && s.Tags.TryGetValue("title", out var title) ? title : null,
            })
            .ToList();

        return new MediaInfo
        {
            DurationSec = JsParseFloatOrZero(data.Format?.Duration),
            Width = video?.Width ?? 0,
            Height = video?.Height ?? 0,
            VideoCodec = video?.CodecName ?? "unknown",
            Fps = ParseFrameRate(video?.RFrameRate ?? video?.AvgFrameRate),
            AudioStreams = audioStreams,
            SizeBytes = !string.IsNullOrEmpty(data.Format?.Size) ? JsParseInt(data.Format.Size) : null,
        };
    }

    /// <summary>Mimics JS <c>Number(s)</c> for a single field: blank → 0, non-numeric → NaN.</summary>
    private static double JsNumber(string s)
    {
        var trimmed = s.Trim();
        if (trimmed.Length == 0) return 0;
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : double.NaN;
    }

    /// <summary>Mimics JS <c>Number.parseFloat(x ?? "0") || 0</c>.</summary>
    private static double JsParseFloatOrZero(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value != 0
            ? value
            : 0;
    }

    /// <summary>Mimics JS <c>Number.parseInt(x, 10)</c>; null when no leading integer.</summary>
    private static long? JsParseInt(string s)
    {
        return long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
