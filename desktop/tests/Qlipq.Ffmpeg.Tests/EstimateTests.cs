using Qlipq.Core;
using Qlipq.Ffmpeg;
using Xunit;

namespace Qlipq.Ffmpeg.Tests;

public class EstimateTests
{
    private static readonly MediaInfo Media = new()
    {
        DurationSec = 100,
        Width = 1920,
        Height = 1080,
        VideoCodec = "h264",
        Fps = 60,
        AudioStreams = [],
        SizeBytes = 1_000_000_000, // 1 GB over 100s
    };

    private static EditSpec OneAudio() => new() { AudioTracks = [new AudioTrackSpec { Index = 0, Enabled = true, Volume = 1 }] };

    private static ResolvedEncode Copy() =>
        new() { Video = new VideoEncodeOptions(), Audio = new AudioEncodeOptions { Bitrate = "192k" }, Reencode = false };

    private static ResolvedEncode Crf(Action<VideoEncodeOptions>? over = null)
    {
        var video = new VideoEncodeOptions { Codec = "libx264", Crf = 23 };
        over?.Invoke(video);
        return new ResolvedEncode { Video = video, Audio = new AudioEncodeOptions { Bitrate = "192k" }, Reencode = true };
    }

    [Fact]
    public void StreamCopyScalesSourceSizeByKeptDuration()
    {
        var full = Estimate.EstimateExportSize(Media, OneAudio(), Copy());
        Assert.True(Math.Abs(full.Bytes - 1_000_000_000) < 500);
        Assert.False(full.Approximate);

        var half = Estimate.EstimateExportSize(
            Media,
            new EditSpec { AudioTracks = OneAudio().AudioTracks, Trim = new TrimSpec { StartSec = 0, EndSec = 50 } },
            Copy());
        Assert.True(Math.Abs(half.Bytes - 500_000_000) < 500);
    }

    [Fact]
    public void BitrateModeIsExactBitrateTimesDuration()
    {
        var enc = new ResolvedEncode
        {
            Video = new VideoEncodeOptions { BitrateKbps = 8000 },
            Audio = new AudioEncodeOptions { Bitrate = "0k" },
            Reencode = true,
        };
        var r = Estimate.EstimateExportSize(Media, new EditSpec { AudioTracks = [] }, enc);
        Assert.True(Math.Abs(r.Bytes - 8000.0 * 1000 * 100 / 8) < 500);
        Assert.False(r.Approximate);
    }

    [Fact]
    public void CrfEstimateIsApproximateAndMonotonic()
    {
        var better = Estimate.EstimateExportSize(Media, OneAudio(), Crf(v => v.Crf = 18));
        var worse = Estimate.EstimateExportSize(Media, OneAudio(), Crf(v => v.Crf = 28));
        Assert.True(better.Approximate);
        Assert.True(better.Bytes > worse.Bytes);

        var downscaled = Estimate.EstimateExportSize(Media, OneAudio(), Crf(v => { v.Crf = 23; v.ScaleHeight = 540; }));
        var fullRes = Estimate.EstimateExportSize(Media, OneAudio(), Crf(v => v.Crf = 23));
        Assert.True(downscaled.Bytes < fullRes.Bytes);
    }

    [Fact]
    public void H265EstimatesSmallerThanH264AtSameCrf()
    {
        var h264 = Estimate.EstimateExportSize(Media, OneAudio(), Crf(v => v.Codec = "libx264"));
        var h265 = Estimate.EstimateExportSize(Media, OneAudio(), Crf(v => v.Codec = "libx265"));
        Assert.True(h265.Bytes < h264.Bytes);
    }

    [Fact]
    public void ZeroLengthOutputEstimatesToZero()
    {
        var r = Estimate.EstimateExportSize(
            Media,
            new EditSpec { AudioTracks = OneAudio().AudioTracks, Trim = new TrimSpec { StartSec = 10, EndSec = 10 } },
            Copy());
        Assert.Equal(0, r.Bytes);
    }
}
