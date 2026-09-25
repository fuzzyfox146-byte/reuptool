using System.Diagnostics;
using System.Text;

namespace VideoAutoTool.Core.Ffmpeg;

internal static class FfmpegProcessRunner
{
    public static async Task<FfmpegResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null,
        double? totalDurationSeconds = null,
        string? stallWatchPath = null)
    {
        var stdout = new StringBuilder();
        var stderrLines = new Queue<string>();
        var progressEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sw = Stopwatch.StartNew();

        using var process = new Process();
        process.StartInfo.FileName = executable;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            process.StartInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var arg in arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();

        var stdoutTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                stdout.AppendLine(line);
                if (line.Equals("progress=end", StringComparison.Ordinal))
                {
                    progressEnded.TrySetResult();
                }

                if (progress is not null && totalDurationSeconds is > 0 &&
                    line.StartsWith("out_time_us=", StringComparison.Ordinal))
                {
                    if (long.TryParse(line["out_time_us=".Length..], out var micros))
                    {
                        progress.Report(Math.Clamp(micros / 1_000_000.0 / totalDurationSeconds.Value, 0, 1));
                    }
                }
            }
        }, cancellationToken);

        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                lock (stderrLines)
                {
                    stderrLines.Enqueue(line);
                    while (stderrLines.Count > 25)
                    {
                        stderrLines.Dequeue();
                    }
                }
            }
        }, cancellationToken);

        var forcedOk = false;
        try
        {
            var exitTask = process.WaitForExitAsync(cancellationToken);
            var first = await Task.WhenAny(exitTask, progressEnded.Task).ConfigureAwait(false);
            if (first != exitTask)
            {
                var lastSize = -1L;
                var lastChange = DateTime.UtcNow;
                while (!process.HasExited)
                {
                    var wait = Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    var finished = await Task.WhenAny(exitTask, wait).ConfigureAwait(false);
                    if (finished == exitTask)
                    {
                        break;
                    }

                    var size = ProcessStallWatch.TryReadLength(stallWatchPath);
                    if (size != lastSize)
                    {
                        lastSize = size;
                        lastChange = DateTime.UtcNow;
                        continue;
                    }

                    if (ProcessStallWatch.IsStalled(lastSize, lastChange, size, DateTime.UtcNow))
                    {
                        KillProcessTree(process);
                        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                        forcedOk = true;
                        break;
                    }
                }
            }

            await exitTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            throw;
        }

        await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask), Task.Delay(3000)).ConfigureAwait(false);
        sw.Stop();

        string tail;
        lock (stderrLines)
        {
            tail = string.Join(Environment.NewLine, stderrLines);
        }

        var exitCode = forcedOk ? 0 : process.ExitCode;
        return new FfmpegResult(exitCode, tail, sw.Elapsed, stdout.ToString());
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
