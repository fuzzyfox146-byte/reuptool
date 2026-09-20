using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VideoAutoTool.Core.Fonts;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Subtitles;

public sealed class AssBuilder
{
    private static readonly Regex EmojiRegex = new(@"[\uD800-\uDBFF][\uDC00-\uDFFF]", RegexOptions.Compiled);

    private readonly ITextMeasurer _measurer;
    private readonly IFontCatalog _fontCatalog;

    public AssBuilder(ITextMeasurer measurer, IFontCatalog fontCatalog)
    {
        _measurer = measurer;
        _fontCatalog = fontCatalog;
    }

    public string Build(RenderJobPlan job, Template template, IReadOnlyList<Cue> cues)
    {
        var preset = job.StylePreset;
        var canvas = template.Canvas;
        var (px, py, an) = LayerGeometry.SubtitleAnchorPoint(
            preset.Box, preset.TextAlign, preset.VerticalAlign, job.Side, true, canvas.Width);

        var sb = new StringBuilder();
        sb.AppendLine("[Script Info]");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine($"PlayResX: {canvas.Width}");
        sb.AppendLine($"PlayResY: {canvas.Height}");
        sb.AppendLine("WrapStyle: 2");
        sb.AppendLine("ScaledBorderAndShadow: yes");
        sb.AppendLine();
        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        sb.AppendLine(string.Join(',',
            "Default",
            Escape(preset.Font),
            preset.Size.ToString(CultureInfo.InvariantCulture),
            ToAssColor(preset.Color),
            ToAssColor(preset.Color),
            ToAssColor(preset.OutlineColor),
            "&H00000000",
            preset.Bold ? "-1" : "0",
            "0", "0", "0", "100", "100", "0", "0", "1",
            preset.Outline.ToString("0.##", CultureInfo.InvariantCulture),
            preset.Shadow.ToString("0.##", CultureInfo.InvariantCulture),
            "5", "20", "20", "20", "1"));
        sb.AppendLine();
        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        foreach (var cue in cues)
        {
            if (cue.Start.TotalSeconds >= job.DurationSeconds)
            {
                continue;
            }

            var end = cue.End.TotalSeconds > job.DurationSeconds
                ? TimeSpan.FromSeconds(job.DurationSeconds)
                : cue.End;
            var text = cue.Text;
            if (template.Layers.FirstOrDefault(l => l.Type == LayerType.Subtitle)?.StripEmoji == true)
            {
                text = EmojiRegex.Replace(text, string.Empty).Trim();
            }

            var wrapped = _measurer.WrapText(text, preset, preset.Box.Width);
            var lineText = string.Join("\\N", wrapped);
            sb.AppendLine($"Dialogue: 0,{FormatAssTime(cue.Start)},{FormatAssTime(end)},Default,,0,0,0,,{{\\an{an}\\pos({px},{py})}}{lineText}");
        }

        _ = _fontCatalog.Exists(preset);
        return sb.ToString();
    }

    public void WriteToFile(string path, RenderJobPlan job, Template template, IReadOnlyList<Cue> cues) =>
        File.WriteAllText(path, Build(job, template, cues), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    private static string FormatAssTime(TimeSpan time)
    {
        var total = time.TotalHours;
        return $"{(int)total:0}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}";
    }

    private static string ToAssColor(string hex)
    {
        var value = hex.TrimStart('#');
        if (value.Length == 6)
        {
            var r = value[..2];
            var g = value.Substring(2, 2);
            var b = value.Substring(4, 2);
            return $"&H00{b}{g}{r}".ToUpperInvariant();
        }

        return "&H00FFFFFF";
    }

    private static string Escape(string value) => value.Replace(',', ' ');
}
