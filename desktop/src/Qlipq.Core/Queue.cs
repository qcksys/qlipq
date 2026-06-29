namespace Qlipq.Core;

/// <summary>Lifecycle of a clip in the editing queue.</summary>
public enum QueueStatus { Pending, Ready, Editing, Exporting, Done, Error }

/// <summary>
/// Per-clip output overrides (quality), merged over the global <see cref="OutputSettings"/>
/// on export. Every field is nullable so only explicitly-set values are persisted and applied
/// (the C# analog of TypeScript's <c>Partial&lt;OutputSettings&gt;</c>).
/// </summary>
public sealed record OutputOverride
{
    public QualityMode? QualityMode { get; set; }
    public QualityPreset? QualityPreset { get; set; }
    public int? Crf { get; set; }
    public int? VideoBitrateKbps { get; set; }
    public string? EncoderPreset { get; set; }
    public VideoCodecChoice? VideoCodec { get; set; }
    public ContainerFormat? Container { get; set; }
    public int? Fps { get; set; }
    public int? MaxHeight { get; set; }
    public int? AudioBitrateKbps { get; set; }
}

/// <summary>A recording tracked in the queue, with any parsed metadata and edit state.</summary>
public sealed record QueueItem
{
    public string Id { get; set; } = "";

    /// <summary>Absolute path on disk.</summary>
    public string Path { get; set; } = "";

    public string FileName { get; set; } = "";

    /// <summary>ISO timestamp of when it entered the queue.</summary>
    public string AddedAt { get; set; } = "";

    public QueueStatus Status { get; set; } = QueueStatus.Pending;

    /// <summary>ISO timestamp parsed from the filename, if any.</summary>
    public string? RecordedAt { get; set; }

    /// <summary>Scene/game label parsed from the filename, if any.</summary>
    public string? Source { get; set; }

    /// <summary>Probed media info, populated lazily when the clip is opened.</summary>
    public MediaInfo? Media { get; set; }

    /// <summary>File size in bytes, read from the filesystem.</summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>ISO timestamp of the file's last modification, from the filesystem.</summary>
    public string? FileModifiedAt { get; set; }

    /// <summary>Clip duration in seconds, probed lazily in the background for the queue.</summary>
    public double? DurationSec { get; set; }

    /// <summary>Working edit spec, persisted so re-opening a clip restores progress.</summary>
    public EditSpec? Edit { get; set; }

    /// <summary>Per-clip output overrides (quality), merged over the global defaults on export.</summary>
    public OutputOverride? OutputOverride { get; set; }

    /// <summary>Free-form labels for filtering the queue.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Where the last successful export was written.</summary>
    public string? ExportPath { get; set; }

    public string? Error { get; set; }
}
