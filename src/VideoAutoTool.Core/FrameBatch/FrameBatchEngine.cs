using System.Text.Json;
using VideoAutoTool.Core.Render;

namespace VideoAutoTool.Core.FrameBatch;

public interface IFrameCommandRunner
{
    Task<FrameCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null,
        double? durationSeconds = null,
        string? stallWatchPath = null);
}

public sealed record FrameCommandResult(int ExitCode, string Tail);

public sealed class FrameBatchEngine
{
    private readonly IFrameCommandRunner _runner;
    private readonly string _ffprobe;

    public FrameBatchEngine(IFrameCommandRunner runner, string ffprobe)
    {
        _runner = runner;
        _ffprobe = ffprobe;
    }

    public async Task<bool> NvencAvailableAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            [
                "-hide_banner", "-loglevel", "error",
                "-f", "lavfi", "-i", "color=c=black:s=256x256:d=0.2",
                "-c:v", "h264_nvenc", "-f", "null", "-"
            ],
            cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public async Task<IReadOnlyDictionary<string, string>> PrepareCacheAsync(
        IReadOnlyList<string> frames,
        IReadOnlyList<string> backgrounds,
        string cacheDirectory,
        FrameSettings settings,
        int workers,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(cacheDirectory, "manifest.json");
        var manifest = ReadManifest(manifestPath);
        var frameKeys = manifest.Frames;
        var backgroundKeys = manifest.Backgrounds;
        var jobs = new List<CacheJob>();
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var frame in frames)
        {
            var destination = FramePlan.CacheFile(Path.Combine(cacheDirectory, "frames"), frame, ".png");
            var key = FramePlan.Fingerprint(frame, $"frame|{settings.Width}x{settings.Height}");
            mapping[Path.GetFullPath(frame)] = destination;
            if (frameKeys.TryGetValue(Path.GetFullPath(frame), out var saved) && saved == key && File.Exists(destination))
            {
                continue;
            }

            jobs.Add(new CacheJob("frame", frame, destination, key));
        }

        foreach (var background in backgrounds)
        {
            mapping[Path.GetFullPath(background)] = Path.GetFullPath(background);
        }

