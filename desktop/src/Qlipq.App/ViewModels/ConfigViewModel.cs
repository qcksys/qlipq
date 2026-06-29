using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qlipq.App.Services;
using Qlipq.Core;
using Qlipq.Host;

namespace Qlipq.App.ViewModels;

public sealed record PresetOption(string Label, string Folder)
{
    public string Display => $"+ {Label} ({Folder})";
}

/// <summary>
/// Settings view-model (ConfigPanel.tsx). Wraps the live <see cref="AppConfig"/>; every setter
/// updates the config and fires <see cref="Changed"/> (debounce-saved by the shell). Folder/extension
/// edits also fire <see cref="WatchTargetsChanged"/> so the shell rescans.
/// </summary>
public partial class ConfigViewModel : ObservableObject
{
    public static readonly string[] EncoderPresets =
        ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"];

    private readonly AppConfig _config;
    private readonly DialogService _dialogs;
    private readonly ProcessRunner _runner;

    public AppConfig Config => _config;

    /// <summary>Raised after any setting changes (the shell debounce-saves config.json).</summary>
    public event Action? Changed;

    /// <summary>Raised when watched folders or video extensions change (the shell rescans).</summary>
    public event Action? WatchTargetsChanged;

    /// <summary>Raised to request a single-folder reprocess (the shell scans and switches to the queue).</summary>
    public event Action<string>? ReprocessRequested;

    public ConfigViewModel(AppConfig config, DialogService dialogs, ProcessRunner runner)
    {
        _config = config;
        _dialogs = dialogs;
        _runner = runner;
        WatchedFolders = new ObservableCollection<string>(config.WatchedFolders);
        RefreshPresetOptions();
    }

    public ObservableCollection<string> WatchedFolders { get; }
    public ObservableCollection<PresetOption> PresetOptions { get; } = [];

    private CapturePresets _presets = new();
    public void SetPresets(CapturePresets presets)
    {
        _presets = presets;
        RefreshPresetOptions();
    }

    private void RefreshPresetOptions()
    {
        PresetOptions.Clear();
        if (!string.IsNullOrEmpty(_presets.Obs) && !_config.WatchedFolders.Contains(_presets.Obs))
            PresetOptions.Add(new PresetOption("OBS", _presets.Obs));
        if (!string.IsNullOrEmpty(_presets.NvidiaShare) && !_config.WatchedFolders.Contains(_presets.NvidiaShare))
            PresetOptions.Add(new PresetOption("NVIDIA Share", _presets.NvidiaShare));
    }

    private void Touch() => Changed?.Invoke();

    public string OutputFolder
    {
        get => _config.OutputFolder;
        set { if (_config.OutputFolder != value) { _config.OutputFolder = value; OnPropertyChanged(); Touch(); } }
    }

    public string NamingTemplate
    {
        get => _config.NamingTemplate;
        set { if (_config.NamingTemplate != value) { _config.NamingTemplate = value; OnPropertyChanged(); Touch(); } }
    }

    public string FfmpegPath
    {
        get => _config.FfmpegPath;
        set { if (_config.FfmpegPath != value) { _config.FfmpegPath = value; OnPropertyChanged(); Touch(); } }
    }

    public string FfprobePath
    {
        get => _config.FfprobePath;
        set { if (_config.FfprobePath != value) { _config.FfprobePath = value; OnPropertyChanged(); Touch(); } }
    }

    // ---- Output defaults (string keys map cleanly to ComboBox SelectedValue/Tag) ----

    private OutputSettings Out => _config.Output;

    public string QualityModeKey
    {
        get => EnumKeys.QualityMode(Out.QualityMode);
        set { Out.QualityMode = EnumKeys.QualityMode(value); OnPropertyChanged(); OnPropertyChanged(nameof(ShowPreset)); OnPropertyChanged(nameof(ShowCrf)); OnPropertyChanged(nameof(ShowBitrate)); OnPropertyChanged(nameof(BitrateLabel)); Touch(); }
    }

    public bool ShowPreset => Out.QualityMode == QualityMode.Preset;
    public bool ShowCrf => Out.QualityMode is QualityMode.Crf or QualityMode.Vbr;
    public bool ShowBitrate => Out.QualityMode is QualityMode.Bitrate or QualityMode.Vbr;
    public string BitrateLabel => Out.QualityMode == QualityMode.Vbr ? "Max bitrate (kbps)" : "Video bitrate (kbps)";

    public string QualityPresetKey
    {
        get => EnumKeys.QualityPreset(Out.QualityPreset);
        set { Out.QualityPreset = EnumKeys.QualityPreset(value); OnPropertyChanged(); Touch(); }
    }

    public int Crf
    {
        get => Out.Crf;
        set { var v = Math.Clamp(value, 0, 51); if (Out.Crf != v) { Out.Crf = v; OnPropertyChanged(); Touch(); } }
    }

