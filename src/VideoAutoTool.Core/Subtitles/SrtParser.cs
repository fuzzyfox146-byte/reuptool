using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VideoAutoTool.Core.Logging;

namespace VideoAutoTool.Core.Subtitles;

public static class SrtParser
{
    private static readonly Regex HtmlTagRegex = new(@"<\s*/?\s*i\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BraceTagRegex = new(@"\{[^}]*\}", RegexOptions.Compiled);
    private static readonly Regex TimeRegex = new(
        @"(\d{1,2}):(\d{2}):(\d{2})[,.](\d{1,3})\s*-->\s*(\d{1,2}):(\d{2}):(\d{2})[,.](\d{1,3})",
        RegexOptions.Compiled);

    public static List<Cue> ParseFile(string path, ILog? log = null)
    {
        var bytes = File.ReadAllBytes(path);
        var text = DecodeText(bytes);
        return Parse(text, log);
    }

    public static List<Cue> Parse(string text, ILog? log = null)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var blocks = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cues = new List<Cue>();

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.TrimEntries);
            if (lines.Length < 2)
            {
                log?.Warn("Skipping malformed SRT block.");
                continue;
            }

            var timeLineIndex = int.TryParse(lines[0], out _) && lines.Length >= 3 ? 1 : 0;
            if (timeLineIndex >= lines.Length)
            {
                continue;
            }

            var timeMatch = TimeRegex.Match(lines[timeLineIndex]);
            if (!timeMatch.Success)
            {
                log?.Warn("Skipping SRT cue with invalid timing.");
                continue;
            }

            try
            {
                var start = ParseTimestamp(timeMatch, 1);
                var end = ParseTimestamp(timeMatch, 5);
                var textLines = lines.Skip(timeLineIndex + 1);
                var joined = string.Join(' ', textLines);
                joined = HtmlTagRegex.Replace(joined, string.Empty);
                joined = BraceTagRegex.Replace(joined, string.Empty);
                joined = Regex.Replace(joined, @"\s+", " ").Trim();
                if (joined.Length == 0)
                {
                    continue;
                }

                cues.Add(new Cue(start, end, joined));
            }
            catch
            {
                log?.Warn("Skipping SRT cue with invalid timing.");
            }
        }

        return cues;
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        foreach (var encoding in new[] { Encoding.UTF8, Encoding.Unicode, Encoding.GetEncoding(1258), Encoding.Latin1 })
        {
            try
            {
                var text = encoding.GetString(bytes);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            catch
            {
                // try next
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static TimeSpan ParseTimestamp(Match match, int startGroup)
    {
        var hours = int.Parse(match.Groups[startGroup].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups[startGroup + 1].Value, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups[startGroup + 2].Value, CultureInfo.InvariantCulture);
        var millisRaw = match.Groups[startGroup + 3].Value;
        var millis = millisRaw.Length switch
        {
            1 => int.Parse(millisRaw, CultureInfo.InvariantCulture) * 100,
            2 => int.Parse(millisRaw, CultureInfo.InvariantCulture) * 10,
            _ => int.Parse(millisRaw, CultureInfo.InvariantCulture)
        };
        return new TimeSpan(0, hours, minutes, seconds, millis);
    }
}
