using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Qlipq.App.Services;
using Qlipq.Core;
using Qlipq.Ffmpeg;
using Qlipq.Host;

namespace Qlipq.App.ViewModels;

/// <summary>The clip editor (Editor.tsx): preview, trim/crop/audio/quality, estimate and export.</summary>
public sealed partial class EditorViewModel : ObservableObject, IDisposable
{
    private readonly MediaProbe _probe;
    private readonly ExportRunner _export;
    private readonly DialogService _dialogs;
    private readonly PlaybackStore _playback;
    private readonly DispatcherQueue _dispatcher;

    // The LibVLC + MediaPlayer are created and owned by EditorView (WinUI VideoView model:
    // the player must be built from the VideoView.Initialized SwapChainOptions). The VM only
    // drives the shared player it is attached to.
    private LibVLC? _libVlc;
    private EventHandler<MediaPlayerTimeChangedEventArgs>? _timeHandler;
    private EventHandler<EventArgs>? _playingHandler;

    private QueueItemViewModel _item = null!;
    private ShellViewModel _shell = null!;
    private AppConfig _config = null!;
    private bool _loaded;
    private double _restoreTime;

    public EditorViewModel(MediaProbe probe, ExportRunner export, DialogService dialogs, PlaybackStore playback)
    {
        _probe = probe;
        _export = export;
        _dialogs = dialogs;
        _playback = playback;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    [ObservableProperty] private MediaInfo? _media;
    [ObservableProperty] private string? _loadError;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private MediaPlayer? _mediaPlayer;
    [ObservableProperty] private double _currentTime;
    [ObservableProperty] private double _duration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    [NotifyPropertyChangedFor(nameof(HasExport))]
    [NotifyPropertyChangedFor(nameof(ExportButtonText))]
    private bool _exporting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    [NotifyPropertyChangedFor(nameof(ExportButtonText))]
    private double _progress;

    [ObservableProperty] private string _newTag = "";

    public ObservableCollection<AudioTrackViewModel> AudioTracks { get; } = [];
    public ObservableCollection<string> Tags { get; } = [];

    public int ProgressPercent => (int)Math.Round(Progress * 100);
    public string ExportButtonText => Exporting ? $"Exporting {ProgressPercent}%" : "Export clip";
    public bool HasError => LoadError is not null;
    public bool HasAudio => AudioTracks.Count > 0;

    // ---- Trim ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrimLengthText))]
    private double _trimStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrimLengthText))]
    private double _trimEnd;

    public string TrimLengthText => DateTimes.FormatDuration(Math.Max(0, TrimEnd - TrimStart));

    // ---- Crop ----

    [ObservableProperty] private bool _cropEnabled;
    [ObservableProperty] private double _cropX;
    [ObservableProperty] private double _cropY;
    [ObservableProperty] private double _cropWidth;
    [ObservableProperty] private double _cropHeight;
    [ObservableProperty] private double _sourceWidth;
    [ObservableProperty] private double _sourceHeight;
    public string SourceDimsText => Media is { } m ? $"{m.Width}×{m.Height} source" : "";

    // ---- Quality override ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverrideQualityModeKey))]
    [NotifyPropertyChangedFor(nameof(OverrideQualityPresetKey))]
    [NotifyPropertyChangedFor(nameof(OverrideCrf))]
    [NotifyPropertyChangedFor(nameof(OverrideVideoBitrateKbps))]
    [NotifyPropertyChangedFor(nameof(ShowOverridePreset))]
    [NotifyPropertyChangedFor(nameof(ShowOverrideCrf))]
    [NotifyPropertyChangedFor(nameof(ShowOverrideBitrate))]
    private bool _overrideEnabled;

    // ---- Derived ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    private string? _validationError;

    [ObservableProperty] private string _estimateText = "";
    [ObservableProperty] private string _outputDimsText = "";
    [ObservableProperty] private string _outputDurationText = "";

    public bool HasValidationError => ValidationError is not null;
    public bool CanExport => !Exporting && ValidationError is null && !string.IsNullOrEmpty(_config?.OutputFolder);
    public bool HasExport => _item?.ExportPath is not null && !Exporting;

    public void Load(QueueItemViewModel item, ShellViewModel shell)
    {
        _item = item;
        _shell = shell;
        _config = shell.Config.Config;
        OverrideEnabled = item.OutputOverride is not null;
        Tags.Clear();
        foreach (var t in item.Tags) Tags.Add(t);
        _ = LoadMediaAsync();
    }

    private async Task LoadMediaAsync()
    {
        IsLoading = true;
        LoadError = null;
        try
        {
            var info = await _probe.ProbeAsync(_item.Path, _config.FfprobePath);
            Media = info;
            _item.DurationSec = info.DurationSec;
            Duration = info.DurationSec;
            SourceWidth = info.Width;
            SourceHeight = info.Height;
            OnPropertyChanged(nameof(SourceDimsText));

            var spec = _item.Edit ?? new EditSpec
            {
                AudioTracks = EditSpecs.DefaultEditSpec(info).AudioTracks,
                Trim = new TrimSpec { StartSec = 0, EndSec = info.DurationSec },
            };

            // Carry audio enable/levels from the previous clip when this one is fresh.
            if (_item.Edit is null && _shell.AudioDefaults.Count > 0)
            {
                foreach (var track in spec.AudioTracks)
                {
                    var carried = _shell.AudioDefaults.FirstOrDefault(d => d.Index == track.Index);
                    if (carried is not null) { track.Enabled = carried.Enabled; track.Volume = carried.Volume; }
                }
            }

            _loaded = false;
            TrimStart = spec.Trim?.StartSec ?? 0;
            TrimEnd = spec.Trim?.EndSec ?? info.DurationSec;
            if (spec.Crop is { } crop)
            {
                CropEnabled = true;
                CropX = crop.X; CropY = crop.Y; CropWidth = crop.Width; CropHeight = crop.Height;
            }
            else { CropEnabled = false; CropWidth = info.Width; CropHeight = info.Height; }

            AudioTracks.Clear();
            foreach (var stream in info.AudioStreams)
            {
                var ts = spec.AudioTracks.FirstOrDefault(t => t.Index == stream.Index)
                         ?? new AudioTrackSpec { Index = stream.Index, Enabled = true, Volume = 1 };
                AudioTracks.Add(new AudioTrackViewModel(stream, ts, OnAudioChanged));
            }
            OnPropertyChanged(nameof(HasAudio));

            _loaded = true;
            TrySetMedia();
            RecomputeDerived();
        }
        catch (Exception e)
        {
            LoadError = e.Message;
            OnPropertyChanged(nameof(HasError));
        }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// Attach the EditorView's LibVLC + MediaPlayer (built from the VideoView's swapchain options).
    /// Called by the view; the VM then drives this shared player.
    /// </summary>
    public void AttachPlayer(LibVLC libVlc, MediaPlayer player)
    {
        DetachPlayer();
        _libVlc = libVlc;
        MediaPlayer = player;

        _timeHandler = (_, e) => _dispatcher.TryEnqueue(() =>
        {
            CurrentTime = e.Time / 1000.0;
            _playback.Set(_item.Path, CurrentTime);
        });
        _playingHandler = (_, _) => _dispatcher.TryEnqueue(() =>
        {
            if (_restoreTime > 0 && _restoreTime < Duration)
            {
                player.Time = (long)(_restoreTime * 1000);
                _restoreTime = 0;
            }
        });
        player.TimeChanged += _timeHandler;
        player.Playing += _playingHandler;

        TrySetMedia();
    }

    public void DetachPlayer()
    {
        if (MediaPlayer is { } p)
        {
            if (_timeHandler is not null) p.TimeChanged -= _timeHandler;
            if (_playingHandler is not null) p.Playing -= _playingHandler;
            try { p.Stop(); } catch { /* player may already be torn down */ }
        }
        _timeHandler = null;
        _playingHandler = null;
        MediaPlayer = null;
        _libVlc = null;
    }

    /// <summary>Load the clip into the attached player once both the player and probe are ready.</summary>
    private void TrySetMedia()
    {
        if (_libVlc is null || MediaPlayer is null || Media is null) return;
        using var media = new Media(_libVlc, _item.Path.Replace('/', '\\'), FromType.FromPath);
        MediaPlayer.Media = media;
        _restoreTime = _playback.Get(_item.Path);
        ApplyAudioPreview();
    }

    // ---- Transport ----

    [RelayCommand]
    private void PlayPause()
    {
        if (MediaPlayer is null) return;
        if (MediaPlayer.IsPlaying) MediaPlayer.Pause();
        else MediaPlayer.Play();
    }

    [RelayCommand]
    private void Skip(string deltaText)
    {
        if (Media is null || !double.TryParse(deltaText, out var delta)) return;
        Seek(Math.Min(Media.DurationSec, Math.Max(0, CurrentTime + delta)));
    }

    public void Seek(double sec)
    {
        CurrentTime = sec;
        if (MediaPlayer is not null) MediaPlayer.Time = (long)(sec * 1000);
    }

    [RelayCommand] private void SetInAtPlayhead() => TrimStart = ClampStart(CurrentTime);
    [RelayCommand] private void SetOutAtPlayhead() => TrimEnd = ClampEnd(CurrentTime);

    private double ClampStart(double v) => Math.Clamp(Math.Min(v, TrimEnd - 0.1), 0, Duration);
    private double ClampEnd(double v) => Math.Clamp(Math.Max(v, TrimStart + 0.1), 0, Duration);

    // ---- Reactions to edits ----

    partial void OnTrimStartChanged(double value) => OnSpecChanged();
    partial void OnTrimEndChanged(double value) => OnSpecChanged();
    partial void OnCropEnabledChanged(bool value)
    {
        if (value && Media is { } m && CropWidth <= 0) { CropX = 0; CropY = 0; CropWidth = m.Width; CropHeight = m.Height; }
        OnSpecChanged();
    }
    partial void OnCropXChanged(double value) => OnSpecChanged();
    partial void OnCropYChanged(double value) => OnSpecChanged();
    partial void OnCropWidthChanged(double value) => OnSpecChanged();
    partial void OnCropHeightChanged(double value) => OnSpecChanged();

    private void OnAudioChanged()
    {
        _shell.AudioDefaults = AudioTracks.Select(t => t.ToSpec()).ToList();
        ApplyAudioPreview();
        OnSpecChanged();
    }

    private void OnSpecChanged()
    {
        if (!_loaded || Media is null) return;
        _item.Edit = BuildSpec();
        _shell.PersistItemEdit(_item);
        RecomputeDerived();
    }

    private EditSpec BuildSpec() => new()
    {
        Trim = new TrimSpec { StartSec = TrimStart, EndSec = TrimEnd },
        Crop = CropEnabled
            ? new CropSpec { X = (int)CropX, Y = (int)CropY, Width = (int)CropWidth, Height = (int)CropHeight }
            : null,
        AudioTracks = AudioTracks.Select(t => t.ToSpec()).ToList(),
    };

    private void RecomputeDerived()
    {
        if (Media is not { } media) return;
        var spec = BuildSpec();
        ValidationError = EditSpecs.ValidateEditSpec(spec, media);

        var output = EffectiveOutput();
        var estimate = Estimate.EstimateExportSize(media, spec, Encode.OutputSettingsToEncode(output, media));
        EstimateText = $"{(estimate.Approximate ? "≈" : "")}{MediaFormat.FormatBytes(estimate.Bytes)}";
        OutputDimsText = CropEnabled ? $"{(int)CropWidth}×{(int)CropHeight}" : $"{media.Width}×{media.Height}";
        OutputDurationText = DateTimes.FormatDuration(EditSpecs.EffectiveDuration(spec, media));
        OnPropertyChanged(nameof(CanExport));
    }

    private OutputSettings EffectiveOutput() => MergeOutput(_config.Output, _item.OutputOverride);

    private static OutputSettings MergeOutput(OutputSettings b, OutputOverride? o) => o is null ? b : new OutputSettings
    {
        QualityMode = o.QualityMode ?? b.QualityMode,
        QualityPreset = o.QualityPreset ?? b.QualityPreset,
        Crf = o.Crf ?? b.Crf,
        VideoBitrateKbps = o.VideoBitrateKbps ?? b.VideoBitrateKbps,
        EncoderPreset = o.EncoderPreset ?? b.EncoderPreset,
        VideoCodec = o.VideoCodec ?? b.VideoCodec,
        Container = o.Container ?? b.Container,
        Fps = o.Fps ?? b.Fps,
        MaxHeight = o.MaxHeight ?? b.MaxHeight,
        AudioBitrateKbps = o.AudioBitrateKbps ?? b.AudioBitrateKbps,
    };

    private void ApplyAudioPreview()
    {
        if (MediaPlayer is null) return;
        var enabled = AudioTracks.Where(t => t.Enabled).ToList();
        if (enabled.Count == 0) { MediaPlayer.Mute = true; return; }
        MediaPlayer.Mute = false;
        // LibVLC previews one track; reflect the primary enabled track's level (it can exceed 100%).
        MediaPlayer.Volume = (int)Math.Round(enabled[0].Volume * 100);
    }

    // ---- Crop / override / tags ----

    partial void OnOverrideEnabledChanged(bool value)
    {
        if (value)
        {
            var o = _config.Output;
            _item.OutputOverride = new OutputOverride
            {
                QualityMode = o.QualityMode,
                QualityPreset = o.QualityPreset,
                Crf = o.Crf,
                VideoBitrateKbps = o.VideoBitrateKbps,
            };
        }
        else _item.OutputOverride = null;
        _shell?.PersistItemEdit(_item);
        RecomputeDerived();
        OnPropertyChanged(nameof(OverrideQualityModeKey));
        OnPropertyChanged(nameof(ShowOverridePreset));
        OnPropertyChanged(nameof(ShowOverrideCrf));
        OnPropertyChanged(nameof(ShowOverrideBitrate));
    }

    private void SetOverride(Action<OutputOverride> patch)
    {
        var o = _item.OutputOverride ?? new OutputOverride();
        patch(o);
        _item.OutputOverride = o;
        _shell.PersistItemEdit(_item);
        RecomputeDerived();
    }

    public string OverrideQualityModeKey
    {
        get => EnumKeys.QualityMode(EffectiveOutput().QualityMode);
        set { SetOverride(o => o.QualityMode = EnumKeys.QualityMode(value)); OnPropertyChanged(); OnPropertyChanged(nameof(ShowOverridePreset)); OnPropertyChanged(nameof(ShowOverrideCrf)); OnPropertyChanged(nameof(ShowOverrideBitrate)); OnPropertyChanged(nameof(OverrideBitrateLabel)); }
    }

    public bool ShowOverridePreset => OverrideEnabled && EffectiveOutput().QualityMode == QualityMode.Preset;
    public bool ShowOverrideCrf => OverrideEnabled && EffectiveOutput().QualityMode is QualityMode.Crf or QualityMode.Vbr;
    public bool ShowOverrideBitrate => OverrideEnabled && EffectiveOutput().QualityMode is QualityMode.Bitrate or QualityMode.Vbr;
    public string OverrideBitrateLabel => EffectiveOutput().QualityMode == QualityMode.Vbr ? "Max bitrate (kbps)" : "Video bitrate (kbps)";

    public string OverrideQualityPresetKey
    {
        get => EnumKeys.QualityPreset(EffectiveOutput().QualityPreset);
        set { SetOverride(o => o.QualityPreset = EnumKeys.QualityPreset(value)); OnPropertyChanged(); }
    }

    public double OverrideCrf
    {
        get => EffectiveOutput().Crf;
        set { SetOverride(o => o.Crf = (int)Math.Clamp(value, 0, 51)); OnPropertyChanged(); }
    }

    public double OverrideVideoBitrateKbps
    {
        get => EffectiveOutput().VideoBitrateKbps;
        set { SetOverride(o => o.VideoBitrateKbps = (int)Math.Clamp(value, 100, 200000)); OnPropertyChanged(); }
    }

    [RelayCommand]
    private void AddTag()
    {
        var t = NewTag.Trim();
        if (t.Length > 0 && !Tags.Contains(t))
        {
            Tags.Add(t);
            CommitTags();
        }
        NewTag = "";
    }

    [RelayCommand]
    private void RemoveTag(string tag)
    {
        if (Tags.Remove(tag)) CommitTags();
    }

    private void CommitTags()
    {
        _item.Tags = [.. Tags];
        _shell.PersistItemEdit(_item);
        _shell.NotifyItemChanged();
    }

    // ---- Export ----

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Media is not { } media || ValidationError is not null || string.IsNullOrEmpty(_config.OutputFolder)) return;
        var output = EffectiveOutput();
        var (name, _) = Rename.SplitFileName(_item.FileName);
        var outName = BuildExportName(name, output.Container.Extension());
        var outputPath = AppPath.Join(_config.OutputFolder, outName);

        if (FileOps.FileExists(outputPath))
        {
            var choice = await _dialogs.ThreeWayAsync(
                "Overwrite existing file?",
                $"A file already exists at {outputPath}. Exporting will replace it.",
                "Overwrite", "Append timestamp", "Cancel");
            if (choice == ContentDialogResult.Primary) await RunExportAsync(outputPath);
            else if (choice == ContentDialogResult.Secondary) await RunExportAsync(AppendTimestamp(outputPath));
            return;
        }
        await RunExportAsync(outputPath);
    }

    private async Task RunExportAsync(string outputPath)
    {
        if (Media is not { } media) return;
        var output = EffectiveOutput();
        var enc = Encode.OutputSettingsToEncode(output, media);
        var spec = BuildSpec();
        var args = ExportArgs.BuildExportArgs(new BuildExportOptions
        {
            InputPath = _item.Path,
            OutputPath = outputPath,
            Spec = spec,
            Progress = true,
            Video = enc.Video,
            Audio = enc.Audio,
            Reencode = enc.Reencode,
            Metadata = string.IsNullOrEmpty(_item.Source) ? null : [new("game", _item.Source)],
        });

        Exporting = true;
        Progress = 0;
        _item.Status = QueueStatus.Exporting;
        _item.Error = null;
        _shell.NotifyItemChanged();

        var total = EditSpecs.EffectiveDuration(spec, media);
        var reporter = new Progress<ProgressUpdate>(u =>
            _dispatcher.TryEnqueue(() => Progress = Qlipq.Ffmpeg.Progress.ProgressFraction(u.OutTimeSec, total)));

        try
        {
            await _export.RunExportAsync(_config.FfmpegPath, args, reporter);
            Progress = 1;
            _item.Status = QueueStatus.Done;
            _item.ExportPath = outputPath;
            _shell.NotifyItemChanged();
            OnPropertyChanged(nameof(HasExport));
            await ApplyAfterExportAsync();
        }
        catch (Exception e)
        {
            _item.Status = QueueStatus.Error;
            _item.Error = e.Message;
            _shell.NotifyItemChanged();
        }
        finally
        {
            Exporting = false;
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(HasExport));
        }
    }

    private async Task ApplyAfterExportAsync()
    {
        var action = _config.AfterExport.Action;
        if (action == AfterExportAction.Prompt)
        {
            var chosen = await _dialogs.ChooseActionAsync(
                "Export complete", "What should happen to the original recording?", "Keep", "Rename", "Move…", "Delete");
            await RunAfterActionAsync(chosen switch
            {
                "Rename" => AfterExportAction.Rename,
                "Move…" => AfterExportAction.Move,
                "Delete" => AfterExportAction.Delete,
                _ => AfterExportAction.Nothing,
            });
            return;
        }
        await RunAfterActionAsync(action);
    }

    private async Task RunAfterActionAsync(AfterExportAction action)
    {
        try
        {
            if (action == AfterExportAction.Delete)
            {
                await Task.Run(() => FileOps.DeleteFile(_item.Path));
            }
            else if (action == AfterExportAction.Move)
            {
                var folder = _config.AfterExport.MoveFolder;
                if (string.IsNullOrEmpty(folder)) folder = await _dialogs.PickFolderAsync() ?? "";
                if (!string.IsNullOrEmpty(folder))
                    await Task.Run(() => FileOps.RenameFile(_item.Path, AppPath.Join(folder, AppPath.BaseName(_item.Path))));
            }
            else if (action == AfterExportAction.Rename)
            {
                var (name, ext) = Rename.SplitFileName(_item.FileName);
                var renamed = $"{_config.AfterExport.RenamePrefix}{name}{_config.AfterExport.RenameSuffix}{(ext.Length > 0 ? $".{ext}" : "")}";
                await Task.Run(() => FileOps.RenameFile(_item.Path, AppPath.Join(AppPath.DirName(_item.Path), renamed)));
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"after-export {action} failed: {e.Message}");
        }
    }

    [RelayCommand]
    private void ShowExportedFile()
    {
        if (_item.ExportPath is { } path) _dialogs.RevealInExplorer(path);
    }

    private string BuildExportName(string name, string ext)
    {
        var recordedAt = IsoTime.ToLocal(_item.RecordedAt);
        return Rename.BuildRenamedFileName(_config.NamingTemplate,
            new RenameVars { Name = name, Ext = ext, RecordedAt = recordedAt, Source = _item.Source });
    }

    private static string AppendTimestamp(string path)
    {
        var (name, ext) = Rename.SplitFileName(AppPath.BaseName(path));
        var stamped = $"{name}_{DateTimes.FormatDateTime(DateTime.Now)}{(ext.Length > 0 ? $".{ext}" : "")}";
        return AppPath.Join(AppPath.DirName(path), stamped);
    }

    // The MediaPlayer is owned by the view (it created it from the VideoView); the VM only detaches.
    public void Dispose() => DetachPlayer();
}
