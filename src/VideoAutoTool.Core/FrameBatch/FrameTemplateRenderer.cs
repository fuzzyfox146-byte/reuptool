using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VideoAutoTool.Core.Subtitles;

namespace VideoAutoTool.Core.FrameBatch;

public static class FrameTemplateRenderer
{
    private static readonly Regex EmojiRegex = new(@"[\uD800-\uDBFF][\uDC00-\uDFFF]", RegexOptions.Compiled);

    public static (int X, int Y, int W, int H) ResolveLayerBox(
        FrameTemplateLayer layer,
        FrameSettings settings,
        bool mirroredLeft)
    {
        if (layer.FullCanvas || layer.Kind is FrameLayerKind.BackgroundVideo or FrameLayerKind.FrameOverlay && layer.Width <= 0)
        {
            return (0, 0, settings.Width, settings.Height);
        }

        if (layer.UseReferenceHole || layer.Kind == FrameLayerKind.MainVideo && layer.UseReferenceHole)
        {
            var scale = layer.Scale > 0.05 ? layer.Scale : settings.VideoScale;
            var holeLayout = FrameLayoutMath.Create(settings with { VideoScale = scale });
            var hx = mirroredLeft ? settings.Width - holeLayout.X - holeLayout.VideoW : holeLayout.X;
            return (hx, holeLayout.Y, holeLayout.VideoW, holeLayout.VideoH);
        }

        var ratioX = settings.Width / (double)FrameDesign.Width;
        var ratioY = settings.Height / (double)FrameDesign.Height;
        var w = FrameLayoutMath.Even(Math.Max(16, layer.Width) * ratioX);
        var h = FrameLayoutMath.Even(Math.Max(16, layer.Height) * ratioY);
        var x = (int)Math.Round(layer.X * ratioX);
        var y = (int)Math.Round(layer.Y * ratioY);
        if (layer.MirrorWithSide && mirroredLeft)
        {
            x = settings.Width - x - w;
        }

        return (x, y, w, h);
    }

