using Qlipq.Core;
using Qlipq.Ffmpeg;
using Xunit;

namespace Qlipq.Ffmpeg.Tests;

public class ArgsTests
{
    private static List<string> Args(EditSpec spec, Action<BuildExportOptions>? extra = null)
    {
        var opts = new BuildExportOptions { InputPath = "in.mkv", OutputPath = "out.mp4", Spec = spec };
        extra?.Invoke(opts);
        return ExportArgs.BuildExportArgs(opts);
    }

    private static AudioTrackSpec Track(int index, bool enabled, double volume) =>
        new() { Index = index, Enabled = enabled, Volume = volume };

    [Fact]
    public void TrimOnlyDefaultsToFastStreamCopy()
    {
        var outArgs = Args(new EditSpec
        {
            Trim = new TrimSpec { StartSec = 5, EndSec = 12.5 },
            AudioTracks = [Track(0, true, 1)],
        });
        Assert.Equal(
            ["-y", "-ss", "5.000", "-i", "in.mkv", "-t", "7.500", "-map", "0:v:0", "-map", "0:a:0",
                "-c:v", "copy", "-c:a", "copy", "out.mp4"],
            outArgs);
    }

    [Fact]
    public void ForcedReencodeOnTrimOnlyReencodesVideoCopiesAudio()
    {
        var outArgs = Args(
            new EditSpec { Trim = new TrimSpec { StartSec = 0, EndSec = 10 }, AudioTracks = [Track(0, true, 1)] },
            o => o.Reencode = true);
        Assert.Contains("-c:v", outArgs);
        Assert.Contains("libx264", outArgs);
        Assert.Contains("-c:a copy", string.Join(" ", outArgs));
        Assert.DoesNotContain("-filter_complex", outArgs);
    }

    [Fact]
    public void CropBuildsFilterGraphAndReencodesVideo()
    {
        var outArgs = Args(new EditSpec
        {
            Crop = new CropSpec { X = 100, Y = 50, Width = 1280, Height = 720 },
            AudioTracks = [Track(0, true, 1)],
        });
        var joined = string.Join(" ", outArgs);
        Assert.Contains("-filter_complex [0:v:0]crop=1280:720:100:50[vout]", joined);
        Assert.Contains("-map [vout]", joined);
        Assert.Contains("-map 0:a:0", joined);
        Assert.Contains("-c:v libx264", joined);
        Assert.Contains("-c:a copy", joined);
    }

    [Fact]
    public void AudioVolumeChangeReencodesAudioViaFilterCopiesVideo()
    {
        var outArgs = Args(new EditSpec
        {
            AudioTracks = [Track(0, true, 0.5), Track(1, true, 1)],
        });
        var joined = string.Join(" ", outArgs);
        Assert.Contains("-filter_complex [0:a:0]volume=0.5[aout0]", joined);
        Assert.Contains("-map 0:v:0", joined);
        Assert.Contains("-map [aout0]", joined);
        Assert.Contains("-map 0:a:1", joined);
        Assert.Contains("-c:v copy", joined);
        Assert.Contains("-c:a aac -b:a 192k", joined);
    }

    [Fact]
    public void CropPlusVolumeCombinesVideoAndAudioFilters()
    {
        var outArgs = Args(new EditSpec
        {
            Crop = new CropSpec { X = 0, Y = 0, Width = 640, Height = 480 },
            AudioTracks = [Track(0, true, 2)],
        });
        var joined = string.Join(" ", outArgs);
        Assert.Contains("[0:v:0]crop=640:480:0:0[vout];[0:a:0]volume=2[aout0]", joined);
        Assert.Contains("-c:v libx264", joined);
        Assert.Contains("-c:a aac", joined);
    }

    [Fact]
    public void DisablingAllAudioYieldsAnAndNoAudioCodec()
    {
        var outArgs = Args(new EditSpec
        {
            AudioTracks = [Track(0, false, 1), Track(1, false, 1)],
        });
        Assert.Contains("-an", outArgs);
        Assert.DoesNotContain("-c:a", outArgs);
    }

    [Fact]
    public void ProgressFlagAppendsMachineReadableProgress()
    {
        var outArgs = Args(new EditSpec { AudioTracks = [Track(0, true, 1)] }, o => o.Progress = true);
        Assert.Contains("-progress pipe:1 -nostats", string.Join(" ", outArgs));
    }

    [Fact]
    public void CustomEncoderOptionsAreHonoured()
    {
        var outArgs = Args(
            new EditSpec
            {
                Crop = new CropSpec { X = 0, Y = 0, Width = 100, Height = 100 },
                AudioTracks = [Track(0, true, 0)],
            },
            o =>
            {
                o.Video = new VideoEncodeOptions { Codec = "libx265", Crf = 28, Preset = "fast" };
                o.Audio = new AudioEncodeOptions { Codec = "libopus", Bitrate = "96k" };
            });
        var joined = string.Join(" ", outArgs);
        Assert.Contains("-c:v libx265 -preset fast -crf 28", joined);
        Assert.Contains("-c:a libopus -b:a 96k", joined);
    }

    [Fact]
    public void MetadataStampsEntriesBeforeOutput()
    {
        var outArgs = Args(
            new EditSpec { AudioTracks = [Track(0, true, 1)] },
            o => o.Metadata = [new("game", "Deadlock")]);
        var joined = string.Join(" ", outArgs);
        Assert.Contains("-metadata game=Deadlock", joined);
        Assert.True(outArgs.IndexOf("-metadata") < outArgs.IndexOf("out.mp4"));
    }

