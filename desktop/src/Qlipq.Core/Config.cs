namespace Qlipq.Core;

/// <summary>How output video quality/bitrate is controlled. <c>Vbr</c> = CRF capped by a max bitrate.</summary>
public enum QualityMode { Preset, Crf, Bitrate, Vbr }

/// <summary>Named quality presets; <c>Original</c> stream-copies when possible.</summary>
public enum QualityPreset { Original, High, Balanced, Small }

public enum VideoCodecChoice { Libx264, Libx265 }

public enum ContainerFormat { Mp4, Mkv }

/// <summary>What to do with the source recording after a successful export.</summary>
public enum AfterExportAction { Nothing, Delete, Move, Rename, Prompt }

/// <summary>Default encoding settings applied to every export.</summary>
public sealed record OutputSettings
{
    public QualityMode QualityMode { get; set; } = QualityMode.Preset;

    /// <summary>Used when <see cref="QualityMode"/> is <c>Preset</c>.</summary>
    public QualityPreset QualityPreset { get; set; } = QualityPreset.Original;

    /// <summary>Constant Rate Factor (0–51, lower = better); used when <c>QualityMode</c> is <c>Crf</c>.</summary>
    public int Crf { get; set; } = 20;

    /// <summary>Target video bitrate in kbps; used when <c>QualityMode</c> is <c>Bitrate</c>.</summary>
    public int VideoBitrateKbps { get; set; } = 8000;

    /// <summary>x26x encoder speed preset, e.g. <c>veryfast</c>.</summary>
    public string EncoderPreset { get; set; } = "veryfast";

    public VideoCodecChoice VideoCodec { get; set; } = VideoCodecChoice.Libx264;
    public ContainerFormat Container { get; set; } = ContainerFormat.Mp4;

    /// <summary>Target frame rate; 0 keeps the source rate. Never up-rates.</summary>
    public int Fps { get; set; }

    /// <summary>Downscale so height ≤ this many pixels; 0 keeps the source size. Never up-scales.</summary>
    public int MaxHeight { get; set; }

    public int AudioBitrateKbps { get; set; } = 192;
}

public sealed record AfterExportSettings
{
    public AfterExportAction Action { get; set; } = AfterExportAction.Nothing;

    /// <summary>Destination folder for the <c>Move</c> action.</summary>
    public string MoveFolder { get; set; } = "";

    /// <summary>Prefix/suffix added to the source file name for the <c>Rename</c> action.</summary>
    public string RenamePrefix { get; set; } = "";
    public string RenameSuffix { get; set; } = "";
}

/// <summary>Persisted application configuration.</summary>
public sealed record AppConfig
{
    /// <summary>Folders watched for new recordings.</summary>
    public List<string> WatchedFolders { get; set; } = [];

    /// <summary>Where exported clips are written.</summary>
    public string OutputFolder { get; set; } = "";

    /// <summary>Lower-case extensions (no dot) considered video files.</summary>
    public List<string> VideoExtensions { get; set; } = [.. Config.DefaultVideoExtensions];

    /// <summary>Naming template applied on rename/export. See <see cref="Rename.ApplyNamingTemplate"/>.</summary>
    public string NamingTemplate { get; set; } = "{date}_{source}_{name}";

    /// <summary>Path or command name for ffmpeg.</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>Path or command name for ffprobe.</summary>
    public string FfprobePath { get; set; } = "ffprobe";

    /// <summary>What to do with the source recording after a successful export.</summary>
    public AfterExportSettings AfterExport { get; set; } = new();

    /// <summary>Default encoding settings applied to every export.</summary>
    public OutputSettings Output { get; set; } = new();
}

public static class Config
{
    /// <summary>Container extensions qlipq treats as editable video by default.</summary>
    public static readonly string[] DefaultVideoExtensions = ["mp4", "mkv", "mov", "flv", "webm", "ts"];

    /// <summary>True if the file extension (case-insensitive) is one of the configured video types.</summary>
    public static bool IsVideoFile(string fileName, IEnumerable<string> videoExtensions)
    {
        var dot = fileName.LastIndexOf('.');
        if (dot < 0) return false;
        var ext = fileName[(dot + 1)..].ToLowerInvariant();
        return videoExtensions.Contains(ext);
    }

    /// <summary>
    /// Merge a partial (e.g. loaded from disk) over defaults, so new fields gain defaults.
    /// Mirrors <c>withConfigDefaults</c>: shallow at the top level, one level deep on
    /// <see cref="AppConfig.Output"/>/<see cref="AppConfig.AfterExport"/>; arrays replace wholesale.
    /// </summary>
    public static AppConfig WithConfigDefaults(AppConfig? partial)
    {
        if (partial is null) return new AppConfig();
        return partial with
        {
            AfterExport = partial.AfterExport ?? new AfterExportSettings(),
            Output = partial.Output ?? new OutputSettings(),
        };
    }
}

public static class OutputSettingsFfmpeg
{
    /// <summary>The ffmpeg encoder token for a codec choice (e.g. <c>libx264</c>).</summary>
    public static string ToFfmpegCodec(this VideoCodecChoice codec) => codec switch
    {
        VideoCodecChoice.Libx264 => "libx264",
        VideoCodecChoice.Libx265 => "libx265",
        _ => "libx264",
    };

    /// <summary>The container file extension (no dot), e.g. <c>mp4</c>.</summary>
    public static string Extension(this ContainerFormat container) => container switch
    {
        ContainerFormat.Mp4 => "mp4",
        ContainerFormat.Mkv => "mkv",
        _ => "mp4",
    };
}
