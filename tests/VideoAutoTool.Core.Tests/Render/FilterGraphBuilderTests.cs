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
    }

    [Fact]
    public void BuildCached_CoversConcatToCanvas()
    {
        var template = TemplateDefaults.CreateCo139();
        var job = CreateJob(template, avatar: null, wave: null, sub: null);
        var graph = FilterGraphBuilder.BuildCached(template, job, @"D:\tmp\list.txt", null, null, includeSubtitles: false);

        Assert.Contains("pad=1280:720", graph.FilterComplex);
        Assert.Contains("1280x720", graph.FilterComplex);
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
