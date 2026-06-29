using System.Globalization;
using System.Text.RegularExpressions;
using Qlipq.Core;

namespace Qlipq.Ffmpeg;

public sealed record SizeEstimate
{
    public double Bytes { get; set; }

    /// <summary>True when the figure is a quality-model ballpark (CRF/preset), not a hard target.</summary>
    public bool Approximate { get; set; }
}

public static partial class Estimate
{
    /// <summary>
    /// Baseline bits-per-pixel (per frame) for each codec at CRF 23. Each 6 CRF steps roughly
    /// halves/doubles bitrate, so <c>bpp = base * 2^((23 - crf) / 6)</c>. Deliberately rough.
    /// </summary>
    private static double BppAtCrf23(string codec) => codec switch
    {
        "libx264" => 0.095,
        "libx265" => 0.06,
        _ => 0.095,
    };

    [GeneratedRegex(@"^[+-]?\d+")]
    private static partial Regex LeadingIntRegex();

    private static bool Truthy(int? value) => value is { } v && v != 0;

    /// <summary>Estimate the exported file size for a clip under the resolved encode settings.</summary>
    public static SizeEstimate EstimateExportSize(MediaInfo media, EditSpec spec, ResolvedEncode encode)
    {
        var duration = EditSpecs.EffectiveDuration(spec, media);
        if (duration <= 0) return new SizeEstimate { Bytes = 0, Approximate = false };

        var video = encode.Video;
        var forcedReencode = spec.Crop is not null || Truthy(video.ScaleHeight) || Truthy(video.Fps);
        var reencoding = encode.Reencode || forcedReencode;

        // Pure stream-copy: output ≈ the source scaled by the fraction of duration kept.
        if (!reencoding)
        {
            var sourceDuration = media.DurationSec != 0 ? media.DurationSec : duration;
            double sourceSize = media.SizeBytes ?? 0;
            return new SizeEstimate { Bytes = sourceSize * (duration / sourceDuration), Approximate = false };
        }

        // Output frame dimensions after crop + downscale.
        double cropW = spec.Crop?.Width ?? media.Width;
        double cropH = spec.Crop?.Height ?? media.Height;
        var outW = cropW;
        var outH = cropH;
        if (Truthy(video.ScaleHeight) && cropH > 0)
        {
            outH = video.ScaleHeight!.Value;
            outW = Math.Round(cropW * (video.ScaleHeight.Value / cropH) / 2.0, MidpointRounding.AwayFromZero) * 2;
        }
        var outFps = video.Fps is { } f && f > 0 ? f : media.Fps != 0 ? media.Fps : 30;

        var audioTracks = spec.AudioTracks.Count(t => t.Enabled);
        var audioKbps = audioTracks * ParseIntLeading(encode.Audio.Bitrate ?? "0");
        var audioBytes = audioKbps * 1000 * duration / 8;

        if (Truthy(video.BitrateKbps))
        {
            var bitrateBytes = (double)video.BitrateKbps!.Value * 1000 * duration / 8;
            return new SizeEstimate { Bytes = bitrateBytes + audioBytes, Approximate = false };
        }

        var baseBpp = BppAtCrf23(video.Codec ?? "libx264");
        var bpp = baseBpp * Math.Pow(2, (23 - (video.Crf ?? 20)) / 6.0);
        var videoBps = outW * outH * outFps * bpp;
        // Constrained VBR caps the bitrate, so the estimate can't exceed the cap.
        if (Truthy(video.MaxrateKbps)) videoBps = Math.Min(videoBps, (double)video.MaxrateKbps!.Value * 1000);
        var videoBytes = videoBps * duration / 8;
        return new SizeEstimate { Bytes = videoBytes + audioBytes, Approximate = true };
    }

    /// <summary>Mimics JS <c>parseInt(s, 10)</c>: leading integer, ignoring a trailing unit like <c>k</c>.</summary>
    private static double ParseIntLeading(string s)
    {
        var match = LeadingIntRegex().Match(s.Trim());
        return match.Success ? double.Parse(match.Value, CultureInfo.InvariantCulture) : double.NaN;
    }
}
