namespace VideoAutoTool.Core.Download;

public sealed record PlaylistEntry(string Id, int Index, string Title);

public static class PlaylistCatalog
{
    public static string WatchUrl(string id) => "https://www.youtube.com/watch?v=" + id;

    public static IReadOnlyList<PlaylistEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<PlaylistEntry>();
        foreach (var raw in lines.ToArray())
        {
            if (string.IsNullOrWhiteSpace(raw) || !raw.Contains('\t'))
            {
                continue;
            }

            var parts = raw.Trim().Split('\t');
            if (parts.Length < 3)
            {
                continue;
            }

            var id = parts[0].Trim();
            if (id.Length < 8 || !int.TryParse(parts[1].Trim(), out var index) || index < 1)
            {
                continue;
            }

            entries.Add(new PlaylistEntry(id, index, parts[2].Trim()));
        }

        return entries;
    }

    public static PlaylistEntry? Find(string fileName, int number, IReadOnlyList<PlaylistEntry> entries)
    {
        PlaylistEntry? first = null;
        PlaylistEntry? indexed = null;
        var matches = 0;
        foreach (var entry in entries)
        {
            if (!DownloadTitleKey.Aligns(fileName, entry.Title))
            {
                continue;
            }

            matches++;
            first ??= entry;
            if (entry.Index == number)
            {
                indexed = entry;
            }
        }

        if (matches == 1)
        {
            return first;
        }

        return indexed ?? first;
    }
}
