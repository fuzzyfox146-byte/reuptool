using System.Text.RegularExpressions;

namespace HdDownloader.Services;

public sealed record ParsedChannel(string FolderName, string ChannelUrl);

public static class ChannelListParser
{
    private static readonly Regex UrlRegex = new(
        @"(?:https?://[^\s\t,;]+|(?:www\.)?youtube\.com/[^\s\t,;]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<ParsedChannel> Parse(string? text, string? currentLastFolder = null)
    {
        var results = new List<ParsedChannel>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return results;
        }

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var lastFolder = currentLastFolder ?? "HD035";

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = UrlRegex.Match(line);
            if (!match.Success)
            {
                // Line does not contain a link (e.g. header, section title, empty cell)
                continue;
            }

            var url = match.Value.Trim();
            if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            else if (url.StartsWith("youtube.com", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://www." + url;
            }

            // Extract folder name from the portion before the URL
            var before = line[..match.Index].Trim();
            var folder = "";

            if (!string.IsNullOrWhiteSpace(before))
            {
                // Spreadsheet rows separate columns with tab \t
                var parts = before.Split(['\t', ';', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 0)
                {
                    // Choose the column token before the URL (e.g. HD036, HD037 or custom name)
                    folder = parts[^1];
                }
            }

            if (!string.IsNullOrWhiteSpace(folder))
            {
                folder = SanitizeFolderName(folder);
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = NextFolderName(lastFolder);
            }

            lastFolder = folder;
            results.Add(new ParsedChannel(folder, url));
        }

        return results;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }

    public static string NextFolderName(string current)
    {
        var name = current.Trim();
        var index = name.Length;
        while (index > 0 && char.IsDigit(name[index - 1]))
        {
            index--;
        }

        if (index == name.Length || !int.TryParse(name[index..], out var number))
        {
            return name + "2";
        }

        var width = name.Length - index;
        return name[..index] + (number + 1).ToString().PadLeft(width, '0');
    }
}
