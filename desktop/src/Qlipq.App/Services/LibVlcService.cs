using LibVLCSharp.Shared;

namespace Qlipq.App.Services;

/// <summary>
/// Ensures the native libvlc runtime is initialized exactly once. The actual
/// <see cref="LibVLC"/> + MediaPlayer are created per-VideoView (WinUI requires building them
/// from the VideoView's <c>Initialized</c> SwapChainOptions), so this only handles Core.Initialize.
/// </summary>
public sealed class LibVlcService
{
    private static bool _initialized;
    private static readonly object Gate = new();

    public void EnsureInitialized()
    {
        if (_initialized) return;
        lock (Gate)
        {
            if (_initialized) return;
            LibVLCSharp.Shared.Core.Initialize();
            _initialized = true;
        }
    }
}
