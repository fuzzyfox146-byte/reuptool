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
        Assert.Contains("white-slab".Replace("white-slab", preset.Font), ass);
    }
}
