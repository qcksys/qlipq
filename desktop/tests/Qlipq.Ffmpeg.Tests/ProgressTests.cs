using Qlipq.Ffmpeg;
using Xunit;

namespace Qlipq.Ffmpeg.Tests;

public class ProgressTests
{
    [Fact]
    public void ParseTimecodeConvertsToSeconds()
    {
        Assert.Equal(1.5, Progress.ParseTimecode("00:00:01.500000"));
        Assert.Equal(3723, Progress.ParseTimecode("01:02:03"));
        Assert.Null(Progress.ParseTimecode("nope"));
    }

    [Fact]
    public void ParseProgressReadsOutTimeUsAndContinueState()
    {
        var chunk = string.Join("\n", "frame=120", "out_time_us=2500000", "out_time=00:00:02.500000", "progress=continue");
        var result = Progress.ParseProgress(chunk);
        Assert.Equal(2.5, result.OutTimeSec);
        Assert.False(result.Done);
    }

    [Fact]
    public void ParseProgressDetectsEndMarker()
    {
        var result = Progress.ParseProgress("out_time_us=10000000\nprogress=end\n");
        Assert.Equal(10, result.OutTimeSec);
        Assert.True(result.Done);
    }

    [Fact]
    public void ParseProgressFallsBackToOutTimeTimecode()
    {
        var result = Progress.ParseProgress("out_time=00:00:04.000000\nprogress=continue");
        Assert.Equal(4, result.OutTimeSec);
    }

    [Fact]
    public void ProgressFractionClampsToUnitInterval()
    {
        Assert.Equal(0.5, Progress.ProgressFraction(5, 10));
        Assert.Equal(1, Progress.ProgressFraction(20, 10));
        Assert.Equal(0, Progress.ProgressFraction(null, 10));
        Assert.Equal(0, Progress.ProgressFraction(5, 0));
    }
}
