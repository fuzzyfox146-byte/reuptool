using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VideoAutoTool.Core.Download;

public static partial class Json3ToSrt
{
    public static int ConvertFolder(string subtitleFolder, IProgress<string>? log)
    {
        if (!Directory.Exists(subtitleFolder))
        {
            return 0;
        }

        var converted = 0;
        foreach (var jsonPath in Directory.EnumerateFiles(subtitleFolder, "*.json3"))
        {
            try
            {
                var srtPath = ConvertFile(jsonPath);
                converted++;
                log?.Report("Đã chuyển SRT: " + Path.GetFileName(srtPath));
            }
            catch (Exception ex)
            {
                log?.Report("Lỗi chuyển JSON3: " + Path.GetFileName(jsonPath) + " — " + ex.Message);
            }
        }

        return converted;
    }

    public static string ConvertFile(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var srt = Convert(json);
        var srtPath = Path.ChangeExtension(jsonPath, ".srt");
        File.WriteAllText(srtPath, srt, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.Delete(jsonPath);
        return srtPath;
    }

    public static string Convert(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var cues = new List<Cue>();
        var previousRaw = "";
        foreach (var evt in events.EnumerateArray())
        {
            if (!evt.TryGetProperty("segs", out var segs) || segs.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var rawBuilder = new StringBuilder();
            foreach (var seg in segs.EnumerateArray())
            {
                if (seg.TryGetProperty("utf8", out var utf8) && utf8.ValueKind == JsonValueKind.String)
                {
                    rawBuilder.Append(utf8.GetString());
                }
            }

            var raw = Clean(rawBuilder.ToString());
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var text = RemoveOverlap(previousRaw, raw);
            previousRaw = raw;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var start = evt.TryGetProperty("tStartMs", out var startEl) ? startEl.GetInt64() : 0;
            var duration = evt.TryGetProperty("dDurationMs", out var durEl) && durEl.ValueKind == JsonValueKind.Number
                ? durEl.GetInt64()
                : 0;
            if (duration < 200)
            {
                duration = 1500;
            }

            cues.Add(new Cue(start, start + duration, text));
        }

        for (var i = 0; i < cues.Count - 1; i++)
        {
            var nextStart = cues[i + 1].Start;
            if (cues[i].End > nextStart)
            {
                cues[i] = cues[i] with { End = Math.Max(cues[i].Start + 100, nextStart - 10) };
            }
        }

        var output = new StringBuilder();
        var index = 1;
        foreach (var cue in cues)
        {
            if (string.IsNullOrWhiteSpace(cue.Text))
            {
                continue;
            }

            if (output.Length > 0)
            {
                output.AppendLine();
                output.AppendLine();
            }

            output.Append(index);
            output.AppendLine();
            output.Append(FormatTime(cue.Start));
            output.Append(" --> ");
            output.Append(FormatTime(cue.End));
            output.AppendLine();
            output.Append(cue.Text);
            index++;
        }

        if (output.Length > 0)
        {
            output.AppendLine();
        }

        return output.ToString();
    }

    private static string RemoveOverlap(string previousText, string currentText)
    {
        previousText = Clean(previousText);
        currentText = Clean(currentText);
        if (string.IsNullOrWhiteSpace(currentText))
        {
            return "";
        }

        if (string.IsNullOrWhiteSpace(previousText))
        {
            return currentText;
        }

        var previousWords = previousText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentWords = currentText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var previousNorm = previousWords.Select(NormalizeWord).Where(w => w.Length > 0).ToArray();
        var currentNorm = currentWords.Select(NormalizeWord).Where(w => w.Length > 0).ToArray();
        var maximum = Math.Min(previousNorm.Length, currentNorm.Length);
        var overlap = 0;
        for (var length = maximum; length >= 1; length--)
        {
            var matched = true;
            for (var i = 0; i < length; i++)
            {
                if (previousNorm[previousNorm.Length - length + i] != currentNorm[i])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                overlap = length;
                break;
            }
        }

        if (overlap >= currentWords.Length)
        {
            return "";
        }

        if (overlap > 0)
        {
            return Clean(string.Join(' ', currentWords.Skip(overlap)));
        }

        return currentText;
    }

    private static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return Whitespace().Replace(text, " ").Trim();
    }

    private static string NormalizeWord(string word)
    {
        var builder = new StringBuilder(word.Length);
        foreach (var ch in word)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static string FormatTime(long milliseconds)
    {
        if (milliseconds < 0)
        {
            milliseconds = 0;
        }

        var hours = milliseconds / 3_600_000;
        milliseconds %= 3_600_000;
        var minutes = milliseconds / 60_000;
        milliseconds %= 60_000;
        var seconds = milliseconds / 1000;
        var millis = milliseconds % 1000;
        return $"{hours:00}:{minutes:00}:{seconds:00},{millis:000}";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private sealed record Cue(long Start, long End, string Text);
}