        var gate = new SemaphoreSlim(Math.Max(1, workers));
        var tasks = jobs.Select(job => RunCacheJobAsync(job, settings, gate, cancellationToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var frame in frames)
        {
            frameKeys[Path.GetFullPath(frame)] = FramePlan.Fingerprint(frame, $"frame|{settings.Width}x{settings.Height}");
        }

        Directory.CreateDirectory(cacheDirectory);
        var payload = new CacheManifest { Frames = frameKeys, Backgrounds = backgroundKeys, Noise = "" };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(payload), cancellationToken).ConfigureAwait(false);
        return mapping;
    }

    public async Task RenderAsync(
        string video,
        string background,
        string frame,
        string output,
        FrameSettings settings,
        IProgress<double>? progress,
        CancellationToken cancellationToken,
        Action<string>? notice = null)
    {
        if (File.Exists(output) && new FileInfo(output).Length > 0)
        {
            progress?.Report(1);
            return;
        }

        var duration = await FrameProbe.DurationAsync(_ffprobe, video, cancellationToken).ConfigureAwait(false);
        var audio = await FrameProbe.HasAudioAsync(_ffprobe, video, cancellationToken).ConfigureAwait(false);
        var partial = FrameStaging.ResolvePartPath(output, FrameStaging.EstimateBytes(duration, settings.Crf));
        var partialDirectory = Path.GetDirectoryName(partial);
        if (!string.IsNullOrWhiteSpace(partialDirectory))
        {
            Directory.CreateDirectory(partialDirectory);
        }

        try
        {
            var layout = FrameLayoutMath.Create(settings);
            var args = FrameCommands.Render(video, background, frame, partial, duration, audio, settings, layout);
            var result = await _runner.RunAsync(args, cancellationToken, progress, duration, partial).ConfigureAwait(false);
            if (result.ExitCode != 0 && settings.Gpu)
            {
                notice?.Invoke("CUDA lỗi, render lại bằng CPU. " + result.Tail);
                PartFile.TryDelete(partial);
                PartFile.TryDelete(FrameStaging.SiblingPart(output));
                var cpu = settings with { Gpu = false };
                args = FrameCommands.Render(video, background, frame, partial, duration, audio, cpu, FrameLayoutMath.Create(cpu));
                result = await _runner.RunAsync(args, cancellationToken, progress, duration, partial).ConfigureAwait(false);
            }

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Tail) ? "ffmpeg lỗi." : result.Tail);
            }

            FrameStaging.Promote(partial, output);
        }
        catch
        {
            PartFile.TryDelete(partial);
            PartFile.TryDelete(FrameStaging.SiblingPart(output));
            throw;
        }
    }

    public async Task RenderTemplateJobAsync(
        ResolvedTemplateJob job,
        FrameTemplatePreset preset,
        string output,
        FrameSettings settings,
        IReadOnlyDictionary<string, string>? cacheMapping,
        IProgress<double>? progress,
        CancellationToken cancellationToken,
        Action<string>? notice = null)
    {
        if (File.Exists(output) && new FileInfo(output).Length > 0)
        {
            progress?.Report(1);
            return;
        }

        if (preset.IsStandardThreeLayer())
        {
            var mainLayer = preset.Layers.First(layer => layer.Enabled && layer.Kind == FrameLayerKind.MainVideo);
            var stdSettings = settings with
            {
                VideoScale = mainLayer.Scale > 0.05 ? mainLayer.Scale : settings.VideoScale,
                VideoOpacity = mainLayer.Opacity
            };
            var bgPath = MapCached(job.PrimaryBackground, cacheMapping);
            var frPath = MapCached(job.PrimaryFrame, cacheMapping);
            await RenderAsync(job.DriverVideo, bgPath, frPath, output, stdSettings, progress, cancellationToken, notice).ConfigureAwait(false);
            return;
        }

        var duration = await FrameProbe.DurationAsync(_ffprobe, job.DriverVideo, cancellationToken).ConfigureAwait(false);
        var audio = await FrameProbe.HasAudioAsync(_ffprobe, job.DriverVideo, cancellationToken).ConfigureAwait(false);
        var partial = FrameStaging.ResolvePartPath(output, FrameStaging.EstimateBytes(duration, settings.Crf));
        var partialDirectory = Path.GetDirectoryName(partial);
        if (!string.IsNullOrWhiteSpace(partialDirectory))
        {
            Directory.CreateDirectory(partialDirectory);
        }

        var mappedLayers = job.Layers
            .Select(item => item with
            {
                AssetPath = string.IsNullOrWhiteSpace(item.AssetPath) ? item.AssetPath : MapCached(item.AssetPath!, cacheMapping)
            })
            .ToList();
        var mappedJob = job with { Layers = mappedLayers };

        var tempAssFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var entry in mappedJob.Layers)
            {
                if (!entry.Layer.Enabled || entry.Layer.Kind is not (FrameLayerKind.Subtitle or FrameLayerKind.StaticText))
                {
                    continue;
                }

                if (entry.Layer.Kind == FrameLayerKind.Subtitle && (string.IsNullOrWhiteSpace(entry.AssetPath) || !File.Exists(entry.AssetPath)))
                {
                    continue;
                }

                var assPath = Path.Combine(
                    partialDirectory ?? Path.GetTempPath(),
                    $"{Path.GetFileNameWithoutExtension(output)}_{entry.Layer.Id}.ass");
                FrameTemplateRenderer.WriteSubtitleAss(
                    assPath,
                    entry.Layer,
                    entry.AssetPath,
                    entry.ChosenTextColor,
                    settings,
                    duration,
                    mappedJob.MirroredLeft);
                tempAssFiles[entry.Layer.Id] = assPath;
            }

            var args = FrameTemplateRenderer.BuildRenderArgs(mappedJob, preset, partial, duration, audio, settings, tempAssFiles);
            var result = await _runner.RunAsync(args, cancellationToken, progress, duration, partial).ConfigureAwait(false);
            if (result.ExitCode != 0 && settings.Gpu)
            {
                notice?.Invoke("CUDA lỗi, render lại bằng CPU. " + result.Tail);
                PartFile.TryDelete(partial);
                PartFile.TryDelete(FrameStaging.SiblingPart(output));
                var cpu = settings with { Gpu = false };
                args = FrameTemplateRenderer.BuildRenderArgs(mappedJob, preset, partial, duration, audio, cpu, tempAssFiles);
                result = await _runner.RunAsync(args, cancellationToken, progress, duration, partial).ConfigureAwait(false);
            }

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Tail) ? "ffmpeg lỗi." : result.Tail);
            }

            FrameStaging.Promote(partial, output);
        }
        catch
        {
            PartFile.TryDelete(partial);
            PartFile.TryDelete(FrameStaging.SiblingPart(output));
            throw;
        }
        finally
        {
            foreach (var assFile in tempAssFiles.Values)
            {
                PartFile.TryDelete(assFile);
            }
        }
    }

    private static string MapCached(string path, IReadOnlyDictionary<string, string>? mapping)
    {
        if (string.IsNullOrWhiteSpace(path) || mapping is null)
        {
            return path;
        }

        return mapping.TryGetValue(Path.GetFullPath(path), out var cached) ? cached : path;
    }

    private async Task RunCacheJobAsync(CacheJob job, FrameSettings settings, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var destination = job.Destination;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var partial = Path.Combine(
                Path.GetDirectoryName(destination)!,
                Path.GetFileNameWithoutExtension(destination) + ".part" + Path.GetExtension(destination));
            IReadOnlyList<string> args = job.Kind switch
            {
                "frame" => FrameCommands.FrameCache(job.Source, partial, settings),
                "background" => FrameCommands.BackgroundCache(job.Source, partial, settings),
                "noise" => FrameCommands.NoiseCache(job.Source, partial, settings),
                _ => throw new InvalidOperationException("Cache không rõ loại: " + job.Kind)
            };
            try
            {
                var result = await _runner.RunAsync(args, cancellationToken).ConfigureAwait(false);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Tail) ? "Cache lỗi." : result.Tail);
                }

                File.Move(partial, destination, overwrite: true);
            }
            catch
            {
                if (File.Exists(partial))
                {
                    File.Delete(partial);
                }

                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static CacheManifest ReadManifest(string path)
    {
        if (!File.Exists(path))
        {
            return new CacheManifest();
        }

        try
        {
            return JsonSerializer.Deserialize<CacheManifest>(File.ReadAllText(path)) ?? new CacheManifest();
        }
        catch (JsonException)
        {
            return new CacheManifest();
        }
    }

    private sealed record CacheJob(string Kind, string Source, string Destination, string Key);

    private sealed class CacheManifest
    {
        public Dictionary<string, string> Frames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Backgrounds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string Noise { get; set; } = "";
    }
}
