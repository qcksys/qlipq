using Qlipq.Core;
using Xunit;

namespace Qlipq.Core.Tests;

public class EditSpecTests
{
    private static readonly MediaInfo Media = new()
    {
        DurationSec = 120,
        Width = 1920,
        Height = 1080,
        VideoCodec = "h264",
        Fps = 60,
        AudioStreams =
        [
            new AudioStreamInfo { StreamIndex = 1, Index = 0, Codec = "aac", Channels = 2, Title = "Desktop" },
            new AudioStreamInfo { StreamIndex = 2, Index = 1, Codec = "aac", Channels = 1, Title = "Mic" },
        ],
    };

    [Fact]
    public void DefaultEditSpecEnablesEverySourceTrackAtUnityGain()
    {
        var spec = EditSpecs.DefaultEditSpec(Media);
        Assert.Equal(
            [
                new AudioTrackSpec { Index = 0, Enabled = true, Volume = 1 },
                new AudioTrackSpec { Index = 1, Enabled = true, Volume = 1 },
            ],
            spec.AudioTracks);
        Assert.Null(spec.Trim);
    }

    [Fact]
    public void EffectiveDurationReflectsTrimWindow()
    {
        Assert.Equal(120, EditSpecs.EffectiveDuration(new EditSpec { AudioTracks = [] }, Media));
        Assert.Equal(30, EditSpecs.EffectiveDuration(
            new EditSpec { AudioTracks = [], Trim = new TrimSpec { StartSec = 10, EndSec = 40 } }, Media));
    }

    [Fact]
    public void ValidateEditSpecAcceptsASaneSpec()
    {
        var spec = EditSpecs.DefaultEditSpec(Media);
        spec.Trim = new TrimSpec { StartSec = 5, EndSec = 50 };
        spec.Crop = new CropSpec { X = 0, Y = 0, Width = 1280, Height = 720 };
        Assert.Null(EditSpecs.ValidateEditSpec(spec, Media));
    }

    [Fact]
    public void ValidateEditSpecRejectsInvertedTrim()
    {
        var msg = EditSpecs.ValidateEditSpec(
            new EditSpec { AudioTracks = [], Trim = new TrimSpec { StartSec = 30, EndSec = 10 } }, Media);
        Assert.Contains("after the start", msg);
    }

    [Fact]
    public void ValidateEditSpecRejectsCropOutsideFrame()
    {
        var msg = EditSpecs.ValidateEditSpec(
            new EditSpec { AudioTracks = [], Crop = new CropSpec { X = 1000, Y = 0, Width = 1280, Height = 720 } }, Media);
        Assert.Contains("outside the frame", msg);
    }

    [Fact]
    public void ValidateEditSpecRejectsNegativeVolume()
    {
        var msg = EditSpecs.ValidateEditSpec(
            new EditSpec { AudioTracks = [new AudioTrackSpec { Index = 0, Enabled = true, Volume = -1 }] }, Media);
        Assert.Contains("volume cannot be negative", msg);
    }
}
