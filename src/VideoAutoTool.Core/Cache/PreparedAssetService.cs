using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Cache;

public sealed class PreparedAssetService
{
    private readonly FfmpegRunner _runner;
    private readonly CacheStore _store;
    private readonly IMediaProbe _probe;

    public PreparedAssetService(FfmpegRunner runner, CacheStore store, IMediaProbe probe)
    {
        _runner = runner;
        _store = store;
        _probe = probe;
    }

    public async Task<List<string>> PrepareBackgroundsAsync(
        Template template,
        RenderJobPlan job,
        VideoEncoderKind encoder,
        string? hwAccel,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var canvas = template.Canvas;
        var bgLayer = template.Layers.First(l => l.Type == LayerType.BackgroundChain);
        var results = new List<string>();
        var total = job.Backgrounds.Count;

        for (int i = 0; i < total; i++)
        {
            var segment = job.Backgrounds[i];
            var fileInfo = new FileInfo(segment.File);
            
            var key = new CacheKey(
                segment.File,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc,
                canvas.Width,
                canvas.Height,
                canvas.Fps,
                bgLayer.Scale,
                bgLayer.Opacity,
                bgLayer.ScaleMode.ToString(),
                new Dictionary<string, string>
                {
                    ["type"] = "background",
                    ["duration"] = segment.DurationFull.ToString("F3")
                });

            var hash = key.ComputeHash();
            var cachedPath = _store.GetPath(hash, ".mp4");

            if (!_store.Exists(hash, ".mp4"))
            {
                var tempPath = _store.GetTempPath(hash, ".mp4");
                
                var duration = BackgroundCacheRules.EncodeDurationSeconds(segment);

                var scaleExpr = bgLayer.ScaleMode == ScaleMode.Cover
                    ? $"w='trunc(max({canvas.Width}/iw,{canvas.Height}/ih)*iw*{bgLayer.Scale:0.###}/2)*2':h='trunc(max({canvas.Width}/iw,{canvas.Height}/ih)*ih*{bgLayer.Scale:0.###}/2)*2'"
                    : $"w={canvas.Width}:h={canvas.Height}";

                var filter = $"[0:v]trim=duration={duration:0.###},setpts=PTS-STARTPTS," +
                             $"fps={canvas.Fps}," +
                             $"scale={scaleExpr}:flags=bicubic," +
                             $"crop=w='min(iw,{canvas.Width})':h='min(ih,{canvas.Height})'," +
                             $"pad={canvas.Width}:{canvas.Height}:(ow-iw)/2:(oh-ih)/2:color=black," +
                             $"setsar=1," +
                             $"format=gbrp," +
                             $"colorchannelmixer=rr={bgLayer.Opacity:0.###}:gg={bgLayer.Opacity:0.###}:bb={bgLayer.Opacity:0.###}[vout]";

                var args = new List<string>
                {
                    "-y", "-hide_banner", "-loglevel", "warning", "-nostdin"
                };
                RenderCommandBuilder.AddMediaInput(args, segment.File, hwAccel, loopImage: false, loopWave: false, fps: 0);
                args.Add("-an");
                args.Add("-filter_complex");
                args.Add(filter);
                args.Add("-map");
                args.Add("[vout]");
                RenderCommandBuilder.AppendVideoEncoder(args, encoder, 18);
                args.Add("-pix_fmt");
                args.Add("yuv420p");
                args.Add("-colorspace");
                args.Add("bt709");
                args.Add("-color_primaries");
                args.Add("bt709");
                args.Add("-color_trc");
                args.Add("bt709");
                args.Add("-color_range");
                args.Add("tv");
                args.Add("-f");
                args.Add("mp4");
                args.Add(tempPath);

                var result = await _runner.RunAsync(
                    args,
                    workingDirectory: null,
                    cancellationToken: cancellationToken,
                    progress: null,
                    totalDurationSeconds: duration).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Failed to prepare background {segment.File}: {result.Tail}");
                }

                _store.CommitTemp(tempPath, cachedPath);
            }

            results.Add(cachedPath);
            progress?.Report((i + 1.0) / total);
        }

