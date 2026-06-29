namespace Qlipq.App.ViewModels;

/// <summary>Coalesces rapid calls into a single delayed invocation (debounced auto-save).</summary>
public sealed class Debouncer
{
    private CancellationTokenSource? _cts;

    public void Run(int delayMs, Action action)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        _ = Task.Delay(delayMs, cts.Token).ContinueWith(
            t => { if (!t.IsCanceled) action(); },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }
}
