using Qlipq.Core;
using Qlipq.Ffmpeg;
using Xunit;

namespace Qlipq.Ffmpeg.Tests;

public class ProbeTests
{
    [Fact]
    public void BuildProbeArgsRequestsJsonFormatAndStreams()
    {
        Assert.Equal(
            ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", "clip.mkv"],
            Probe.BuildProbeArgs("clip.mkv"));
    }

    [Fact]
    public void ParseFrameRateHandlesRationalsAndIntegers()
    {
        Assert.Equal(29.97, Probe.ParseFrameRate("30000/1001"));
        Assert.Equal(60, Probe.ParseFrameRate("60/1"));
        Assert.Equal(0, Probe.ParseFrameRate("0/0"));
        Assert.Equal(0, Probe.ParseFrameRate(null));
    }

    [Fact]
    public void ParseFfprobeExtractsVideoAndAudioRelativeIndices()
    {
        var probe = new FfprobeOutput
        {
            Streams =
            [
                new FfprobeStream { Index = 0, CodecType = "video", CodecName = "h264", Width = 2560, Height = 1440, RFrameRate = "60/1" },
                new FfprobeStream { Index = 1, CodecType = "audio", CodecName = "aac", Channels = 2, Tags = new() { ["title"] = "Desktop" } },
                new FfprobeStream { Index = 2, CodecType = "audio", CodecName = "aac", Channels = 1, Tags = new() { ["language"] = "eng", ["title"] = "Mic" } },
            ],
            Format = new FfprobeFormat { Duration = "63.500000", Size = "104857600" },
        };
        var info = Probe.ParseFfprobe(probe);
        Assert.Equal(63.5, info.DurationSec);
        Assert.Equal(2560, info.Width);
        Assert.Equal(1440, info.Height);
        Assert.Equal("h264", info.VideoCodec);
        Assert.Equal(60, info.Fps);
        Assert.Equal(104857600, info.SizeBytes);
        Assert.Equal(
            [
                new AudioStreamInfo { StreamIndex = 1, Index = 0, Codec = "aac", Channels = 2, Language = null, Title = "Desktop" },
                new AudioStreamInfo { StreamIndex = 2, Index = 1, Codec = "aac", Channels = 1, Language = "eng", Title = "Mic" },
            ],
            info.AudioStreams);
    }

    [Fact]
    public void ParseFfprobeAcceptsAJsonString()
    {
        var info = Probe.ParseFfprobe("{\"streams\":[],\"format\":{\"duration\":\"1.0\"}}");
        Assert.Equal(1, info.DurationSec);
        Assert.Empty(info.AudioStreams);
    }
}
