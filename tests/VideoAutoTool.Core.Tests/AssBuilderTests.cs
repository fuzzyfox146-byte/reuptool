using VideoAutoTool.Core.Fonts;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Subtitles;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests;

public class AssBuilderTests
{
    [Fact]
    public void Build_UsesPresetBoxPosition()
    {
        var template = TemplateDefaults.CreateCo139();
        var preset = template.StylePresets.First(p => p.Id == "white-slab");
        var job = new RenderJobPlan(
            1,
            "driver.mp4",
            "driver",
            "sub.srt",
            10,
            [],
            null,
            null,
            "right",
            preset,
            "out.mp4");
        var builder = new AssBuilder(new SkiaTextMeasurer(), new FontCatalog());
        var ass = builder.Build(job, template, [new Cue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "Line one")]);
        Assert.Contains("\\pos(", ass);
        Assert.Contains("PlayResX: 1280", ass);
        Assert.Contains("PlayResY: 720", ass);
        Assert.Contains(preset.Font, ass);
    }

    [Fact]
    public void Build_GoldPreset_PositionsCenterOfBox()
    {
        var template = TemplateDefaults.CreateCo139();
        var preset = template.StylePresets.First(p => p.Id == "gold-serif");
        var job = new RenderJobPlan(
            1,
            "driver.mp4",
            "driver",
            "sub.srt",
            10,
            [],
            null,
            null,
            "right",
            preset,
            "out.mp4");
        var builder = new AssBuilder(new SkiaTextMeasurer(), new FontCatalog());
        var ass = builder.Build(job, template, [new Cue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "Hello")]);
        Assert.Contains("\\an5\\pos(415,575)", ass);
        Assert.Contains("&H0023A6F5", ass);
        Assert.Contains("\\fs79", ass);
    }

    [Fact]
    public void ResolveFontSize_UsesLargerOfRequestedAndBox()
    {
        var preset = TemplateDefaults.CreateCo139().StylePresets[0];
        preset.Size = 40;
        preset.Box = new SubtitleBox { Height = 220, Width = 480 };
        Assert.Equal(79, AssBuilder.ResolveFontSize(preset));

        preset.Size = 120;
        Assert.Equal(120, AssBuilder.ResolveFontSize(preset));
    }
}