    public static string WriteSubtitleAss(
        string destinationAssPath,
        FrameTemplateLayer layer,
        string? srtOrAssPath,
        string chosenColorHex,
        FrameSettings settings,
        double durationSeconds,
        bool mirroredLeft)
    {
        if (!string.IsNullOrWhiteSpace(srtOrAssPath)
            && srtOrAssPath.EndsWith(".ass", StringComparison.OrdinalIgnoreCase)
            && File.Exists(srtOrAssPath))
        {
            File.Copy(srtOrAssPath, destinationAssPath, overwrite: true);
            return destinationAssPath;
        }

        var cues = !string.IsNullOrWhiteSpace(srtOrAssPath) && File.Exists(srtOrAssPath)
            ? SrtParser.Parse(File.ReadAllText(srtOrAssPath))
            : [];

        var ratioX = settings.Width / (double)FrameDesign.Width;
        var ratioY = settings.Height / (double)FrameDesign.Height;
        var fontSize = Math.Max(12, (int)Math.Round(layer.FontSize * ratioY));
        var boxW = Math.Max(120, (int)Math.Round(layer.Width * ratioX));
        var centerX = (int)Math.Round(layer.X * ratioX);
        var centerY = (int)Math.Round(layer.Y * ratioY);
        if (layer.MirrorWithSide && mirroredLeft)
        {
            centerX = settings.Width - centerX;
        }

        var primary = ToAssColor(chosenColorHex);
        var outline = ToAssColor(layer.OutlineColor);
        var outlineW = Math.Max(0, layer.OutlineWidth * ratioY).ToString("0.##", CultureInfo.InvariantCulture);
        var shadowD = Math.Max(0, layer.ShadowDepth * ratioY).ToString("0.##", CultureInfo.InvariantCulture);
        var fontName = string.IsNullOrWhiteSpace(layer.FontName) ? "Arial" : layer.FontName.Replace(',', ' ');

        var sb = new StringBuilder();
        sb.AppendLine("[Script Info]");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine($"PlayResX: {settings.Width}");
        sb.AppendLine($"PlayResY: {settings.Height}");
        sb.AppendLine("WrapStyle: 2");
        sb.AppendLine("ScaledBorderAndShadow: yes");
        sb.AppendLine();
        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        sb.AppendLine($"Style: Default,{fontName},{fontSize},{primary},{primary},{outline},&H00000000,-1,0,0,0,100,100,0,0,1,{outlineW},{shadowD},5,20,20,20,1");
        sb.AppendLine();
        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        var maxCharsPerLine = Math.Max(12, (int)Math.Round(boxW / Math.Max(8.0, fontSize * 0.52)));
        if (layer.Kind == FrameLayerKind.StaticText && !string.IsNullOrWhiteSpace(layer.StaticText))
        {
            var text = FormatText(layer.StaticText, layer.StripEmoji, layer.Uppercase, maxCharsPerLine);
            sb.AppendLine($"Dialogue: 0,0:00:00.00,{FormatAssTime(TimeSpan.FromSeconds(Math.Max(1, durationSeconds)))},Default,,0,0,0,,{{\\an5\\pos({centerX},{centerY})}}{text}");
        }
        else
        {
            foreach (var cue in cues)
            {
                if (cue.Start.TotalSeconds >= durationSeconds)
                {
                    continue;
                }

                var end = cue.End.TotalSeconds > durationSeconds
                    ? TimeSpan.FromSeconds(durationSeconds)
                    : cue.End;
                var text = FormatText(cue.Text, layer.StripEmoji, layer.Uppercase, maxCharsPerLine);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                sb.AppendLine($"Dialogue: 0,{FormatAssTime(cue.Start)},{FormatAssTime(end)},Default,,0,0,0,,{{\\an5\\pos({centerX},{centerY})}}{text}");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationAssPath)!);
        File.WriteAllText(destinationAssPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return destinationAssPath;
    }

    public static IReadOnlyList<string> BuildRenderArgs(
        ResolvedTemplateJob job,
        FrameTemplatePreset preset,
        string destination,
        double duration,
        bool withAudio,
        FrameSettings settings,
        IReadOnlyDictionary<string, string>? preparedAssByLayerId = null)
    {
        if (preset.IsStandardThreeLayer())
        {
            var mainLayer = preset.Layers.First(layer => layer.Enabled && layer.Kind == FrameLayerKind.MainVideo);
            var effectiveSettings = settings with
            {
                VideoScale = mainLayer.Scale > 0.05 ? mainLayer.Scale : settings.VideoScale,
                VideoOpacity = mainLayer.Opacity
            };
            var layout = FrameLayoutMath.Create(effectiveSettings);
            return FrameCommands.Render(
                job.DriverVideo,
                job.PrimaryBackground,
                job.PrimaryFrame,
                destination,
                duration,
                withAudio,
                effectiveSettings,
                layout);
        }

        var effectiveDuration = preset.AudioSpeed > 0.1 && Math.Abs(preset.AudioSpeed - 1.0) > 0.001
            ? Math.Max(0.1, duration / preset.AudioSpeed)
            : Math.Max(0.1, duration);

        var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-nostdin" };
        var filters = new List<string>();

        // Input 0 is always the driver (for audio and optionally main video)
        var mainVideoUsed = !preset.AudioOnlyDriver && job.Layers.Any(item => item.Layer.Enabled && item.Layer.Kind == FrameLayerKind.MainVideo);
        if (!mainVideoUsed)
        {
            args.Add("-vn");
            args.Add("-sn");
            args.Add("-dn");
            args.Add("-i");
            args.Add(job.DriverVideo);
        }
        else if (settings.Gpu)
        {
            AddCudaInput(args, job.DriverVideo, loop: false);
        }
        else
        {
            args.Add("-i");
            args.Add(job.DriverVideo);
        }

        var nextInputIndex = 1;
        var currentLabel = "[base]";
        var baseCreated = false;
        var currentOnGpu = false;
        var step = 0;

        foreach (var entry in job.Layers)
        {
            var layer = entry.Layer;
            if (!layer.Enabled)
            {
                continue;
            }

            if (layer.Kind is FrameLayerKind.Subtitle or FrameLayerKind.StaticText)
            {
                if (preparedAssByLayerId is null || !preparedAssByLayerId.TryGetValue(layer.Id, out var assPath) || !File.Exists(assPath))
                {
                    continue;
                }

                step++;
                var nextLabel = $"[v{step}]";
                var escapedAss = EscapeFilterPath(assPath);
                if (currentOnGpu)
                {
                    filters.Add($"{currentLabel}hwdownload,format=yuv420p,subtitles=filename='{escapedAss}',hwupload_cuda{nextLabel}");
                }
                else
                {
                    if (!baseCreated)
                    {
                        filters.Add($"color=c=black:s={settings.Width}x{settings.Height}:r={settings.Fps}:d={effectiveDuration.ToString("0.000", CultureInfo.InvariantCulture)},format=yuv420p[base]");
                        baseCreated = true;
                    }

                    filters.Add($"{currentLabel}subtitles=filename='{escapedAss}'{nextLabel}");
                }

                currentLabel = nextLabel;
                continue;
            }

            int inputIndex;
            if (layer.Kind == FrameLayerKind.MainVideo)
            {
                if (preset.AudioOnlyDriver)
                {
                    continue;
                }

                inputIndex = 0;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(entry.AssetPath) || !File.Exists(entry.AssetPath))
                {
                    continue;
                }

                inputIndex = nextInputIndex++;
                AddLayerInput(args, layer, entry.AssetPath!, settings);
            }

            step++;
            var box = ResolveLayerBox(layer, settings, job.MirroredLeft);
            var layerLabel = $"[ly{step}]";
            var outLabel = $"[v{step}]";

            if (!baseCreated && layer.Kind == FrameLayerKind.BackgroundVideo && box.X == 0 && box.Y == 0 && box.W == settings.Width && box.H == settings.Height && layer.Opacity >= 0.999 && layer.KeyMode == FrameKeyMode.None)
            {
                filters.Add(BuildBaseBackgroundChain(inputIndex, layer, settings, outLabel));
                baseCreated = true;
                currentOnGpu = settings.Gpu && layer.HasNoCustomEffects();
                currentLabel = outLabel;
                continue;
            }

            if (!baseCreated)
            {
                filters.Add($"color=c=black:s={settings.Width}x{settings.Height}:r={settings.Fps}:d={effectiveDuration.ToString("0.000", CultureInfo.InvariantCulture)},format=yuv420p[base]");
                baseCreated = true;
                currentOnGpu = false;
            }

            if (settings.Gpu && currentOnGpu && CanOverlayOnGpu(layer))
            {
                filters.Add(BuildGpuOverlayLayerChain(inputIndex, layer, box, settings, layerLabel));
                filters.Add($"{currentLabel}{layerLabel}overlay_cuda=x={box.X}:y={box.Y}:eof_action=repeat{outLabel}");
                currentLabel = outLabel;
            }
            else
            {
                if (currentOnGpu)
                {
                    var cpuBase = $"[cpu{step}]";
                    filters.Add($"{currentLabel}hwdownload,format=yuv420p{cpuBase}");
                    currentLabel = cpuBase;
                    currentOnGpu = false;
                }

                filters.Add(BuildCpuOverlayLayerChain(inputIndex, layer, box, settings, layerLabel));
                filters.Add($"{currentLabel}{layerLabel}overlay={box.X}:{box.Y}:eof_action=repeat:format=auto{outLabel}");
                currentLabel = outLabel;
            }
        }

        if (!baseCreated)
        {
            filters.Add($"color=c=black:s={settings.Width}x{settings.Height}:r={settings.Fps}:d={effectiveDuration.ToString("0.000", CultureInfo.InvariantCulture)},format=yuv420p[out]");
        }
        else if (settings.Gpu && !currentOnGpu)
        {
            filters.Add($"{currentLabel}format=yuv420p,hwupload_cuda[out]");
        }
        else if (!settings.Gpu)
        {
            filters.Add($"{currentLabel}format=yuv420p[out]");
        }
        else
        {
            filters.Add($"{currentLabel}copy[out]");
        }

        var mapCustomAudio = false;
        if (withAudio)
        {
            var audioFilters = BuildAudioFilterChain(preset, effectiveDuration, settings.Gpu);
            if (audioFilters is not null)
            {
                filters.Add(audioFilters);
                mapCustomAudio = true;
            }
        }

        args.Add("-filter_complex");
        args.Add(string.Join(';', filters));
        args.Add("-map");
        args.Add("[out]");
        if (withAudio)
        {
            args.Add("-map");
            args.Add(mapCustomAudio ? "[aout]" : "0:a:0");
            args.Add("-c:a");
            args.Add("aac");
            args.Add("-b:a");
            args.Add("192k");
            if (settings.Gpu)
            {
                args.Add("-ar");
                args.Add("48000");
                args.Add("-max_muxing_queue_size");
                args.Add("200000");
                args.Add("-muxing_queue_data_threshold");
                args.Add("1073741824");
            }
        }
        else
        {
            args.Add("-an");
        }

        args.Add("-t");
        args.Add(effectiveDuration.ToString("0.000", CultureInfo.InvariantCulture));
        args.Add("-r");
        args.Add(settings.Fps.ToString());
        args.Add("-threads");
        args.Add((settings.Gpu ? Math.Min(2, Math.Max(1, settings.Threads)) : settings.Threads).ToString());
        args.Add("-progress");
        args.Add("pipe:1");
        args.Add("-nostats");
        if (settings.Gpu)
        {
            args.Add("-c:v");
            args.Add("h264_nvenc");
            args.Add("-preset");
            args.Add("p1");
            args.Add("-tune");
            args.Add("hq");
            args.Add("-rc");
            args.Add("vbr");
            args.Add("-cq");
            args.Add(settings.Crf.ToString(CultureInfo.InvariantCulture));
            args.Add("-b:v");
            args.Add("0");
            args.Add("-bf");
            args.Add("0");
            args.Add("-profile:v");
            args.Add("high");
        }
        else
        {
            args.Add("-c:v");
            args.Add("libx264");
            args.Add("-preset");
            args.Add(settings.Preset);
            args.Add("-crf");
            args.Add(settings.Crf.ToString(CultureInfo.InvariantCulture));
            args.Add("-profile:v");
            args.Add("high");
            args.Add("-pix_fmt");
            args.Add("yuv420p");
        }

        args.Add("-colorspace");
        args.Add("bt709");
        args.Add("-color_primaries");
        args.Add("bt709");
        args.Add("-color_trc");
        args.Add("bt709");
        args.Add("-color_range");
        args.Add("tv");
        args.Add("-movflags");
        args.Add("+faststart");
        args.Add(destination);
        return args;
    }