        return results;
    }

    public async Task<string> PrepareAvatarAsync(
        Template template,
        string avatarPath,
        MediaInfo avatarInfo,
        CancellationToken cancellationToken = default)
    {
        var canvas = template.Canvas;
        var avatarLayer = template.Layers.FirstOrDefault(l => l.Type == LayerType.Image);
        if (avatarLayer is null)
        {
            throw new InvalidOperationException("No avatar layer found");
        }

        var fileInfo = new FileInfo(avatarPath);
        var key = new CacheKey(
            avatarPath,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc,
            canvas.Width,
            canvas.Height,
            canvas.Fps,
            avatarLayer.Transform?.Scale ?? 1.0,
            avatarLayer.Opacity,
            "avatar",
            new Dictionary<string, string>
            {
                ["type"] = "avatar",
                ["target_height"] = canvas.Height.ToString()
            });

        var hash = key.ComputeHash();
        var cachedPath = _store.GetPath(hash, Path.GetExtension(avatarPath));

        if (!_store.Exists(hash, Path.GetExtension(avatarPath)))
        {
            var tempPath = _store.GetTempPath(hash, Path.GetExtension(avatarPath));
            
            var scale = avatarLayer.Transform?.Scale ?? 1.0;
            var targetHeight = (int)(canvas.Height * scale);
            var aspectRatio = (double)(avatarInfo.Width ?? canvas.Width) / (avatarInfo.Height ?? canvas.Height);
            var targetWidth = (int)(targetHeight * aspectRatio);
            targetWidth = (targetWidth / 2) * 2;

            var args = new List<string>
            {
                "-y", "-hide_banner", "-loglevel", "warning", "-nostdin",
                "-i", Path.GetFullPath(avatarPath),
                "-vf", $"scale=w={targetWidth}:h={targetHeight}:force_original_aspect_ratio=decrease",
                "-c:v", "png",
                tempPath
            };

            var result = await _runner.RunAsync(
                args,
                workingDirectory: null,
                cancellationToken: cancellationToken,
                progress: null,
                totalDurationSeconds: null).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to prepare avatar {avatarPath}: {result.Tail}");
            }

            _store.CommitTemp(tempPath, cachedPath);
        }

        return cachedPath;
    }

    public async Task<string> PrepareWaveAsync(
        Template template,
        RenderJobPlan job,
        MediaInfo waveInfo,
        CancellationToken cancellationToken = default)
    {
        var canvas = template.Canvas;
        var waveLayer = FilterGraphBuilder.FindWaveLayer(template);
        if (waveLayer is null)
        {
            throw new InvalidOperationException("No wave layer found");
        }

        var wavePath = job.WavePath ?? waveInfo.Path;
        var fileInfo = new FileInfo(wavePath);
        var rect = FilterGraphBuilder.ResolveWaveRect(template, job, waveLayer, waveInfo);
        var key = new CacheKey(
            wavePath,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc,
            canvas.Width,
            canvas.Height,
            canvas.Fps,
            1.0,
            waveLayer.Opacity,
            "wave",
            new Dictionary<string, string>
            {
                ["type"] = "wave-baked",
                ["w"] = rect.Width.ToString(),
                ["h"] = rect.Height.ToString(),
                ["alpha"] = waveInfo.HasAlpha ? "1" : "0",
                ["tolerance"] = waveLayer.BlackKeyTolerance.ToString("F3")
            });

        var hash = key.ComputeHash();
        var cachedPath = _store.GetPath(hash, ".mov");

        if (!_store.Exists(hash, ".mov"))
        {
            var tempPath = _store.GetTempPath(hash, ".mov");
            var vf = $"fps={canvas.Fps},scale=w={rect.Width}:h={rect.Height}:flags=bicubic,format=rgba";
            if (!waveInfo.HasAlpha)
            {
                vf += $",lumakey=threshold=0:tolerance={waveLayer.BlackKeyTolerance:0.###}:softness={waveLayer.BlackKeyTolerance:0.###}";
            }

            var args = new List<string>
            {
                "-y", "-hide_banner", "-loglevel", "warning", "-nostdin",
                "-i", Path.GetFullPath(wavePath),
                "-an",
                "-vf", vf,
                "-c:v", "qtrle",
                "-pix_fmt", "argb",
                tempPath
            };

            var result = await _runner.RunAsync(
                args,
                workingDirectory: null,
                cancellationToken: cancellationToken,
                progress: null,
                totalDurationSeconds: null).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to prepare wave {wavePath}: {result.Tail}");
            }

            _store.CommitTemp(tempPath, cachedPath);
        }

        return cachedPath;
    }

}

