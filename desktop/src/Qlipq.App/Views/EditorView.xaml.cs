using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Qlipq.App.Services;
using Qlipq.App.ViewModels;

namespace Qlipq.App.Views;

public sealed partial class EditorView : UserControl
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private EditorViewModel? _attached;
    private bool _scrubbing;

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private EditorViewModel? Vm => DataContext as EditorViewModel;

    // WinUI requires building LibVLC from the VideoView's swapchain options.
    private void OnVideoInitialized(object sender, InitializedEventArgs e)
    {
        App.Services.GetRequiredService<LibVlcService>().EnsureInitialized();
        _libVlc = new LibVLC(e.SwapChainOptions);
        _mediaPlayer = new MediaPlayer(_libVlc);
        Video.MediaPlayer = _mediaPlayer;
        AttachCurrent();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ReferenceEquals(_attached, Vm)) return;
        _attached?.DetachPlayer();
        _attached = Vm;
        AttachCurrent();
    }

    private void AttachCurrent()
    {
        if (_libVlc is not null && _mediaPlayer is not null && Vm is { } vm)
        {
            vm.AttachPlayer(_libVlc, _mediaPlayer);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _attached?.DetachPlayer();
        if (Video is not null) Video.MediaPlayer = null;
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
        _libVlc?.Dispose();
        _libVlc = null;
    }

    // ---- Scrubbing: only seek when the change is user-driven. ----

    private void OnScrubPressed(object sender, PointerRoutedEventArgs e) => _scrubbing = true;

    private void OnScrubReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_scrubbing && sender is Slider s) Vm?.Seek(s.Value);
        _scrubbing = false;
    }

    private void OnScrubChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_scrubbing) Vm?.Seek(e.NewValue);
    }

    // ---- Tags ----

    private void OnRemoveTag(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string tag) Vm?.RemoveTagCommand.Execute(tag);
    }

    private void OnTagKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) Vm?.AddTagCommand.Execute(null);
    }
}
