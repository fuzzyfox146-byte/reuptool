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

    public const int OutputHeight720 = 720;
    public const int OutputHeight480 = 480;

    public static int NormalizeOutputHeight(int height) =>
        height == OutputHeight480 ? OutputHeight480 : OutputHeight720;

    public static void ApplyOutputHeight(Template template, int outputHeight)
    {
        outputHeight = NormalizeOutputHeight(outputHeight);
        var canvas = template.Canvas;
        if (canvas.Height <= 0 || canvas.Height == outputHeight)
        {
            return;
        }

        var scale = outputHeight / (double)canvas.Height;
        canvas.Width = LayerGeometry.MakeEven(Math.Max(2, (int)Math.Round(canvas.Width * scale)));
        canvas.Height = outputHeight;

        foreach (var layer in template.Layers)
        {
            layer.OffsetX *= scale;
            layer.OffsetY *= scale;
            if (layer.Transform is not null)
            {
                layer.Transform.X *= scale;
                layer.Transform.Y *= scale;
                if (layer.Transform.Width is int width)
                {
                    layer.Transform.Width = ScaleEven(width, scale);
                }

                if (layer.Transform.Height is int height)
                {
                    layer.Transform.Height = ScaleEven(height, scale);
                }
            }
        }

        foreach (var preset in template.StylePresets)
        {
            preset.Size = Math.Clamp((int)Math.Round(preset.Size * scale), 8, 200);
            preset.Outline *= scale;
            preset.Shadow *= scale;
            preset.Box.X *= scale;
            preset.Box.Y *= scale;
            preset.Box.Width *= scale;
            preset.Box.Height *= scale;
        }
    }

    private static int ScaleEven(int value, double scale) =>
        LayerGeometry.MakeEven(Math.Max(2, (int)Math.Round(value * scale)));
}
