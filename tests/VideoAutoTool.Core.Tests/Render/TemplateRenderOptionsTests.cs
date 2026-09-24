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

    [Fact]
    public void ApplyOutputHeight_720_LeavesDesignCanvas()
    {
        var template = TemplateDefaults.CreateCo139();
        TemplateRenderOptions.ApplyOutputHeight(template, 720);
        Assert.Equal(1280, template.Canvas.Width);
        Assert.Equal(720, template.Canvas.Height);
        Assert.Equal(72, template.StylePresets[0].Size);
    }

    [Fact]
    public void ApplyOutputHeight_480_ScalesCanvasLayersAndText()
    {
        var template = TemplateDefaults.CreateCo139();
        TemplateRenderOptions.ApplyOutputHeight(template, 480);
        Assert.Equal(854, template.Canvas.Width);
        Assert.Equal(480, template.Canvas.Height);
        Assert.Equal(48, template.StylePresets[0].Size);
        Assert.Equal(320, template.StylePresets[0].Box.Width, 1);
        Assert.Equal(374, template.Layers.First(l => l.Id == "avatar").Transform!.Width);
        Assert.Equal(1.5, template.Layers.First(l => l.Type == LayerType.BackgroundChain).Scale, 3);
    }

    [Fact]
    public void NormalizeOutputHeight_OnlyAllows480Or720()
    {
        Assert.Equal(480, TemplateRenderOptions.NormalizeOutputHeight(480));
        Assert.Equal(720, TemplateRenderOptions.NormalizeOutputHeight(720));
        Assert.Equal(720, TemplateRenderOptions.NormalizeOutputHeight(0));
        Assert.Equal(720, TemplateRenderOptions.NormalizeOutputHeight(1080));
    }
}