    [Fact]
    public void FrameRateChangeReencodesAndEmitsRWithoutFilter()
    {
        var outArgs = Args(
            new EditSpec { AudioTracks = [Track(0, true, 1)] },
            o => o.Video = new VideoEncodeOptions { Fps = 30 });
        var joined = string.Join(" ", outArgs);
        Assert.Contains("-c:v libx264", joined);
        Assert.Contains("-r 30", joined);
        Assert.DoesNotContain("-filter_complex", outArgs);
        Assert.Contains("-c:a copy", joined);
    }

    [Fact]
    public void DownscaleBuildsScaleFilterAndReencodes()
    {
        var outArgs = Args(
            new EditSpec { AudioTracks = [Track(0, true, 1)] },
            o => o.Video = new VideoEncodeOptions { ScaleHeight = 720 });
        var joined = string.Join(" ", outArgs);
        Assert.Contains("-filter_complex [0:v:0]scale=-2:720[vout]", joined);
        Assert.Contains("-map [vout]", joined);
        Assert.Contains("-c:v libx264", joined);
    }

    [Fact]
    public void CropAndDownscaleComposeIntoOneFilterChain()
    {
        var outArgs = Args(
            new EditSpec { Crop = new CropSpec { X = 0, Y = 0, Width = 1920, Height = 1080 }, AudioTracks = [] },
            o => o.Video = new VideoEncodeOptions { ScaleHeight = 720 });
        Assert.Contains("[0:v:0]crop=1920:1080:0:0,scale=-2:720[vout]", string.Join(" ", outArgs));
    }

    [Fact]
    public void BitrateRateControlUsesBvInsteadOfCrf()
    {
        var outArgs = Args(
            new EditSpec { AudioTracks = [Track(0, true, 1)] },
            o =>
            {
                o.Reencode = true;
                o.Video = new VideoEncodeOptions { BitrateKbps = 6000 };
            });
        var joined = string.Join(" ", outArgs);
        Assert.Contains("-b:v 6000k", joined);
        Assert.DoesNotContain("-crf", joined);
    }

    private static readonly MediaInfo Media = new()
    {
        DurationSec = 60,
        Width = 2560,
        Height = 1440,
        VideoCodec = "h264",
        Fps = 60,
        AudioStreams = [],
    };

    private static OutputSettings Settings(Action<OutputSettings> over)
    {
        var s = new OutputSettings();
        over(s);
        return s;
    }

    [Fact]
    public void OutputSettingsToEncode_OriginalPresetIsStreamCopy()
    {
        var r = Encode.OutputSettingsToEncode(Settings(s => s.QualityPreset = QualityPreset.Original), Media);
        Assert.False(r.Reencode);
    }

    [Fact]
    public void OutputSettingsToEncode_NamedPresetsMapToCrfAndForceReencode()
    {
        Assert.Equal(18, Encode.OutputSettingsToEncode(Settings(s => s.QualityPreset = QualityPreset.High), Media).Video.Crf);
        Assert.Equal(23, Encode.OutputSettingsToEncode(Settings(s => s.QualityPreset = QualityPreset.Balanced), Media).Video.Crf);
        var small = Encode.OutputSettingsToEncode(Settings(s => s.QualityPreset = QualityPreset.Small), Media);
        Assert.Equal(28, small.Video.Crf);
        Assert.True(small.Reencode);
    }

    [Fact]
    public void OutputSettingsToEncode_BitrateModeSetsBitrateKbps()
    {
        var r = Encode.OutputSettingsToEncode(
            Settings(s => { s.QualityMode = QualityMode.Bitrate; s.VideoBitrateKbps = 5000; }), Media);
        Assert.Equal(5000, r.Video.BitrateKbps);
        Assert.True(r.Reencode);
    }

    [Fact]
    public void OutputSettingsToEncode_VbrMapsToCrfPlusMaxrateCap()
    {
        var r = Encode.OutputSettingsToEncode(
            Settings(s => { s.QualityMode = QualityMode.Vbr; s.Crf = 22; s.VideoBitrateKbps = 9000; }), Media);
        Assert.Equal(22, r.Video.Crf);
        Assert.Equal(9000, r.Video.MaxrateKbps);
        Assert.True(r.Reencode);

        var outArgs = Args(
            new EditSpec { AudioTracks = [Track(0, true, 1)] },
            o => { o.Reencode = true; o.Video = r.Video; });
        var joined = string.Join(" ", outArgs);
        Assert.Contains("-crf 22", joined);
        Assert.Contains("-maxrate 9000k", joined);
        Assert.Contains("-bufsize 18000k", joined);
    }

    [Fact]
    public void OutputSettingsToEncode_FpsMaxHeightClampAgainstSource()
    {
        var up = Encode.OutputSettingsToEncode(Settings(s => { s.Fps = 120; s.MaxHeight = 2160; }), Media);
        Assert.Null(up.Video.Fps);
        Assert.Null(up.Video.ScaleHeight);
        var down = Encode.OutputSettingsToEncode(Settings(s => { s.Fps = 30; s.MaxHeight = 1080; }), Media);
        Assert.Equal(30, down.Video.Fps);
        Assert.Equal(1080, down.Video.ScaleHeight);
    }
}
