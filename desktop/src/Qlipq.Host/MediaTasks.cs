using Qlipq.Core;
using Qlipq.Ffmpeg;

namespace Qlipq.Host;

/// <summary>Runs ffprobe and parses the result (mirrors the Rust <c>probe_raw</c> + frontend parse).</summary>
public sealed class MediaProbe(ProcessRunner runner)
{
    public async Task<string> ProbeRawAsync(string path, string ffprobePath, CancellationToken ct = default)
    {
        var (code, stdout, stderr) = await runner.RunCaptureAsync(ffprobePath, Probe.BuildProbeArgs(path), ct);
        if (code != 0) throw new ProcessException(stderr);
        return stdout;
    }

    public async Task<MediaInfo> ProbeAsync(string path, string ffprobePath, CancellationToken ct = default)
    {
        return Probe.ParseFfprobe(await ProbeRawAsync(path, ffprobePath, ct));
    }
}

/// <summary>
/// Runs an ffmpeg export with a pre-built argument list (built by <see cref="ExportArgs"/>),
/// streaming <c>-progress</c> stdout and reporting parsed <see cref="ProgressUpdate"/>s. Rust never
/// interpreted the edit — it only ran the given args — and neither does this.
/// </summary>
public sealed class ExportRunner(ProcessRunner runner)
{
    public Task RunExportAsync(
        string ffmpegPath,
        IReadOnlyList<string> args,
        IProgress<ProgressUpdate>? progress,
        CancellationToken ct = default)
    {
        double? lastOutTime = null;
        return runner.RunStreamingAsync(ffmpegPath, args, line =>
        {
            if (progress is null) return;
            var update = Progress.ParseProgress(line);
            if (update.OutTimeSec is not null) lastOutTime = update.OutTimeSec;
            progress.Report(new ProgressUpdate { OutTimeSec = lastOutTime, Done = update.Done });
        }, ct);
    }
}
