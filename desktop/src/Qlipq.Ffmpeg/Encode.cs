using System.Globalization;
using Qlipq.Core;

namespace Qlipq.Ffmpeg;

public sealed record VideoEncodeOptions
{
    public string? Codec { get; set; }
    public int? Crf { get; set; }

    /// <summary>Target video bitrate in kbps. When set, takes precedence over <see cref="Crf"/>.</summary>
    public int? BitrateKbps { get; set; }

    /// <summary>Max bitrate cap (kbps) for constrained-VBR: pairs with <see cref="Crf"/> via -maxrate/-bufsize.</summary>
    public int? MaxrateKbps { get; set; }

    public string? Preset { get; set; }

    /// <summary>Output frame rate; when set, forces a re-encode.</summary>
    public int? Fps { get; set; }

    /// <summary>Downscale to this height (keeps aspect, even width); forces a re-encode.</summary>
    public int? ScaleHeight { get; set; }
}

public sealed record AudioEncodeOptions
{
    public string? Codec { get; set; }
    public string? Bitrate { get; set; }
}

/// <summary>Resolved encoding choices, ready to feed <see cref="ExportArgs.BuildExportArgs"/>.</summary>
public sealed record ResolvedEncode
{
    public VideoEncodeOptions Video { get; set; } = new();
    public AudioEncodeOptions Audio { get; set; } = new();

    /// <summary>Whether the chosen quality wants a re-encode (edits may force one regardless).</summary>
    public bool Reencode { get; set; }
}

public static class Encode
{
    /// <summary>CRF values backing each named quality preset (<c>original</c> stream-copies instead).</summary>
    private static int PresetCrf(QualityPreset preset) => preset switch
    {
        QualityPreset.High => 18,
        QualityPreset.Balanced => 23,
        QualityPreset.Small => 28,
        _ => 23,
    };

    /// <summary>Format a number of seconds for ffmpeg's <c>-ss</c>/<c>-t</c> (millisecond precision).</summary>
    public static string FormatSeconds(double sec) =>
        Math.Max(0, sec).ToString("F3", CultureInfo.InvariantCulture);

    internal static string FormatVolume(double volume) =>
        Math.Round(volume, 4, MidpointRounding.AwayFromZero).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Resolve persisted <see cref="OutputSettings"/> into concrete encode options for a clip.
    /// fps and downscale are clamped against the source so we never up-rate or up-scale.
    /// </summary>
    public static ResolvedEncode OutputSettingsToEncode(OutputSettings output, MediaInfo media)
    {
        int? fps = output.Fps > 0 && output.Fps < media.Fps ? output.Fps : null;
        int? scaleHeight = output.MaxHeight > 0 && output.MaxHeight < media.Height ? output.MaxHeight : null;

        var video = new VideoEncodeOptions
        {
            Codec = output.VideoCodec.ToFfmpegCodec(),
            Preset = output.EncoderPreset,
            Fps = fps,
            ScaleHeight = scaleHeight,
        };

        var reencode = false;
        if (output.QualityMode == QualityMode.Bitrate)
        {
            video.BitrateKbps = output.VideoBitrateKbps;
            reencode = true;
        }
        else if (output.QualityMode == QualityMode.Vbr)
        {
            video.Crf = output.Crf;
            video.MaxrateKbps = output.VideoBitrateKbps;
            reencode = true;
        }
        else if (output.QualityMode == QualityMode.Crf)
        {
            video.Crf = output.Crf;
            reencode = true;
        }
        else if (output.QualityPreset == QualityPreset.Original)
        {
            // Stream-copy by default; this crf only applies if an edit forces a re-encode.
            video.Crf = 18;
            reencode = false;
        }
        else
        {
            video.Crf = PresetCrf(output.QualityPreset);
            reencode = true;
        }

        return new ResolvedEncode
        {
            Video = video,
            Audio = new AudioEncodeOptions { Codec = "aac", Bitrate = $"{output.AudioBitrateKbps}k" },
            Reencode = reencode,
        };
    }
}