    private static bool CanOverlayOnGpu(FrameTemplateLayer layer) =>
        layer.HasNoCustomEffects()
        && layer.Kind is FrameLayerKind.MainVideo or FrameLayerKind.FrameOverlay or FrameLayerKind.ImageOverlay or FrameLayerKind.LoopVideo;

    private static string BuildBaseBackgroundChain(
        int inputIndex,
        FrameTemplateLayer layer,
        FrameSettings settings,
        string outputLabel)
    {
        if (settings.Gpu && layer.HasNoCustomEffects())
        {
            return $"[{inputIndex}:v]scale_cuda={settings.Width}:{settings.Height}:format=yuv420p{outputLabel}";
        }

        var parts = new List<string>();
        if (settings.Gpu)
        {
            parts.Add("hwdownload");
            parts.Add("format=nv12");
        }

        AppendCommonCpuEffects(parts, layer, (0, 0, settings.Width, settings.Height), settings, isAlphaOverlay: false);
        return $"[{inputIndex}:v]{string.Join(',', parts)}{outputLabel}";
    }

    private static string BuildGpuOverlayLayerChain(
        int inputIndex,
        FrameTemplateLayer layer,
        (int X, int Y, int W, int H) box,
        FrameSettings settings,
        string outputLabel)
    {
        var opacity = Math.Clamp(layer.Opacity, 0.0, 1.0).ToString("0.00", CultureInfo.InvariantCulture);
        if (layer.Kind is FrameLayerKind.FrameOverlay or FrameLayerKind.ImageOverlay)
        {
            var scalePart = box.W == settings.Width && box.H == settings.Height
                ? ""
                : $"scale={box.W}:{box.H}:force_original_aspect_ratio=decrease,";
            var alphaPart = layer.Opacity < 0.999 ? $"colorchannelmixer=aa={opacity}," : "";
            return $"[{inputIndex}:v]format=rgba,{scalePart}{alphaPart}format=yuva420p,hwupload_cuda{outputLabel}";
        }

        return
            $"[{inputIndex}:v]scale_cuda={box.W}:{box.H}:format=nv12:force_original_aspect_ratio=increase:force_divisible_by=2," +
            $"hwdownload,format=nv12,format=rgba,colorchannelmixer=aa={opacity},format=yuva420p,hwupload_cuda{outputLabel}";
    }

