using System.Collections.Concurrent;
using System.Text;

namespace VideoAutoTool.Core.Ffmpeg;

public sealed class FfmpegProbe : IMediaProbe
{
    private readonly FfmpegPaths _paths;
    private readonly ConcurrentDictionary<string, MediaInfo> _cache = new();

    public FfmpegProbe(FfmpegPaths paths) => _paths = paths;

    public async Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Media file not found.", fullPath);
        }

        var info = new FileInfo(fullPath);
        var cacheKey = $"{fullPath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var args = new List<string>
        {
            "-v", "quiet",
            "-print_format", "json",
            "-show_format",
            "-show_streams",
            fullPath
        };

        var result = await FfmpegProcessRunner.RunAsync(_paths.FfprobePath, args, null, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe failed for '{fullPath}': {result.Tail}");
        }

        var media = MediaInfoParser.Parse(fullPath, result.StandardOutput);
        _cache[cacheKey] = media;
        return media;
    }
}
