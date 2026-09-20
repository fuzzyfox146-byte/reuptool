using SkiaSharp;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Subtitles;

public sealed class SkiaTextMeasurer : ITextMeasurer, IDisposable
{
    public double MeasureWidth(string text, StylePreset preset)
    {
        using var paint = CreatePaint(preset);
        return paint.MeasureText(text);
    }

    public IReadOnlyList<string> WrapText(string text, StylePreset preset, double maxWidth, double safetyFactor = 0.97)
    {
        var limit = maxWidth * safetyFactor;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (MeasureWidth(candidate, preset) <= limit)
            {
                current = candidate;
            }
            else
            {
                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                }

                current = word;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        return lines;
    }

    private static SKPaint CreatePaint(StylePreset preset)
    {
        var typeface = SKTypeface.FromFamilyName(preset.Font, preset.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
        return new SKPaint
        {
            Typeface = typeface,
            TextSize = preset.Size,
            IsAntialias = true
        };
    }

    public void Dispose()
    {
    }
}
