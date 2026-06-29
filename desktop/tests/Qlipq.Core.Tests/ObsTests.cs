using Qlipq.Core;
using Xunit;

namespace Qlipq.Core.Tests;

public class ObsTests
{
    [Fact]
    public void ParsesObsDefaultRecordingFilename()
    {
        var result = Obs.ParseObsFilename("2024-01-31 18-09-05.mkv");
        Assert.NotNull(result.RecordedAt);
        Assert.Equal(2024, result.RecordedAt!.Value.Year);
        Assert.Equal(1, result.RecordedAt.Value.Month); // C# months are 1-based (JS getMonth() == 0)
        Assert.Equal(31, result.RecordedAt.Value.Day);
        Assert.Equal(18, result.RecordedAt.Value.Hour);
        Assert.Equal(9, result.RecordedAt.Value.Minute);
        Assert.Equal(5, result.RecordedAt.Value.Second);
        Assert.Null(result.Source);
        Assert.False(result.IsReplay);
    }

    [Fact]
    public void ParsesReplayBufferFilenameAndFlagsIt()
    {
        var result = Obs.ParseObsFilename("Replay 2024-12-01_07-30-00.mp4");
        Assert.True(result.IsReplay);
        Assert.Equal(7, result.RecordedAt!.Value.Hour);
        Assert.Null(result.Source);
    }

    [Fact]
    public void ExtractsLeadingGameOrSceneNameAsSource()
    {
        var result = Obs.ParseObsFilename("Apex Legends 2024-03-15 21-45-10.mkv");
        Assert.Equal("Apex Legends", result.Source);
        Assert.Equal(15, result.RecordedAt!.Value.Day);
    }

    [Fact]
    public void StripsReplayPrefixFromSourceLabel()
    {
        var result = Obs.ParseObsFilename("Replay - Valorant - 2024-05-05 12-00-00.mp4");
        Assert.True(result.IsReplay);
        Assert.Equal("Valorant", result.Source);
    }

    [Fact]
    public void SupportsDottedTimeSeparators()
    {
        var result = Obs.ParseObsFilename("2024-06-28 14.02.59.mov");
        Assert.Equal(2, result.RecordedAt!.Value.Minute);
    }

    [Fact]
    public void ReturnsNoTimestampForUnrecognisedName()
    {
        var result = Obs.ParseObsFilename("random-clip.mp4");
        Assert.Null(result.RecordedAt);
        Assert.False(result.IsReplay);
    }

    [Fact]
    public void InferGameReturnsPerGameSubfolderUnderRoot()
    {
        Assert.Equal("Counter-strike 2",
            Obs.InferGameFromPath("E:/Shadowplay", "E:/Shadowplay/Counter-strike 2/clip.mp4"));
    }

    [Fact]
    public void InferGameIgnoresFilesDirectlyInRoot()
    {
        Assert.Null(Obs.InferGameFromPath("E:/OBS Recordings", "E:/OBS Recordings/clip.mkv"));
    }

    [Fact]
    public void InferGameToleratesBackslashesAndTrailingSlash()
    {
        Assert.Equal("Deadlock", Obs.InferGameFromPath("E:\\Shadowplay\\", "E:\\Shadowplay\\Deadlock\\a.mp4"));
        Assert.Equal("Deadlock", Obs.InferGameFromPath("E:/Shadowplay", "E:/Shadowplay/Deadlock\\a.mp4"));
    }

    [Fact]
    public void InferGameMatchesRootCaseInsensitively()
    {
        Assert.Equal("Apex", Obs.InferGameFromPath("e:/shadowplay", "E:/Shadowplay/Apex/a.mp4"));
    }

    [Fact]
    public void InferGameReturnsNullWhenNotUnderRoot()
    {
        Assert.Null(Obs.InferGameFromPath("E:/Shadowplay", "D:/Other/x.mp4"));
    }
}
