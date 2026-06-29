using CommunityToolkit.Mvvm.ComponentModel;
using Qlipq.Core;

namespace Qlipq.App.ViewModels;

/// <summary>Observable wrapper over a <see cref="QueueItem"/> for the queue list and editor.</summary>
public partial class QueueItemViewModel : ObservableObject
{
    public const string DismissedTag = "dismissed";

    public string Id { get; }
    public string AddedAt { get; }

    [ObservableProperty] private string _path;
    [ObservableProperty] private string _fileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private QueueStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    private string? _source;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    private string? _recordedAt;

    [ObservableProperty] private MediaInfo? _media;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    private long? _fileSizeBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    private string? _fileModifiedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetaLine))]
    private double? _durationSec;

    [ObservableProperty] private EditSpec? _edit;
    [ObservableProperty] private OutputOverride? _outputOverride;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTags))]
    [NotifyPropertyChangedFor(nameof(IsDismissed))]
    [NotifyPropertyChangedFor(nameof(DismissLabel))]
    private List<string> _tags;

    [ObservableProperty] private string? _exportPath;
    [ObservableProperty] private string? _error;

    public QueueItemViewModel(QueueItem model)
    {
        Id = model.Id;
        AddedAt = model.AddedAt;
        _path = model.Path;
        _fileName = model.FileName;
        _status = model.Status;
        _source = model.Source;
        _recordedAt = model.RecordedAt;
        _media = model.Media;
        _fileSizeBytes = model.FileSizeBytes;
        _fileModifiedAt = model.FileModifiedAt;
        _durationSec = model.DurationSec;
        _edit = model.Edit;
        _outputOverride = model.OutputOverride;
        _tags = model.Tags ?? [];
        _exportPath = model.ExportPath;
        _error = model.Error;
    }

    public string StatusLabel => Status switch
    {
        QueueStatus.Pending => "Pending",
        QueueStatus.Ready => "Ready",
        QueueStatus.Editing => "Editing",
        QueueStatus.Exporting => "Exporting",
        QueueStatus.Done => "Done",
        QueueStatus.Error => "Error",
        _ => Status.ToString(),
    };

    public bool HasTags => Tags.Count > 0;
    public bool IsDismissed => Tags.Contains(DismissedTag);
    public string DismissLabel => IsDismissed ? "Restore" : "Dismiss";

    /// <summary>One-line metadata summary for the queue card (matches the web's joined meta).</summary>
    public string MetaLine
    {
        get
        {
            var when = IsoTime.ToLocal(RecordedAt) ?? IsoTime.ToLocal(FileModifiedAt);
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Source)) parts.Add(Source);
            parts.Add(when is { } w ? $"{DateTimes.FormatDate(w)} {DateTimes.FormatTime(w)}" : "Unknown time");
            if (DurationSec is { } d) parts.Add(DateTimes.FormatDuration(d));
            if (FileSizeBytes is { } size) parts.Add(MediaFormat.FormatBytes(size));
            return string.Join(" · ", parts);
        }
    }

    public StoredEdit ToStoredEdit() => new() { Edit = Edit, OutputOverride = OutputOverride, Tags = Tags.Count > 0 ? Tags : null };
}
