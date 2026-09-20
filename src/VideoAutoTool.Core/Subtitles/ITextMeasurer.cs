using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Subtitles;

public interface ITextMeasurer
{
    double MeasureWidth(string text, StylePreset preset);

    IReadOnlyList<string> WrapText(string text, StylePreset preset, double maxWidth, double safetyFactor = 0.97);
}