    private static string BuildCpuOverlayLayerChain(
        int inputIndex,
        FrameTemplateLayer layer,
        (int X, int Y, int W, int H) box,
        FrameSettings settings,
        string outputLabel)
    {
        var parts = new List<string>();
        if (settings.Gpu && IsVideoInput(layer.Kind))
        {
            parts.Add("hwdownload");
            parts.Add("format=nv12");
        }

        AppendCommonCpuEffects(parts, layer, box, settings, isAlphaOverlay: true);
        return $"[{inputIndex}:v]{string.Join(',', parts)}{outputLabel}";
    }

    private static void AppendCommonCpuEffects(
        List<string> parts,
        FrameTemplateLayer layer,
        (int X, int Y, int W, int H) box,
        FrameSettings settings,
        bool isAlphaOverlay)
    {
        if (Math.Abs(layer.Speed - 1.0) > 0.01 && layer.Speed > 0.05)
        {
            parts.Add($"setpts=PTS/{layer.Speed.ToString("0.###", CultureInfo.InvariantCulture)}");
        }
        else
        {
            parts.Add("setpts=PTS-STARTPTS");
        }

        parts.Add($"fps={settings.Fps}");

        if (layer.CropLeft > 0.001 || layer.CropRight > 0.001 || layer.CropTop > 0.001 || layer.CropBottom > 0.001)
        {
            var cw = Math.Clamp(1.0 - layer.CropLeft - layer.CropRight, 0.1, 1.0).ToString("0.###", CultureInfo.InvariantCulture);
            var ch = Math.Clamp(1.0 - layer.CropTop - layer.CropBottom, 0.1, 1.0).ToString("0.###", CultureInfo.InvariantCulture);
            var cx = Math.Clamp(layer.CropLeft, 0.0, 0.85).ToString("0.###", CultureInfo.InvariantCulture);
            var cy = Math.Clamp(layer.CropTop, 0.0, 0.85).ToString("0.###", CultureInfo.InvariantCulture);
            parts.Add($"crop=iw*{cw}:ih*{ch}:iw*{cx}:ih*{cy}");
        }

        if (layer.FlipHorizontal)
        {
            parts.Add("hflip");
        }

        if (layer.FlipVertical)
        {
            parts.Add("vflip");
        }

        if (layer.Kind is FrameLayerKind.ImageOverlay or FrameLayerKind.FrameOverlay)
        {
            parts.Add("format=rgba");
            parts.Add($"scale={box.W}:{box.H}:force_original_aspect_ratio=decrease");
        }
        else
        {
            parts.Add($"scale={box.W}:{box.H}:force_original_aspect_ratio=increase");
            parts.Add($"crop={box.W}:{box.H}");
            parts.Add("setsar=1");
        }

        if (Math.Abs(layer.Brightness) > 0.001 || Math.Abs(layer.Contrast - 1.0) > 0.001 || Math.Abs(layer.Saturation - 1.0) > 0.001)
        {
            var b = Math.Clamp(layer.Brightness, -1.0, 1.0).ToString("0.##", CultureInfo.InvariantCulture);
            var c = Math.Clamp(layer.Contrast, 0.1, 3.0).ToString("0.##", CultureInfo.InvariantCulture);
            var s = Math.Clamp(layer.Saturation, 0.0, 3.0).ToString("0.##", CultureInfo.InvariantCulture);
            parts.Add($"eq=brightness={b}:contrast={c}:saturation={s}");
        }

        if (layer.Blur > 0.05)
        {
            var radius = Math.Clamp((int)Math.Round(layer.Blur), 1, 40);
            parts.Add($"boxblur={radius}:1");
        }

        if (isAlphaOverlay)
        {
            parts.Add("format=rgba");
            var tol = Math.Clamp(layer.KeyTolerance, 0.01, 0.80).ToString("0.###", CultureInfo.InvariantCulture);
            var soft = Math.Clamp(layer.KeySoftness, 0.0, 0.80).ToString("0.###", CultureInfo.InvariantCulture);
            if (layer.KeyMode == FrameKeyMode.BlackKey)
            {
                parts.Add($"lumakey=threshold=0:tolerance={tol}:softness={soft}");
            }
            else if (layer.KeyMode == FrameKeyMode.GreenKey)
            {
                parts.Add($"colorkey=0x00FF00:{tol}:{soft}");
            }
            else if (layer.KeyMode == FrameKeyMode.CustomColorKey)
            {
                var hex = NormalizeHex(layer.KeyColor);
                parts.Add($"colorkey=0x{hex}:{tol}:{soft}");
            }

            if (layer.Opacity < 0.999)
            {
                var opacity = Math.Clamp(layer.Opacity, 0.0, 1.0).ToString("0.00", CultureInfo.InvariantCulture);
                parts.Add($"colorchannelmixer=aa={opacity}");
            }
        }
        else
        {
            parts.Add("format=yuv420p");
        }
    }

    private static string? BuildAudioFilterChain(FrameTemplatePreset preset, double durationSeconds, bool gpu)
    {
        var hasSpeed = Math.Abs(preset.AudioSpeed - 1.0) > 0.001;
        var hasPitch = Math.Abs(preset.AudioPitchSemitones) > 0.001;
        var hasVolume = Math.Abs(preset.AudioVolume - 1.0) > 0.001;
        if (!gpu && !hasSpeed && !hasPitch && !hasVolume)
        {
            return null;
        }

        var chain = new List<string>();
        if (hasPitch)
        {
            var pitchFactor = Math.Pow(2.0, Math.Clamp(preset.AudioPitchSemitones, -12.0, 12.0) / 12.0);
            var tempoCompensation = Math.Clamp(preset.AudioSpeed / pitchFactor, 0.5, 2.0);
            chain.Add($"asetrate=48000*{pitchFactor.ToString("0.####", CultureInfo.InvariantCulture)}");
            chain.Add("aresample=48000");
            chain.Add($"atempo={tempoCompensation.ToString("0.####", CultureInfo.InvariantCulture)}");
        }
        else if (hasSpeed)
        {
            var speed = Math.Clamp(preset.AudioSpeed, 0.5, 2.0);
            chain.Add($"atempo={speed.ToString("0.####", CultureInfo.InvariantCulture)}");
        }

        if (hasVolume)
        {
            var vol = Math.Clamp(preset.AudioVolume, 0.0, 4.0).ToString("0.##", CultureInfo.InvariantCulture);
            chain.Add($"volume={vol}");
        }

        var seconds = Math.Max(0.1, durationSeconds).ToString("0.000", CultureInfo.InvariantCulture);
        chain.Add($"atrim=duration={seconds}");
        chain.Add("asetpts=PTS-STARTPTS");
        chain.Add("aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo");
        return $"[0:a]{string.Join(',', chain)}[aout]";
    }

    private static void AddLayerInput(
        List<string> args,
        FrameTemplateLayer layer,
        string path,
        FrameSettings settings)
    {
        if (layer.Kind is FrameLayerKind.FrameOverlay or FrameLayerKind.ImageOverlay)
        {
            args.Add("-loop");
            args.Add("1");
            args.Add("-framerate");
            args.Add(settings.Fps.ToString(CultureInfo.InvariantCulture));
            args.Add("-i");
            args.Add(path);
            return;
        }

        var useCuda = settings.Gpu && layer.HasNoCustomEffects();
        if (useCuda)
        {
            AddCudaInput(args, path, loop: true);
        }
        else
        {
            args.Add("-stream_loop");
            args.Add("-1");
            args.Add("-i");
            args.Add(path);
        }
    }

    private static bool IsVideoInput(FrameLayerKind kind) =>
        kind is FrameLayerKind.BackgroundVideo or FrameLayerKind.MainVideo or FrameLayerKind.LoopVideo;

    private static void AddCudaInput(List<string> args, string path, bool loop)
    {
        if (loop)
        {
            args.Add("-stream_loop");
            args.Add("-1");
        }

        args.Add("-hwaccel");
        args.Add("cuda");
        args.Add("-hwaccel_output_format");
        args.Add("cuda");
        args.Add("-i");
        args.Add(path);
    }

    private static string EscapeFilterPath(string path) =>
        path.Replace('\\', '/').Replace(":", "\\:").Replace("'", "\\'");

    private static string FormatText(string raw, bool stripEmoji, bool upper, int maxCharsPerLine)
    {
        var text = stripEmoji ? EmojiRegex.Replace(raw, string.Empty).Trim() : raw.Trim();
        if (upper)
        {
            text = text.ToUpperInvariant();
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1)
        {
            return text;
        }

        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > maxCharsPerLine)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return string.Join("\\N", lines);
    }

    private static string FormatAssTime(TimeSpan time) =>
        $"{(int)time.TotalHours:0}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}";

    private static string ToAssColor(string hex)
    {
        var clean = NormalizeHex(hex);
        var r = clean[..2];
        var g = clean.Substring(2, 2);
        var b = clean.Substring(4, 2);
        return $"&H00{b}{g}{r}".ToUpperInvariant();
    }

    private static string NormalizeHex(string? hex)
    {
        var clean = (hex ?? "FFFFFF").Trim().TrimStart('#');
        return clean.Length == 6 ? clean.ToUpperInvariant() : "FFFFFF";
    }
}
