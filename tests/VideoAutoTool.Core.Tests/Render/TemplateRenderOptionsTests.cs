using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Render;

public class TemplateRenderOptionsTests
{
    [Fact]
    public void ApplyBackgroundScalePercent_SetsCoverScaleOnBackgroundLayer()
    {
        var template = TemplateDefaults.CreateCo139();
        TemplateRenderOptions.ApplyBackgroundScalePercent(template, 120);
        Assert.Equal(1.2, template.Layers.First(l => l.Type == LayerType.BackgroundChain).Scale, 3);
    }

    [Fact]
    public void ClampScalePercent_StaysInRange()
    {
        Assert.Equal(100, TemplateRenderOptions.ClampScalePercent(50));
        Assert.Equal(250, TemplateRenderOptions.ClampScalePercent(400));
        Assert.Equal(150, TemplateRenderOptions.ClampScalePercent(150));
    }
}
