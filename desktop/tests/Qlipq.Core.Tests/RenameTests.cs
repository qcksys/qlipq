using Qlipq.Core;
using Xunit;

namespace Qlipq.Core.Tests;

public class RenameTests
{
    private static readonly DateTime RecordedAt = new(2024, 1, 31, 18, 9, 5, DateTimeKind.Local);

    [Fact]
    public void ExpandsDateSourceNameTokens()
    {
        var outName = Rename.ApplyNamingTemplate("{date}_{source}_{name}",
            new RenameVars { Name = "raw", Ext = "mp4", RecordedAt = RecordedAt, Source = "Apex" });
        Assert.Equal("2024-01-31_Apex_raw", outName);
    }

    [Fact]
    public void CollapsesSeparatorsWhenATokenIsEmpty()
    {
        var outName = Rename.ApplyNamingTemplate("{date}_{source}_{name}",
            new RenameVars { Name = "raw", Ext = "mp4", RecordedAt = RecordedAt });
        Assert.Equal("2024-01-31_raw", outName);
    }

    [Fact]
    public void PreservesOriginalExtensionWhenBuildingFilename()
    {
        var outName = Rename.BuildRenamedFileName("{datetime}",
            new RenameVars { Name = "raw", Ext = "MKV", RecordedAt = RecordedAt });
        Assert.Equal("2024-01-31_18-09-05.MKV", outName);
    }

    [Fact]
    public void FallsBackToClipWhenEverythingResolvesAway()
    {
        var outName = Rename.ApplyNamingTemplate("{source}", new RenameVars { Name = "x", Ext = "mp4" });
        Assert.Equal("clip", outName);
    }

    [Fact]
    public void SanitizesIllegalCharactersButKeepsDashes()
    {
        Assert.Equal("a_b_c_d-2024-01-01", Rename.SanitizeFileName("a:b/c?d-2024-01-01"));
    }

    [Fact]
    public void SplitFileNameSeparatesBaseAndExtension()
    {
        Assert.Equal(("clip.final", "mp4"), Rename.SplitFileName("clip.final.mp4"));
        Assert.Equal(("noext", ""), Rename.SplitFileName("noext"));
    }

    [Fact]
    public void IndexTokenRendersOneBasedPosition()
    {
        var outName = Rename.ApplyNamingTemplate("{name}-{index}",
            new RenameVars { Name = "clip", Ext = "mp4", Index = 3 });
        Assert.Equal("clip-3", outName);
    }
}
