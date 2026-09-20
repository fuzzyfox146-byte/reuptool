using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Render;

public sealed record FilterGraphPlan(
    IReadOnlyList<string> ExtraInputs,
    string FilterComplex,
    string VideoOutputLabel);

public static class FilterGraphBuilder
{
    public static FilterGraphPlan Build(
        Template template,
        RenderJobPlan job,
        MediaInfo? avatarInfo,
        MediaInfo? waveInfo,
        bool includeSubtitles = true)
    {
        var canvas = template.Canvas;
        var bgLayer = template.Layers.First(l => l.Type == LayerType.BackgroundChain);
        var avatarLayer = FindAvatarLayer(template);
        var waveLayer = template.Layers.FirstOrDefault(l => l.Type == LayerType.LoopVideo);

        var filters = new List<string>();
        var extraInputs = new List<string>();
        var inputIndex = 1;

        filters.Add($"color=c=black:s={canvas.Width}x{canvas.Height}:r={canvas.Fps}:d={job.DurationSeconds:0.###},format=gbrp[base]");

        var segmentLabels = new List<string>();
        foreach (var segment in job.Backgrounds)
        {
            var label = $"s{inputIndex}";
            var segDuration = segment == job.Backgrounds[^1]
                ? Math.Max(0.01, job.DurationSeconds - job.Backgrounds.Take(job.Backgrounds.Count - 1).Sum(s => s.DurationFull))
                : segment.DurationFull;
            var scaleExpr = bgLayer.ScaleMode == ScaleMode.Cover
                ? CoverScaleExpr(canvas, bgLayer.Scale)
                : $"w={canvas.Width}:h={canvas.Height}";
            filters.Add(
                $"[{inputIndex}:v]trim=duration={segDuration:0.###},setpts=PTS-STARTPTS,fps={canvas.Fps},scale={scaleExpr}:flags=bicubic,crop=w='min(iw,{canvas.Width})':h='min(ih,{canvas.Height})',pad={canvas.Width}:{canvas.Height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1[{label}]");
            extraInputs.Add(segment.File);
            segmentLabels.Add($"[{label}]");
            inputIndex++;
        }

        if (segmentLabels.Count == 0)
        {
            filters.Add("[base]format=rgba,colorchannelmixer=aa=0[bga]");
        }
        else if (segmentLabels.Count == 1)
        {
            filters.Add($"{segmentLabels[0]}copy[bgcat]");
            filters.Add($"[bgcat]format=rgba,colorchannelmixer=aa={bgLayer.Opacity:0.###}[bga]");
        }
        else
        {
            filters.Add($"{string.Join(string.Empty, segmentLabels)}concat=n={segmentLabels.Count}:v=1:a=0[bgcat]");
            filters.Add($"[bgcat]format=rgba,colorchannelmixer=aa={bgLayer.Opacity:0.###}[bga]");
        }

        filters.Add("[base][bga]overlay=eof_action=repeat:format=gbrp[L1]");
        var current = "[L1]";
        AppendOverlays(
            filters,
            extraInputs,
            ref inputIndex,
            ref current,
            template,
            job,
            avatarLayer,
            waveLayer,
            avatarInfo,
            waveInfo,
            includeSubtitles);
        return new FilterGraphPlan(extraInputs, string.Join(';', filters), "[vout]");
    }

    public static FilterGraphPlan BuildCached(
        Template template,
        RenderJobPlan job,
        string concatInputPath,
        MediaInfo? avatarInfo,
        MediaInfo? waveInfo,
        bool includeSubtitles = true)
    {
        var canvas = template.Canvas;
        var avatarLayer = FindAvatarLayer(template);
        var waveLayer = template.Layers.FirstOrDefault(l => l.Type == LayerType.LoopVideo);

        var filters = new List<string>();
        var extraInputs = new List<string> { concatInputPath };
        var inputIndex = 2;

        // Input 0: driver audio
        // Input 1: concat of backgrounds already prepared at canvas size + opacity.
        filters.Add($"[1:v]fps={canvas.Fps},trim=duration={job.DurationSeconds:0.###},setpts=PTS-STARTPTS,setsar=1,format=gbrp[L1]");
        var current = "[L1]";
        AppendOverlays(
            filters,
            extraInputs,
            ref inputIndex,
            ref current,
            template,
            job,
            avatarLayer,
            waveLayer,
            avatarInfo,
            waveInfo,
            includeSubtitles);
        return new FilterGraphPlan(extraInputs, string.Join(';', filters), "[vout]");
    }

