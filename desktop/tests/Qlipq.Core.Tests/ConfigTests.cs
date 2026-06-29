using Qlipq.Core;
using Xunit;

namespace Qlipq.Core.Tests;

public class ConfigTests
{
    [Fact]
    public void ParseFillsInMissingFields()
    {
        var merged = ConfigJson.Parse("{\"outputFolder\":\"D:/out\"}");
        Assert.Equal("D:/out", merged.OutputFolder);
        Assert.Equal("ffmpeg", merged.FfmpegPath);
        Assert.Equal(new AppConfig().NamingTemplate, merged.NamingTemplate);
    }

    [Fact]
    public void ParseToleratesEmptyAndInvalidJson()
    {
        Assert.Equal("ffmpeg", ConfigJson.Parse("").FfmpegPath);
        Assert.Equal("{date}_{source}_{name}", ConfigJson.Parse("not json").NamingTemplate);
    }

    [Fact]
    public void WithConfigDefaultsToleratesNull()
    {
        // Compare by serialized form: AppConfig record equality treats its List<> members
        // by reference, so structurally-equal defaults are not Equal by value.
        Assert.Equal(
            ConfigJson.Serialize(new AppConfig()),
            ConfigJson.Serialize(Config.WithConfigDefaults(null)));
    }

    [Fact]
    public void ParseDeepMergesOutputKeepingDefaultsForAbsentSubFields()
    {
        var merged = ConfigJson.Parse("{\"output\":{\"qualityMode\":\"bitrate\"}}");
        Assert.Equal(QualityMode.Bitrate, merged.Output.QualityMode);
        // Untouched sub-fields fall back to defaults rather than becoming zero/null.
        Assert.Equal(new OutputSettings().AudioBitrateKbps, merged.Output.AudioBitrateKbps);
        Assert.Equal(ContainerFormat.Mp4, merged.Output.Container);
    }

    [Fact]
    public void ParseRepairsInvalidEnumsAndClampsCrf()
    {
        var merged = ConfigJson.Parse("{\"output\":{\"qualityPreset\":\"ultra\",\"crf\":99,\"videoCodec\":\"av1\"}}");
        Assert.Equal(QualityPreset.Original, merged.Output.QualityPreset); // invalid → default
        Assert.Equal(51, merged.Output.Crf); // clamped to range max
        Assert.Equal(VideoCodecChoice.Libx264, merged.Output.VideoCodec); // invalid → default
    }

    [Fact]
    public void SerializeStampsSchemaAndRoundTrips()
    {
        var json = ConfigJson.Serialize(new AppConfig { OutputFolder = "D:/out" });
        Assert.Contains("\"$schema\": \"https://qlipq.com/schema/config.json\"", json);
        Assert.Contains("\"qualityPreset\": \"original\"", json);
        Assert.Contains("\"videoCodec\": \"libx264\"", json);

        var round = ConfigJson.Parse(json);
        Assert.Equal("D:/out", round.OutputFolder);
        Assert.Equal(QualityPreset.Original, round.Output.QualityPreset);
    }

    [Fact]
    public void FormatBytesRendersBinaryUnits()
    {
        Assert.Equal("0 B", MediaFormat.FormatBytes(0));
        Assert.Equal("512 B", MediaFormat.FormatBytes(512));
        Assert.Equal("1.0 KB", MediaFormat.FormatBytes(1024));
        Assert.Equal("1.5 MB", MediaFormat.FormatBytes(1024 * 1024 * 1.5));
        Assert.Equal("3.2 GB", MediaFormat.FormatBytes(3.2 * Math.Pow(1024, 3)));
    }

    [Fact]
    public void IsVideoFileMatchesConfiguredExtensionsCaseInsensitively()
    {
        var ext = new AppConfig().VideoExtensions;
        Assert.True(Config.IsVideoFile("clip.MKV", ext));
        Assert.True(Config.IsVideoFile("clip.mp4", ext));
        Assert.False(Config.IsVideoFile("notes.txt", ext));
        Assert.False(Config.IsVideoFile("noext", ext));
    }

    [Fact]
    public void FormatDurationRendersMinutesAndHours()
    {
        Assert.Equal("1:05", DateTimes.FormatDuration(65));
        Assert.Equal("1:02:05", DateTimes.FormatDuration(3725));
        Assert.Equal("0:00", DateTimes.FormatDuration(-5));
    }
}
