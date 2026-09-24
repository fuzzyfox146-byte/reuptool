using System.Diagnostics;
using System.Text;

namespace VideoAutoTool.Core.Download;

public sealed class YtDlpProcessRunner : IYtDlpRunner
{
    public async Task<YtDlpRunResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        IProgress<string>? log,
        CancellationToken cancellationToken)
    {
        var cookieLine = "";
        var cookieDead = 0;

        using var process = new Process();
        process.StartInfo.FileName = executable;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        foreach (var arg in arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        using var cancelReg = cancellationToken.Register(() => Kill(process));

        var stdout = PumpAsync(process.StandardOutput, log, OnLine, cancellationToken);
        var stderr = PumpAsync(process.StandardError, log, OnLine, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }

        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        var dead = cookieDead == 1;
        return new YtDlpRunResult(process.ExitCode, dead, dead ? cookieLine : null);

        void OnLine(string line)
        {
            if (!CookieFailure.IsFatal(line))
            {
                return;
            }

            cookieLine = line;
            if (Interlocked.Exchange(ref cookieDead, 1) == 0)
            {
                Kill(process);
            }
        }
    }

    private static async Task PumpAsync(
        StreamReader reader,
        IProgress<string>? log,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                log?.Report(line);
                onLine(line);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void Kill(Process process)
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