    public int VideoBitrateKbps
    {
        get => Out.VideoBitrateKbps;
        set { var v = Math.Clamp(value, 100, 200000); if (Out.VideoBitrateKbps != v) { Out.VideoBitrateKbps = v; OnPropertyChanged(); Touch(); } }
    }

    public IReadOnlyList<string> EncoderPresetsList => EncoderPresets;

    public string EncoderPreset
    {
        get => Out.EncoderPreset;
        set { if (Out.EncoderPreset != value) { Out.EncoderPreset = value; OnPropertyChanged(); Touch(); } }
    }

    public string VideoCodecKey
    {
        get => EnumKeys.VideoCodec(Out.VideoCodec);
        set { Out.VideoCodec = EnumKeys.VideoCodec(value); OnPropertyChanged(); Touch(); }
    }

    public string ContainerKey
    {
        get => EnumKeys.Container(Out.Container);
        set { Out.Container = EnumKeys.Container(value); OnPropertyChanged(); Touch(); }
    }

    public string FpsKey
    {
        get => Out.Fps.ToString();
        set { Out.Fps = int.TryParse(value, out var v) ? v : 0; OnPropertyChanged(); Touch(); }
    }

    public string MaxHeightKey
    {
        get => Out.MaxHeight.ToString();
        set { Out.MaxHeight = int.TryParse(value, out var v) ? v : 0; OnPropertyChanged(); Touch(); }
    }

    public string AudioBitrateKey
    {
        get => Out.AudioBitrateKbps.ToString();
        set { Out.AudioBitrateKbps = int.TryParse(value, out var v) ? v : 192; OnPropertyChanged(); Touch(); }
    }

    // ---- After export ----

    private AfterExportSettings After => _config.AfterExport;

    public string AfterActionKey
    {
        get => EnumKeys.AfterExport(After.Action);
        set { After.Action = EnumKeys.AfterExport(value); OnPropertyChanged(); OnPropertyChanged(nameof(ShowMove)); OnPropertyChanged(nameof(ShowRename)); Touch(); }
    }

    public bool ShowMove => After.Action == AfterExportAction.Move;
    public bool ShowRename => After.Action == AfterExportAction.Rename;

    public string MoveFolder
    {
        get => After.MoveFolder;
        set { if (After.MoveFolder != value) { After.MoveFolder = value; OnPropertyChanged(); Touch(); } }
    }

    public string RenamePrefix
    {
        get => After.RenamePrefix;
        set { if (After.RenamePrefix != value) { After.RenamePrefix = value; OnPropertyChanged(); Touch(); } }
    }

    public string RenameSuffix
    {
        get => After.RenameSuffix;
        set { if (After.RenameSuffix != value) { After.RenameSuffix = value; OnPropertyChanged(); Touch(); } }
    }

    // ---- ffmpeg/ffprobe test results ----

    [ObservableProperty] private string? _ffmpegTest;
    [ObservableProperty] private bool _ffmpegOk = true;
    [ObservableProperty] private string? _ffprobeTest;
    [ObservableProperty] private bool _ffprobeOk = true;

    // ---- Commands ----

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync();
        if (folder is not null) AddWatchedFolder(folder);
    }

    public void AddWatchedFolder(string folder)
    {
        if (_config.WatchedFolders.Contains(folder)) return;
        _config.WatchedFolders.Add(folder);
        WatchedFolders.Add(folder);
        RefreshPresetOptions();
        Touch();
        WatchTargetsChanged?.Invoke();
    }

    [RelayCommand]
    private void RemoveFolder(string folder)
    {
        _config.WatchedFolders.Remove(folder);
        WatchedFolders.Remove(folder);
        RefreshPresetOptions();
        Touch();
        WatchTargetsChanged?.Invoke();
    }

    [RelayCommand]
    private void Reprocess(string folder) => ReprocessRequested?.Invoke(folder);

    [RelayCommand]
    private async Task PickOutputAsync()
    {
        var folder = await _dialogs.PickFolderAsync();
        if (folder is not null) OutputFolder = folder;
    }

    [RelayCommand]
    private async Task PickMoveFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync();
        if (folder is not null) MoveFolder = folder;
    }

    [RelayCommand]
    private async Task TestFfmpegAsync()
    {
        FfmpegOk = true;
        FfmpegTest = "Testing…";
        try { var v = await _runner.CheckBinaryAsync(FfmpegPath); FfmpegOk = true; FfmpegTest = string.IsNullOrEmpty(v) ? "OK" : v; }
        catch (Exception e) { FfmpegOk = false; FfmpegTest = e.Message; }
    }

    [RelayCommand]
    private async Task TestFfprobeAsync()
    {
        FfprobeOk = true;
        FfprobeTest = "Testing…";
        try { var v = await _runner.CheckBinaryAsync(FfprobePath); FfprobeOk = true; FfprobeTest = string.IsNullOrEmpty(v) ? "OK" : v; }
        catch (Exception e) { FfprobeOk = false; FfprobeTest = e.Message; }
    }
}
