using System.Globalization;
using Qlipq.Core;

namespace Qlipq.Ffmpeg;

public sealed record BuildExportOptions
{
    public required string InputPath { get; set; }
    public required string OutputPath { get; set; }
    public required EditSpec Spec { get; set; }

    /// <summary>Force a full re-encode even when a stream copy would suffice.</summary>
    public bool Reencode { get; set; }

    /// <summary>Append <c>-progress pipe:1 -nostats</c> for machine-readable progress on stdout.</summary>
    public bool Progress { get; set; }

    public VideoEncodeOptions? Video { get; set; }
    public AudioEncodeOptions? Audio { get; set; }

    /// <summary>Container metadata to stamp into the output, e.g. <c>{ game = "Deadlock" }</c>.</summary>
    public IReadOnlyList<KeyValuePair<string, string>>? Metadata { get; set; }
}

public static class ExportArgs
{
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>JS-style truthiness for an optional integer: present and non-zero.</summary>
    private static bool Truthy(int? value) => value is { } v && v != 0;

    /// <summary>
    /// Build the ffmpeg argument list to apply an <see cref="EditSpec"/> to a clip.
    /// <para>
    /// - Trim uses a fast seek (<c>-ss</c> before <c>-i</c>, <c>-t</c> after). With a stream
    ///   copy this snaps to the nearest keyframe; pass <c>Reencode = true</c> for frame accuracy.
    /// - Crop and downscale (<c>Video.ScaleHeight</c>) compose into one video filter and force a
    ///   re-encode; a changed frame rate (<c>Video.Fps</c>) also forces one.
    /// - <c>Video.BitrateKbps</c> selects bitrate rate-control (<c>-b:v</c>), else CRF (<c>-crf</c>).
    /// - Audio tracks are mapped by their audio-relative index; a non-unity volume re-encodes
    ///   audio (aac by default). Disabling all audio yields <c>-an</c>.
    /// </para>
    /// </summary>
    public static List<string> BuildExportArgs(BuildExportOptions opts)
    {
        var spec = opts.Spec;
        var videoCodec = opts.Video?.Codec ?? "libx264";
        var videoCrf = opts.Video?.Crf ?? 20;
        var videoPreset = opts.Video?.Preset ?? "veryfast";
        var videoBitrateKbps = opts.Video?.BitrateKbps;
        var videoMaxrateKbps = opts.Video?.MaxrateKbps;
        var videoFps = opts.Video?.Fps;
        var videoScaleHeight = opts.Video?.ScaleHeight;
        var audioCodec = opts.Audio?.Codec ?? "aac";
        var audioBitrate = opts.Audio?.Bitrate ?? "192k";

        var enabledAudio = spec.AudioTracks.Where(t => t.Enabled).ToList();
        var needsVideoFilter = spec.Crop is not null || Truthy(videoScaleHeight);
        var needsAudioFilter = enabledAudio.Any(t => t.Volume != 1);
        var videoReencode = needsVideoFilter || Truthy(videoFps) || opts.Reencode;
        var audioReencode = needsAudioFilter;

        var args = new List<string> { "-y" };

        double? duration = null;
        if (spec.Trim is { } trim)
        {
            args.Add("-ss");
            args.Add(Encode.FormatSeconds(trim.StartSec));
            duration = Math.Max(0, trim.EndSec - trim.StartSec);
        }
        args.Add("-i");
        args.Add(opts.InputPath);
        if (duration is { } dur)
        {
            args.Add("-t");
            args.Add(Encode.FormatSeconds(dur));
        }

        if (needsVideoFilter || needsAudioFilter)
        {
            var filters = new List<string>();
            var videoMap = "0:v:0";

            var videoSteps = new List<string>();
            if (spec.Crop is { } crop)
            {
                videoSteps.Add($"crop={I(crop.Width)}:{I(crop.Height)}:{I(crop.X)}:{I(crop.Y)}");
            }
            if (Truthy(videoScaleHeight)) videoSteps.Add($"scale=-2:{I(videoScaleHeight!.Value)}");
            if (videoSteps.Count > 0)
            {
                filters.Add($"[0:v:0]{string.Join(",", videoSteps)}[vout]");
                videoMap = "[vout]";
            }

            var audioMaps = new List<string>();
            for (var i = 0; i < enabledAudio.Count; i++)
            {
                var track = enabledAudio[i];
                if (track.Volume != 1)
                {
                    var label = $"[aout{I(i)}]";
                    filters.Add($"[0:a:{I(track.Index)}]volume={Encode.FormatVolume(track.Volume)}{label}");
                    audioMaps.Add(label);
                }
                else
                {
                    audioMaps.Add($"0:a:{I(track.Index)}");
                }
            }
            args.Add("-filter_complex");
            args.Add(string.Join(";", filters));
            args.Add("-map");
            args.Add(videoMap);
            foreach (var map in audioMaps)
            {
                args.Add("-map");
                args.Add(map);
            }
        }
        else
        {
            args.Add("-map");
            args.Add("0:v:0");
            if (enabledAudio.Count == 0)
            {
                args.Add("-an");
            }
            else
            {
                foreach (var track in enabledAudio)
                {
                    args.Add("-map");
                    args.Add($"0:a:{I(track.Index)}");
                }
            }
        }

        if (videoReencode)
        {
            args.Add("-c:v");
            args.Add(videoCodec);
            args.Add("-preset");
            args.Add(videoPreset);
            if (Truthy(videoBitrateKbps))
            {
                args.Add("-b:v");
                args.Add($"{I(videoBitrateKbps!.Value)}k");
            }
            else
            {
                args.Add("-crf");
                args.Add(I(videoCrf));
                // Constrained VBR: cap the bitrate while keeping CRF quality.
                if (Truthy(videoMaxrateKbps))
                {
                    args.Add("-maxrate");
                    args.Add($"{I(videoMaxrateKbps!.Value)}k");
                    args.Add("-bufsize");
                    args.Add($"{I(videoMaxrateKbps.Value * 2)}k");
                }
            }
            if (Truthy(videoFps))
            {
                args.Add("-r");
                args.Add(I(videoFps!.Value));
            }
        }
        else
        {
            args.Add("-c:v");
            args.Add("copy");
        }

        if (enabledAudio.Count > 0)
        {
            if (audioReencode)
            {
                args.Add("-c:a");
                args.Add(audioCodec);
                args.Add("-b:a");
                args.Add(audioBitrate);
            }
            else
            {
                args.Add("-c:a");
                args.Add("copy");
            }
        }

        if (opts.Metadata is { } metadata)
        {
            foreach (var (key, value) in metadata)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    args.Add("-metadata");
                    args.Add($"{key}={value}");
                }
            }
        }

        if (opts.Progress)
        {
            args.Add("-progress");
            args.Add("pipe:1");
            args.Add("-nostats");
        }

        args.Add(opts.OutputPath);
        return args;
    }
}
