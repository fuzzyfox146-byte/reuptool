using System.Diagnostics;
using System.Text;

namespace VideoAutoTool.Core.FrameBatch;

public static class FrameProbe
{
    public static async Task<double> DurationAsync(string ffprobe, string path, CancellationToken cancellationToken)
    {
        var output = await RunAsync(
            ffprobe,
            ["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path],
            cancellationToken).ConfigureAwait(false);
        if (!double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var duration) || duration <= 0)
        {
            throw new InvalidOperationException("Không đọc được độ dài: " + path);
        }

        return duration;
    }

    public static async Task<bool> HasAudioAsync(string ffprobe, string path, CancellationToken cancellationToken)
    {
        var output = await RunAsync(
            ffprobe,
            ["-v", "error", "-select_streams", "a", "-show_entries", "stream=index", "-of", "csv=p=0", path],
            cancellationToken).ConfigureAwait(false);
        return output.Trim().Length > 0;
    }

    private static async Task<string> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo.FileName = executable;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        var output = await stdout.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "ffprobe lỗi." : error.Trim());
        }

        return output;
    }
}
