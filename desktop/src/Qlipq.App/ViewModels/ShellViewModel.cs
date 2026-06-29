using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Qlipq.App.Services;
using Qlipq.Core;
using Qlipq.Host;

namespace Qlipq.App.ViewModels;

/// <summary>Top-level orchestrator (App.tsx): the queue, scanning/watching, persistence and navigation.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private const string EditsFile = "edits.json";
    private const string DeleteFlagFile = "delete-confirmed.flag";

    private readonly IServiceProvider _provider;
    private readonly ConfigStore _configStore;
    private readonly AppDataStore _appData;
    private readonly MediaProbe _probe;
    private readonly CaptureDetect _detect;
    private readonly FolderWatcher _watcher;
    private readonly DialogService _dialogs;
    private readonly ProcessRunner _runner;
    private readonly DispatcherQueue _dispatcher;

    private readonly HashSet<string> _knownPaths = [];
    private readonly Dictionary<string, StoredEdit> _editStore = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _probeGate = new(3);
    private readonly Debouncer _saveConfig = new();
    private readonly Debouncer _saveStore = new();

    private AppConfig _config = new();
    private bool _deleteConfirmed;

    public ConfigViewModel Config { get; private set; } = null!;
    public ObservableCollection<QueueItemViewModel> Items { get; } = [];
    public ObservableCollection<QueueItemViewModel> VisibleItems { get; } = [];
    public ObservableCollection<string> AllTags { get; } = [];

    /// <summary>Audio enable/levels carried over from the previously edited clip.</summary>
    public List<AudioTrackSpec> AudioDefaults { get; set; } = [];

    [ObservableProperty] private bool _isReady;
    [ObservableProperty] private string _currentView = "queue";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private QueueItemViewModel? _selectedItem;

    [ObservableProperty] private EditorViewModel? _editor;
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private string? _tagFilter;

    public bool HasSelection => SelectedItem is not null;
    public bool IsQueueView => CurrentView == "queue";
    public bool IsSettingsView => CurrentView == "settings";
    public bool HasWatchedFolders => _config.WatchedFolders.Count > 0;

    public ShellViewModel(
        IServiceProvider provider, ConfigStore configStore, AppDataStore appData, MediaProbe probe,
        CaptureDetect detect, FolderWatcher watcher, DialogService dialogs, ProcessRunner runner)
    {
        _provider = provider;
        _configStore = configStore;
        _appData = appData;
        _probe = probe;
        _detect = detect;
        _watcher = watcher;
        _dialogs = dialogs;
        _runner = runner;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    public async Task InitializeAsync()
    {
        _config = await _configStore.LoadAsync();
        Config = new ConfigViewModel(_config, _dialogs, _runner);
        Config.Changed += () => _saveConfig.Run(400, () => _ = _configStore.SaveAsync(_config));
        Config.WatchTargetsChanged += () => _ = LoadFromFoldersAsync(_config.WatchedFolders, _config.VideoExtensions);
        Config.ReprocessRequested += folder => _ = ReprocessFolderAsync(folder);
        OnPropertyChanged(nameof(Config));

        var storeText = await _appData.ReadAsync(EditsFile);
        if (!string.IsNullOrEmpty(storeText))
        {
            try
            {
                var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, StoredEdit>>(storeText, QlipqJson.Options);
                if (loaded is not null) foreach (var (k, v) in loaded) _editStore[k] = v;
            }
            catch (System.Text.Json.JsonException) { /* ignore corrupt store */ }
        }

        _deleteConfirmed = await _appData.ReadAsync(DeleteFlagFile) == "1";

        _watcher.FileAdded += path => _dispatcher.TryEnqueue(() => AddPaths([path]));
        IsReady = true;
        OnPropertyChanged(nameof(HasWatchedFolders));

        await LoadFromFoldersAsync(_config.WatchedFolders, _config.VideoExtensions);

        // Best-effort capture-folder preset detection (off the UI thread).
        _ = Task.Run(() =>
        {
            var presets = _detect.DetectCapturePresets();
            _dispatcher.TryEnqueue(() => Config.SetPresets(presets));
        });
    }

    private async Task LoadFromFoldersAsync(IReadOnlyList<string> folders, IReadOnlyList<string> extensions)
    {
        if (folders.Count > 0)
        {
            var found = await Task.Run(() => Scanner.ScanFolders(folders, extensions));
            AddPaths(found);
        }
        _watcher.Start(folders, extensions);
    }

    /// <summary>Dedup, build queue items, hydrate file info and enqueue duration probes. Returns # added.</summary>
    private int AddPaths(IReadOnlyList<string> paths)
    {
        var fresh = new List<string>();
        foreach (var raw in paths)
        {
            var path = PathUtil.ToPosix(raw);
            if (_knownPaths.Add(path)) fresh.Add(path);
        }
        if (fresh.Count == 0) return 0;

        var roots = _config.WatchedFolders;
        var index = 0;
        foreach (var path in fresh)
        {
            var vm = BuildItem(path, roots);
            if (_editStore.TryGetValue(path, out var stored)) ApplyStored(vm, stored);
            Items.Insert(index++, vm);
        }
        RebuildVisible();
        _ = HydrateFileInfoAsync(fresh);
        foreach (var path in fresh) _ = ProbeDurationAsync(path);
        return fresh.Count;
    }

    private static QueueItemViewModel BuildItem(string path, IReadOnlyList<string> roots)
    {
        var fileName = PathBaseName(path);
        var parsed = Obs.ParseObsFilename(fileName);
        var game = roots.Select(r => Obs.InferGameFromPath(r, path)).FirstOrDefault(g => g is not null);
        return new QueueItemViewModel(new QueueItem
        {
            Id = Ids.CreateId(),
            Path = path,
            FileName = fileName,
            AddedAt = IsoTime.UtcNow(),
            Status = QueueStatus.Pending,
            RecordedAt = parsed.RecordedAt is { } r ? IsoTime.FromLocal(r) : null,
            Source = parsed.Source ?? game,
        });
    }

    private static void ApplyStored(QueueItemViewModel vm, StoredEdit stored)
    {
        vm.Edit = stored.Edit;
        vm.OutputOverride = stored.OutputOverride;
        if (stored.Tags is not null) vm.Tags = stored.Tags;
    }

    private async Task HydrateFileInfoAsync(IReadOnlyList<string> paths)
    {
        var infos = await Task.Run(() => FileOps.FileInfoBatch(paths));
        _dispatcher.TryEnqueue(() =>
        {
            var byPath = infos.ToDictionary(i => i.Path, i => i);
            foreach (var vm in Items)
            {
                if (byPath.TryGetValue(vm.Path, out var info))
                {
                    vm.FileSizeBytes = info.Size;
                    vm.FileModifiedAt = IsoTime.FromUnixMs(info.ModifiedMs);
                }
            }
        });
    }

    private async Task ProbeDurationAsync(string path)
    {
        var existing = Items.FirstOrDefault(i => i.Path == path);
        if (existing is null || existing.DurationSec is not null) return;
        await _probeGate.WaitAsync();
        try
        {
            var info = await _probe.ProbeAsync(path, _config.FfprobePath);
            _dispatcher.TryEnqueue(() =>
            {
                var vm = Items.FirstOrDefault(i => i.Path == path);
                if (vm is not null && vm.DurationSec is null) vm.DurationSec = info.DurationSec;
            });
        }
        catch { /* leave duration unknown */ }
        finally { _probeGate.Release(); }
    }

    // ---- Persistence of per-file edits (edits.json) ----

    public void PersistItemEdit(QueueItemViewModel item)
    {
        _editStore[item.Path] = item.ToStoredEdit();
        _saveStore.Run(500, () =>
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_editStore, QlipqJson.Options);
            _ = _appData.WriteAsync(EditsFile, json);
        });
    }

    // ---- Navigation ----

    [RelayCommand] private void ShowQueue() => CurrentView = "queue";
    [RelayCommand] private void ShowSettings() => CurrentView = "settings";

    partial void OnCurrentViewChanged(string value)
    {
        OnPropertyChanged(nameof(IsQueueView));
        OnPropertyChanged(nameof(IsSettingsView));
    }

    partial void OnSelectedItemChanged(QueueItemViewModel? value)
    {
        if (value is null) { Editor = null; return; }
        var editor = _provider.GetRequiredService<EditorViewModel>();
        editor.Load(value, this);
        Editor = editor;
    }

    // ---- Tag filtering / visibility ----

    private void RebuildVisible()
    {
        var tags = Items.SelectMany(i => i.Tags).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
        SyncTags(tags);

        IEnumerable<QueueItemViewModel> visible =
            TagFilter is { } f && tags.Contains(f)
                ? Items.Where(i => i.Tags.Contains(f))
                : Items.Where(i => !i.Tags.Contains(QueueItemViewModel.DismissedTag));

        var selected = SelectedItem;
        VisibleItems.Clear();
        foreach (var item in visible) VisibleItems.Add(item);
        if (selected is not null && VisibleItems.Contains(selected)) SelectedItem = selected;

        PendingCount = Items.Count(i => i.Status != QueueStatus.Done && !i.Tags.Contains(QueueItemViewModel.DismissedTag));
    }

    private void SyncTags(List<string> tags)
    {
        AllTags.Clear();
        foreach (var t in tags) AllTags.Add(t);
    }

    [RelayCommand]
    private void SetTagFilter(string? tag)
    {
        TagFilter = tag;
        RebuildVisible();
    }

    // ---- Item commands ----

    [RelayCommand]
    private async Task RescanAllAsync()
    {
        var found = await Task.Run(() => Scanner.ScanFolders(_config.WatchedFolders, _config.VideoExtensions));
        AddPaths(found);
    }

    private async Task ReprocessFolderAsync(string folder)
    {
        var found = await Task.Run(() => Scanner.ScanFolders([folder], _config.VideoExtensions));
        AddPaths(found);
        CurrentView = "queue";
    }

    [RelayCommand]
    private async Task RenameAsync(QueueItemViewModel item)
    {
        var newName = await _dialogs.PromptRenameAsync(item.FileName, item.RecordedAt, item.Source, _config.NamingTemplate);
        if (newName is null) return;
        var newPath = PathJoin(PathDirName(item.Path), newName);
        try
        {
            var finalPath = await Task.Run(() => FileOps.RenameFile(item.Path, newPath));
            var finalName = PathBaseName(finalPath);
            var parsed = Obs.ParseObsFilename(finalName);
            _knownPaths.Remove(item.Path);
            _knownPaths.Add(finalPath);
            // Move the persisted edit entry to the new path key.
            if (_editStore.Remove(item.Path, out var stored)) _editStore[finalPath] = stored;
            item.Path = finalPath;
            item.FileName = finalName;
            item.RecordedAt = parsed.RecordedAt is { } r ? IsoTime.FromLocal(r) : item.RecordedAt;
            item.Source = parsed.Source ?? item.Source;
            PersistItemEdit(item);
        }
        catch (Exception e)
        {
            item.Status = QueueStatus.Error;
            item.Error = e.Message;
        }
    }

    [RelayCommand]
    private void Dismiss(QueueItemViewModel item)
    {
        var has = item.IsDismissed;
        var tags = new List<string>(item.Tags);
        if (has) tags.Remove(QueueItemViewModel.DismissedTag);
        else tags.Add(QueueItemViewModel.DismissedTag);
        item.Tags = tags;
        PersistItemEdit(item);
        if (!has && SelectedItem == item) SelectedItem = null;
        RebuildVisible();
    }

    [RelayCommand]
    private async Task DeleteAsync(QueueItemViewModel item)
    {
        if (!_deleteConfirmed)
        {
            var ok = await _dialogs.ConfirmAsync(
                "Delete this file from disk?",
                $"{item.FileName} will be permanently deleted from your drive. This can't be undone. (You won't be asked again.)",
                "Delete");
            if (!ok) return;
            _deleteConfirmed = true;
            _ = _appData.WriteAsync(DeleteFlagFile, "1");
        }
        try
        {
            await Task.Run(() => FileOps.DeleteFile(item.Path));
            RemoveItem(item);
        }
        catch (Exception e)
        {
            item.Status = QueueStatus.Error;
            item.Error = e.Message;
        }
    }

    private void RemoveItem(QueueItemViewModel item)
    {
        _knownPaths.Remove(item.Path);
        Items.Remove(item);
        if (SelectedItem == item) SelectedItem = null;
        RebuildVisible();
    }

    /// <summary>Called by the editor when an item's status/tags change, to refresh derived state.</summary>
    public void NotifyItemChanged() => RebuildVisible();

    // ---- External links ----

    [RelayCommand] private void OpenRepo() => _dialogs.OpenExternal("https://github.com/qcksys/qlipq");
    [RelayCommand] private void OpenFfmpeg() => _dialogs.OpenExternal("https://ffmpeg.org");

    [RelayCommand]
    private async Task OpenConfigFileAsync()
    {
        try
        {
            await _configStore.SaveAsync(_config);
            _dialogs.RevealInExplorer(_configStore.ConfigFilePath);
        }
        catch (Exception e) { System.Diagnostics.Debug.WriteLine($"open config failed: {e.Message}"); }
    }

    // ---- Path helpers (forward-slash, matching the web's queue.ts) ----

    private static string PathBaseName(string path)
    {
        var n = path.Replace('\\', '/');
        return n[(n.LastIndexOf('/') + 1)..];
    }

    private static string PathDirName(string path)
    {
        var n = path.Replace('\\', '/');
        var idx = n.LastIndexOf('/');
        return idx <= 0 ? "" : path[..idx];
    }

    private static string PathJoin(string dir, string name) =>
        string.IsNullOrEmpty(dir) ? name : $"{dir.TrimEnd('/', '\\')}/{name}";
}
