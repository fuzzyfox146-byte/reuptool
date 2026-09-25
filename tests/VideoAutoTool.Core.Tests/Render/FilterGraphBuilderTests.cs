using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Render;

public class FilterGraphBuilderTests
{
    [Fact]
    public void Build_UsesDesignBoxForAvatarAndFontsdir()
    {
        var template = TemplateDefaults.CreateCo139();
        template.Layers.First(l => l.Id == "avatar").Transform = new TransformSettings
        {
            Anchor = Anchor.BottomRight,
            X = 0,
            Y = 0,
            Width = 560,
            Height = 720
        };
        var job = CreateJob(template, avatar: @"D:\a\avatar.png", wave: null, sub: "sub.srt");
        var avatar = new MediaInfo(@"D:\a\avatar.png", null, false, null, true, 400, 800, null, "rgba", true, "png");
        var graph = FilterGraphBuilder.Build(template, job, avatar, null, includeSubtitles: true);

        Assert.Contains("overlay=x=100:y=0", graph.FilterComplex);
        Assert.Contains("ass=filename=sub.ass:fontsdir=fonts", graph.FilterComplex);
        Assert.Contains("1280x720", graph.FilterComplex);
        Assert.DoesNotContain("overlay=x=-", graph.FilterComplex);
        Assert.Contains("format=rgba,scale=w=", graph.FilterComplex);
        Assert.DoesNotContain("trim=duration=8,setpts=PTS-STARTPTS,format=rgba,scale=", graph.FilterComplex);
    }

    [Fact]
    public void BuildCached_PreparedWaveSkipsScaleAndKey()
    {
        var template = TemplateDefaults.CreateCo139();
        var job = CreateJob(template, avatar: null, wave: @"D:\a\wave.mp4", sub: null);
        var wave = new MediaInfo(@"D:\a\wave.mp4", 2, false, null, true, 640, 160, 25, "argb", true, "qtrle");
        var graph = FilterGraphBuilder.BuildCached(template, job, @"D:\tmp\list.txt", null, wave, includeSubtitles: false, wavePrepared: true);

        Assert.Contains("setpts=PTS-STARTPTS[wv]", graph.FilterComplex);
        Assert.DoesNotContain("lumakey=", graph.FilterComplex);
        Assert.DoesNotContain("scale=w=", graph.FilterComplex);
    }

    [Fact]
    public void BuildCached_CoversConcatToCanvas()
    {
        var template = TemplateDefaults.CreateCo139();
        var job = CreateJob(template, avatar: null, wave: null, sub: null);
        var graph = FilterGraphBuilder.BuildCached(template, job, @"D:\tmp\list.txt", null, null, includeSubtitles: false);

        Assert.Contains("[1:v]fps=", graph.FilterComplex);
        Assert.Contains("format=gbrp[L1]", graph.FilterComplex);
        Assert.DoesNotContain("pad=1280:720", graph.FilterComplex);
    }

    [Fact]
    public void ShouldUseGpuOverlay_OnlyAt720pAndAbove()
    {
        Assert.True(FilterGraphBuilder.ShouldUseGpuOverlay(new CanvasSettings { Width = 1280, Height = 720 }));
        Assert.False(FilterGraphBuilder.ShouldUseGpuOverlay(new CanvasSettings { Width = 854, Height = 480 }));
    }

    [Fact]
    public void BuildCachedGpu_UploadsPngAndKeysWaveOnCuda()
    {
        var template = TemplateDefaults.CreateCo139();
        var job = CreateJob(template, avatar: @"D:\a\avatar.png", wave: @"D:\a\wave.mp4", sub: "sub.srt");
        var avatar = new MediaInfo(@"D:\a\avatar.png", null, false, null, true, 400, 800, null, "rgba", true, "png");
        var wave = new MediaInfo(@"D:\a\wave.mp4", 2, false, null, true, 640, 160, 25, "yuv420p", false, "h264");
        var graph = FilterGraphBuilder.BuildCachedGpu(template, job, @"D:\tmp\list.txt", avatar, wave, includeSubtitles: true);

        Assert.Contains("scale_cuda=1280:720:format=yuv420p", graph.FilterComplex);
        Assert.Contains("format=yuva420p,hwupload_cuda[av]", graph.FilterComplex);
        Assert.Contains("overlay_cuda=", graph.FilterComplex);
        Assert.Contains("chromakey_cuda=0x000000:", graph.FilterComplex);
        Assert.Contains("ass=filename=sub.ass:fontsdir=fonts", graph.FilterComplex);
        Assert.DoesNotContain("lumakey=", graph.FilterComplex);
    }

    [Fact]
    public void BuildCachedGpu_AlphaWaveStaysSoftwareUntilUpload()
    {
        var template = TemplateDefaults.CreateCo139();
        var job = CreateJob(template, avatar: null, wave: @"D:\a\wave.mov", sub: null);
        var wave = new MediaInfo(@"D:\a\wave.mov", 2, false, null, true, 640, 160, 25, "argb", true, "qtrle");
        var graph = FilterGraphBuilder.BuildCachedGpu(template, job, @"D:\tmp\list.txt", null, wave, includeSubtitles: false);

        Assert.Contains("format=yuva420p,hwupload_cuda[wv]", graph.FilterComplex);
        Assert.DoesNotContain("chromakey_cuda", graph.FilterComplex);
        Assert.Contains("hwdownload,format=yuv420p,hwupload_cuda[vout]", graph.FilterComplex);
    }

    [Fact]
    public void BuildCachedGpu_PreparedAlphaWaveSkipsScale()
    {
        var template = TemplateDefaults.CreateCo139();
        var job = CreateJob(template, avatar: null, wave: @"D:\a\wave.mov", sub: null);
        var wave = new MediaInfo(@"D:\a\wave.mov", 2, false, null, true, 500, 80, 25, "argb", true, "qtrle");
        var graph = FilterGraphBuilder.BuildCachedGpu(template, job, @"D:\tmp\list.txt", null, wave, includeSubtitles: false, wavePrepared: true);

        Assert.Contains("format=yuva420p,hwupload_cuda[wv]", graph.FilterComplex);
        Assert.DoesNotContain("scale=w=", graph.FilterComplex);
        Assert.DoesNotContain("chromakey_cuda", graph.FilterComplex);
    }

    private static RenderJobPlan CreateJob(Template template, string? avatar, string? wave, string? sub) =>
        new(
            1,
            @"D:\src\a.mp4",
            "a",
            sub,
            8,
            [new BackgroundSegment(@"D:\bg\clip.mp4", 10)],
            avatar,
            wave,
            "right",
            template.StylePresets[0],
            @"D:\out\a.mp4");
}
