using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Render;

public static class TemplateRenderOptions
{
    public const int MinBackgroundScalePercent = 100;
    public const int MaxBackgroundScalePercent = 250;
    public const int DefaultBackgroundScalePercent = 150;

    public static double ClampScale(double scale) =>
        Math.Clamp(scale, MinBackgroundScalePercent / 100.0, MaxBackgroundScalePercent / 100.0);

    public static int ClampScalePercent(int percent) =>
        Math.Clamp(percent, MinBackgroundScalePercent, MaxBackgroundScalePercent);

    public static void ApplyBackgroundScalePercent(Template template, int percent)
    {
        var scale = ClampScale(percent / 100.0);
        foreach (var layer in template.Layers.Where(l => l.Type == LayerType.BackgroundChain))
        {
            layer.Scale = scale;
        }
    }
}