    private static LayerDefinition? FindAvatarLayer(Template template) =>
        template.Layers.FirstOrDefault(l => l.Id == "avatar")
        ?? template.Layers.FirstOrDefault(l => l.Type == LayerType.Image);

    private static void AppendOverlays(
        List<string> filters,
        List<string> extraInputs,
        ref int inputIndex,
        ref string current,
        Template template,
        RenderJobPlan job,
        LayerDefinition? avatarLayer,
        LayerDefinition? waveLayer,
        MediaInfo? avatarInfo,
        MediaInfo? waveInfo,
        bool includeSubtitles)
    {
        var canvas = template.Canvas;
        if (job.AvatarPath is not null && avatarLayer is { Visible: true } && avatarInfo is not null)
        {
            var srcW = avatarInfo.Width ?? canvas.Width;
            var srcH = avatarInfo.Height ?? canvas.Height;
            var (aw, ah) = LayerGeometry.ScaleToFitHeight(
                avatarLayer.Transform?.Scale ?? 1.0,
                canvas.Height,
                srcW,
                srcH);
            var rect = LayerGeometry.ResolveFittedOverlay(
                avatarLayer.Transform,
                srcW,
                srcH,
                canvas.Width,
                canvas.Height,
                job.Side,
                avatarLayer.MirrorWithSide,
                avatarLayer.Transform?.Anchor ?? Anchor.BottomRight,
                avatarLayer.Transform?.X ?? 0,
                avatarLayer.Transform?.Y ?? 0,
                aw,
                ah);
            filters.Add($"[{inputIndex}:v]fps={canvas.Fps},trim=duration={job.DurationSeconds:0.###},setpts=PTS-STARTPTS,format=rgba,scale=w={rect.Width}:h={rect.Height}:force_original_aspect_ratio=decrease[av]");
            extraInputs.Add(job.AvatarPath);
            inputIndex++;
            filters.Add($"{current}[av]overlay=x={rect.Left}:y={rect.Top}:eof_action=repeat:format=gbrp[L2]");
            current = "[L2]";
        }

        if (job.WavePath is not null && waveLayer is { Visible: true } && waveInfo is not null)
        {
            var srcW = waveInfo.Width ?? waveLayer.Transform?.Width ?? 500;
            var srcH = waveInfo.Height ?? 100;
            var targetWidth = waveLayer.Transform?.Width ?? 500;
            var (ww, wh) = LayerGeometry.ScaleToFitWidth(targetWidth, srcW, srcH);
            var rect = LayerGeometry.ResolveFittedOverlay(
                waveLayer.Transform,
                srcW,
                srcH,
                canvas.Width,
                canvas.Height,
                job.Side,
                waveLayer.MirrorWithSide,
                waveLayer.Transform?.Anchor ?? Anchor.Center,
                waveLayer.Transform?.X ?? 0,
                waveLayer.Transform?.Y ?? 0,
                ww,
                wh);
            var waveChain = $"[{inputIndex}:v]fps={canvas.Fps},trim=duration={job.DurationSeconds:0.###},setpts=PTS-STARTPTS,format=rgba,scale=w={rect.Width}:h={rect.Height}:flags=bicubic";
            if (!waveInfo.HasAlpha)
            {
                waveChain += $",lumakey=threshold=0:tolerance={waveLayer.BlackKeyTolerance:0.###}:softness={waveLayer.BlackKeyTolerance:0.###}";
            }

            waveChain += "[wv]";
            filters.Add(waveChain);
            extraInputs.Add(job.WavePath);
            inputIndex++;
            filters.Add($"{current}[wv]overlay=x={rect.Left}:y={rect.Top}:eof_action=repeat:format=gbrp[L3]");
            current = "[L3]";
        }

        if (includeSubtitles)
        {
            filters.Add($"{current}ass=filename=sub.ass:fontsdir=fonts[L4]");
            current = "[L4]";
        }

        filters.Add($"{current}scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int,format=yuv420p[vout]");
    }

    private static string CoverScaleExpr(CanvasSettings canvas, double scale) =>
        $"w='trunc(max({canvas.Width}/iw,{canvas.Height}/ih)*iw*{scale:0.###}/2)*2':h='trunc(max({canvas.Width}/iw,{canvas.Height}/ih)*ih*{scale:0.###}/2)*2'";
}
