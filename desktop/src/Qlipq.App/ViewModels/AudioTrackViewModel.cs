using CommunityToolkit.Mvvm.ComponentModel;
using Qlipq.Core;

namespace Qlipq.App.ViewModels;

/// <summary>Per-track enable toggle and volume (linear gain 0–2), shown as a percentage.</summary>
public sealed partial class AudioTrackViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public int Index { get; }
    public string Label { get; }
    public string Detail { get; }

    [ObservableProperty] private bool _enabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumePercent))]
    private double _volume;

    public AudioTrackViewModel(AudioStreamInfo stream, AudioTrackSpec spec, Action onChanged)
    {
        _onChanged = onChanged;
        Index = stream.Index;
        Label = MediaFormat.AudioStreamLabel(stream);
        Detail = $"{stream.Codec} · {stream.Channels}ch";
        _enabled = spec.Enabled;
        _volume = spec.Volume;
    }

    public string VolumePercent => $"{(int)Math.Round(Volume * 100)}%";

    partial void OnEnabledChanged(bool value) => _onChanged();
    partial void OnVolumeChanged(double value) => _onChanged();

    public AudioTrackSpec ToSpec() => new() { Index = Index, Enabled = Enabled, Volume = Volume };
}
