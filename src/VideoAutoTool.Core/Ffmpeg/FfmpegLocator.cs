using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VideoAutoTool.Core.Ffmpeg;

public static class FfmpegLocator
{
    private static readonly Regex VersionRegex = new(@"ffmpeg version (\S+)", RegexOptions.Compiled);

    public static FfmpegPaths Locate(string? configuredDirectory = null)
    {
        var candidates = BuildCandidates(configuredDirectory);
        foreach (var dir in candidates)
        {
            var ffmpeg = Path.Combine(dir, "ffmpeg.exe");
            var ffprobe = Path.Combine(dir, "ffprobe.exe");
            if (!File.Exists(ffmpeg) || !File.Exists(ffprobe))
            {
                continue;
            }

            var version = TryReadVersion(ffmpeg);
            return new FfmpegPaths(ffmpeg, ffprobe, version);
        }

        throw new FfmpegNotFoundException();
    }

    private static IEnumerable<string> BuildCandidates(string? configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            yield return configuredDirectory;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (var part in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return part;
            }
        }

        yield return @"C:\ffmpeg\bin";
        yield return @"C:\tools\ffmpeg\bin";
    }

    private static string? TryReadVersion(string ffmpegPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            var match = VersionRegex.Match(output);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }
}
