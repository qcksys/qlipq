using System.Diagnostics;
using System.Text;

namespace Qlipq.Host;

/// <summary>Thrown when a spawned ffmpeg/ffprobe process fails; message carries the stderr tail.</summary>
public sealed class ProcessException(string message) : Exception(message);

/// <summary>
/// Spawns ffmpeg/ffprobe and either captures output or streams stdout line-by-line, draining
/// stderr concurrently to avoid pipe-buffer deadlocks. Mirrors the Rust host (<c>hidden_command</c>
/// with CREATE_NO_WINDOW, <c>run_ffmpeg</c>'s streaming + last-8-stderr-lines error).
/// </summary>
public sealed class ProcessRunner
{
    private static ProcessStartInfo MakeStartInfo(string path, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    /// <summary>Run <c>&lt;path&gt; -version</c>; returns the first stdout line (version banner).</summary>
    public async Task<string> CheckBinaryAsync(string path, CancellationToken ct = default)
    {
        (int code, string stdout, string stderr) result;
        try { result = await RunCaptureAsync(path, ["-version"], ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new ProcessException($"Not found ({path}): {e.Message}");
        }
        if (result.code != 0) throw new ProcessException(result.stderr.Trim());
        var first = result.stdout.Split('\n', 2)[0];
        return first.Trim();
    }

    /// <summary>Run to completion, capturing full stdout/stderr.</summary>
    public async Task<(int Code, string Stdout, string Stderr)> RunCaptureAsync(
        string path, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        using var p = new Process { StartInfo = MakeStartInfo(path, args) };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        if (!p.Start()) throw new ProcessException($"Failed to start {path}");
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>
    /// Run ffmpeg, delivering each stdout line to <paramref name="onStdoutLine"/> as it arrives,
    /// while draining stderr. On non-zero exit throws <see cref="ProcessException"/> with the last
    /// (up to) 8 stderr lines. Cancellation kills the process tree.
    /// </summary>
    public async Task RunStreamingAsync(
        string path, IReadOnlyList<string> args, Action<string> onStdoutLine, CancellationToken ct = default)
    {
        using var p = new Process { StartInfo = MakeStartInfo(path, args) };
        var stderr = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) onStdoutLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        if (!p.Start()) throw new ProcessException($"Failed to start ffmpeg ({path})");
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        try
        {
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            TryKill(p);
            throw;
        }

        if (p.ExitCode != 0)
        {
            var tail = TailLines(stderr.ToString(), 8);
            throw new ProcessException(tail.Length == 0 ? $"ffmpeg exited with status {p.ExitCode}" : tail);
        }
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static string TailLines(string text, int count)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        return lines.Length <= count ? string.Join("\n", lines) : string.Join("\n", lines[^count..]);
    }
}
