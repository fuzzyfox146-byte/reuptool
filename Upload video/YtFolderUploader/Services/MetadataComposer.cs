namespace YtFolderUploader;

public static class MetadataComposer
{
    public const int DescriptionLimit = 5000;
    public const int TagLimit = 500;

    public static string Description(string? template, string title, string index, string source, string fileName)
    {
        var text = template ?? "";
        text = text.Replace("{title}", title, StringComparison.Ordinal)
            .Replace("{index}", index, StringComparison.Ordinal)
            .Replace("{source}", source, StringComparison.Ordinal)
            .Replace("{filename}", fileName, StringComparison.Ordinal);

        if (text.Length <= DescriptionLimit)
            return text;

        return text[..DescriptionLimit];
    }

    public static IReadOnlyList<string> Tags(string? raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var used = 0;
        foreach (var part in raw.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var tag = part.Trim();
            if (tag.Length == 0)
                continue;
            if (tag.Length > 100)
                tag = tag[..100];

            var cost = tag.Contains(' ') ? tag.Length + 2 : tag.Length;
            var comma = result.Count > 0 ? 1 : 0;
            if (used + comma + cost > TagLimit)
                break;

            result.Add(tag);
            used += comma + cost;
        }

        return result;
    }

    public static string? NormalizePlaylistId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var text = input.Trim();
        var list = System.Text.RegularExpressions.Regex.Match(text, @"[?&]list=([A-Za-z0-9_-]+)");
        if (list.Success)
            return list.Groups[1].Value;

        var path = System.Text.RegularExpressions.Regex.Match(text, @"playlist/([A-Za-z0-9_-]+)");
        if (path.Success)
            return path.Groups[1].Value;

        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z0-9_-]{13,}$"))
            return text;

        return null;
    }
}
