using Qlipq.Core;
using Xunit;

namespace Qlipq.Core.Tests;

public class DetectTests
{
    // Trimmed from real OBS files on a Windows install. Note the leading UTF-8 BOM
    // and CRLF endings, which OBS writes and the parser must tolerate.
    private const string UserIni =
        "﻿[General]\r\nFirstRun=true\r\n\r\n[Basic]\r\nProfile=Default\r\nProfileDir=Default\r\n";

    private const string AdvancedBasic =
        "﻿[General]\r\nName=Default\r\n\r\n[Output]\r\nMode=Advanced\r\n\r\n" +
        "[SimpleOutput]\r\nFilePath=E:/Simple Path\r\n\r\n" +
        "[AdvOut]\r\nRecType=Standard\r\nRecFilePath=E:/OBS Recordings\r\nRecFormat2=mp4\r\n";

    private const string SimpleBasic = "﻿[Output]\r\nMode=Simple\r\n\r\n[SimpleOutput]\r\nFilePath=D:/Clips\r\n";

    [Fact]
    public void AdvancedModeUsesAdvOutRecFilePath()
    {
        Assert.Equal("E:/OBS Recordings", Detect.DetectObsRecordingFolder(
            new ObsConfigFiles { UserIni = UserIni, Profiles = new() { ["Default"] = AdvancedBasic } }));
    }

    [Fact]
    public void SimpleModeUsesSimpleOutputFilePath()
    {
        Assert.Equal("D:/Clips", Detect.DetectObsRecordingFolder(
            new ObsConfigFiles { UserIni = UserIni, Profiles = new() { ["Default"] = SimpleBasic } }));
    }

    [Fact]
    public void MissingModeFallsBackToSimpleOutputPath()
    {
        var noMode = "﻿[SimpleOutput]\r\nFilePath=C:/Recordings\r\n";
        Assert.Equal("C:/Recordings", Detect.DetectObsRecordingFolder(
            new ObsConfigFiles { UserIni = null, Profiles = new() { ["Default"] = noMode } }));
    }

    [Fact]
    public void ActiveProfileSelectedByProfileDirAmongSeveral()
    {
        var result = Detect.DetectObsRecordingFolder(new ObsConfigFiles
        {
            UserIni = "[Basic]\nProfileDir=Gaming\n",
            Profiles = new()
            {
                ["Default"] = AdvancedBasic,
                ["Gaming"] = "[Output]\nMode=Simple\n[SimpleOutput]\nFilePath=G:/Gaming\n",
            },
        });
        Assert.Equal("G:/Gaming", result);
    }

    [Fact]
    public void ProfileDirMatchedCaseInsensitively()
    {
        var result = Detect.DetectObsRecordingFolder(new ObsConfigFiles
        {
            UserIni = "[Basic]\nProfileDir=default\n",
            Profiles = new() { ["Default"] = AdvancedBasic },
        });
        Assert.Equal("E:/OBS Recordings", result);
    }

    [Fact]
    public void FallsBackToFirstProfileWhenUserIniAbsent()
    {
        Assert.Equal("D:/Clips", Detect.DetectObsRecordingFolder(
            new ObsConfigFiles { UserIni = null, Profiles = new() { ["Default"] = SimpleBasic } }));
    }

    [Fact]
    public void NoProfilesReturnsNull()
    {
        Assert.Null(Detect.DetectObsRecordingFolder(new ObsConfigFiles { UserIni = UserIni, Profiles = new() }));
    }

    [Fact]
    public void EmptyRecordingPathReturnsNull()
    {
        var empty = "[Output]\nMode=Advanced\n[AdvOut]\nRecFilePath=\n";
        Assert.Null(Detect.DetectObsRecordingFolder(
            new ObsConfigFiles { UserIni = null, Profiles = new() { ["Default"] = empty } }));
    }
}
