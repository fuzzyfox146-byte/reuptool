using System.Collections.Concurrent;
using VideoAutoTool.Core.Cache;
using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Fonts;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Subtitles;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Render;

public sealed class JobRenderer
{
    private readonly FfmpegRunner _runner;
    private readonly IMediaProbe _probe;
    private readonly AssBuilder _assBuilder;
    private readonly EncoderSelector _encoderSelector;
    private readonly IFontCatalog _fontCatalog;
    private readonly PreparedAssetService? _assetService;
    private static readonly ConcurrentDictionary<string, MediaInfo> ProbeCache = new(StringComparer.OrdinalIgnoreCase);

    public JobRenderer(
        FfmpegRunner runner,
        IMediaProbe probe,
        AssBuilder assBuilder,
        EncoderSelector encoderSelector,
        IFontCatalog fontCatalog,
        PreparedAssetService? assetService = null)
    {
        _runner = runner;
        _probe = probe;
        _assBuilder = assBuilder;
        _encoderSelector = encoderSelector;
        _fontCatalog = fontCatalog;
        _assetService = assetService;
    }

    public async Task RenderAsync(
        Template template,
        RenderJobPlan job,
        RenderRequest request,
        string outputPath,
        bool useCache = false,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (useCache && _assetService is null)
        {
            throw new InvalidOperationException("PreparedAssetService is required for cached rendering");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "VideoAutoTool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var duration = request.Mode switch
        {
            RenderMode.Clip => Math.Min(request.ClipSeconds ?? 10, job.DurationSeconds),
            RenderMode.Frame => (request.FrameTimeSeconds ?? 0) + 0.5,
            _ => job.DurationSeconds
        };
        var partPath = request.Mode == RenderMode.Frame
            ? outputPath
            : RenderStaging.ResolvePartPath(outputPath, RenderStaging.EstimateJobBytes(template, duration));
        string? concatListPath = null;

        try
        {
            MediaInfo? avatarInfo = null;
            MediaInfo? waveInfo = null;
            if (job.AvatarPath is not null)
            {
                avatarInfo = await ProbeCachedAsync(job.AvatarPath, cancellationToken).ConfigureAwait(false);
            }

            if (job.WavePath is not null)
            {
                waveInfo = await ProbeCachedAsync(job.WavePath, cancellationToken).ConfigureAwait(false);
            }

            if (job.SubPath is not null)
            {
                var cues = SrtParser.ParseFile(job.SubPath);
                _assBuilder.WriteToFile(Path.Combine(tempDir, "sub.ass"), job, template, cues);
                FontFilePreparer.PrepareFontsDirectory(tempDir, job.StylePreset, _fontCatalog);
            }

            var preferNvenc = template.Output.Encoder is OutputEncoder.Auto or OutputEncoder.Nvenc;
            var encode = await _encoderSelector.SelectAsync(preferNvenc, cancellationToken).ConfigureAwait(false);
            var timedJob = job with { DurationSeconds = duration };

            string? bakedWavePath = null;
            var originalHasAlpha = waveInfo?.HasAlpha == true;
            if (useCache && _assetService is not null && job.WavePath is not null && waveInfo is not null)
            {
                var assetService = _assetService;
                var wavePath = job.WavePath;
                var wave = waveInfo;
                await AssetPrepareGate.RunAsync(
                    [wavePath],
                    async () =>
                    {
                        bakedWavePath = await assetService.PrepareWaveAsync(
                            template,
                            job,
                            wave,
                            cancellationToken).ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            List<string> BuildCachedRenderArguments(bool useGpu)
            {
                var useBakedWave = bakedWavePath is not null && (!useGpu || originalHasAlpha);
                var graphJob = useBakedWave ? timedJob with { WavePath = bakedWavePath } : timedJob;
                var graphWave = useBakedWave && waveInfo is not null
                    ? waveInfo with { Path = bakedWavePath!, HasAlpha = true }
                    : waveInfo;
                var graph = useGpu
                    ? FilterGraphBuilder.BuildCachedGpu(
                        template, graphJob, concatListPath!, avatarInfo, graphWave, job.SubPath is not null, useBakedWave)
                    : FilterGraphBuilder.BuildCached(
                        template, graphJob, concatListPath!, avatarInfo, graphWave, job.SubPath is not null, useBakedWave);
                var enc = useGpu ? encode with { KeepFramesOnGpu = true } : encode;
                return RenderCommandBuilder.BuildCachedArguments(
                    template,
                    graphJob,
                    graph,
                    timedJob.DriverPath,
                    concatListPath!,
                    partPath,
                    enc,
                    request,
                    cpuDecodeWave: useGpu && (useBakedWave || originalHasAlpha));
            }

            List<string> args;
            var retryCpuGraph = false;
            if (useCache && _assetService is not null)
            {
                var assetService = _assetService;
                List<string>? cachedBackgrounds = null;
                await AssetPrepareGate.RunAsync(
                    job.Backgrounds.Select(s => s.File),
                    async () =>
                    {
                        cachedBackgrounds = await assetService.PrepareBackgroundsAsync(
                            template,
                            job,
                            encode.Encoder,
                            encode.HwAccel,
                            progress: null,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(false);

                concatListPath = ConcatListBuilder.Create(cachedBackgrounds!, tempDir);

                retryCpuGraph = encode.Encoder == VideoEncoderKind.H264Nvenc
                    && FilterGraphBuilder.ShouldUseGpuOverlay(template.Canvas);
                args = BuildCachedRenderArguments(retryCpuGraph);
            }
            else
            {
                var graph = FilterGraphBuilder.Build(template, timedJob, avatarInfo, waveInfo, job.SubPath is not null);

                args = request.Mode == RenderMode.Frame
                    ? RenderCommandBuilder.BuildPreviewArguments(
                        template,
                        timedJob,
                        graph,
                        timedJob.DriverPath,
                        partPath,
                        request.FrameTimeSeconds ?? 0,
                        encode)
                    : RenderCommandBuilder.BuildArguments(
                        template,
                        timedJob,
                        graph,
                        timedJob.DriverPath,
                        partPath,
                        encode,
                        request);
            }

            RenderStaging.TryDeletePair(partPath, outputPath);
            var result = await _runner.RunAsync(
                args, tempDir, cancellationToken, progress, duration, partPath).ConfigureAwait(false);
            if (result.ExitCode != 0 && retryCpuGraph)
            {
                RenderStaging.TryDeletePair(partPath, outputPath);

                args = BuildCachedRenderArguments(useGpu: false);
                result = await _runner.RunAsync(
                    args, tempDir, cancellationToken, progress, duration, partPath).ConfigureAwait(false);
            }

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg render failed: {result.Tail}");
            }

            if (request.Mode != RenderMode.Frame && File.Exists(partPath))
            {
                RenderStaging.Promote(partPath, outputPath);
            }
        }
        catch
        {
            RenderStaging.TryDeletePair(partPath, outputPath);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private async Task<MediaInfo> ProbeCachedAsync(string path, CancellationToken cancellationToken)
    {
        if (ProbeCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var info = await _probe.ProbeAsync(path, cancellationToken).ConfigureAwait(false);
        ProbeCache[path] = info;
        return info;
    }
}
