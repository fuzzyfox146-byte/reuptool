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
        var partPath = request.Mode == RenderMode.Frame ? outputPath : $"{outputPath}.part";
        string? concatListPath = null;

        try
        {
            MediaInfo? avatarInfo = null;
            MediaInfo? waveInfo = null;
            if (job.AvatarPath is not null)
            {
                avatarInfo = await _probe.ProbeAsync(job.AvatarPath, cancellationToken).ConfigureAwait(false);
            }

            if (job.WavePath is not null)
            {
                waveInfo = await _probe.ProbeAsync(job.WavePath, cancellationToken).ConfigureAwait(false);
            }

            if (job.SubPath is not null)
            {
                var cues = SrtParser.ParseFile(job.SubPath);
                _assBuilder.WriteToFile(Path.Combine(tempDir, "sub.ass"), job, template, cues);
                FontFilePreparer.PrepareFontsDirectory(tempDir, job.StylePreset, _fontCatalog);
            }

            var preferNvenc = template.Output.Encoder is OutputEncoder.Auto or OutputEncoder.Nvenc;
            var encoder = await _encoderSelector.SelectAsync(preferNvenc, cancellationToken).ConfigureAwait(false);

            List<string> args;
            if (useCache && _assetService is not null)
            {
                var cachedBackgrounds = await _assetService.PrepareBackgroundsAsync(
                    template,
                    job,
                    progress: null,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                concatListPath = ConcatListBuilder.Create(cachedBackgrounds, tempDir);

                var graph = FilterGraphBuilder.BuildCached(template, job, concatListPath, avatarInfo, waveInfo, job.SubPath is not null);

                args = RenderCommandBuilder.BuildCachedArguments(
                    template,
                    job,
                    graph,
                    job.DriverPath,
                    concatListPath,
                    partPath,
                    encoder,
                    request);
            }
            else
            {
                var graph = FilterGraphBuilder.Build(template, job, avatarInfo, waveInfo, job.SubPath is not null);

                args = request.Mode == RenderMode.Frame
                    ? RenderCommandBuilder.BuildPreviewArguments(
                        template,
                        job,
                        graph,
                        job.DriverPath,
                        partPath,
                        request.FrameTimeSeconds ?? 0)
                    : RenderCommandBuilder.BuildArguments(
                        template,
                        job,
                        graph,
                        job.DriverPath,
                        partPath,
                        encoder,
                        request);
            }

            var duration = request.Mode switch
            {
                RenderMode.Clip => Math.Min(request.ClipSeconds ?? 10, job.DurationSeconds),
                _ => job.DurationSeconds
            };

            var result = await _runner.RunAsync(args, tempDir, cancellationToken, progress, duration).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg render failed: {result.Tail}");
            }

            if (request.Mode != RenderMode.Frame && File.Exists(partPath))
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                File.Move(partPath, outputPath);
            }
        }
        catch
        {
            if (File.Exists(partPath))
            {
                File.Delete(partPath);
            }

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
}
