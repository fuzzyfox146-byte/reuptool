namespace VideoAutoTool.Core.Download;

public sealed record PackViewEntry(string Id, long Views, int PlaylistIndex);

public static class PackViewRank
{
    public static bool IsVideoId(string? id) =>
        id is { Length: 11 } && id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    public static IReadOnlyList<PackViewEntry> Parse(IEnumerable<string> lines)
    {
        var list = new List<PackViewEntry>();
        foreach (var raw in lines)
        {
            var parts = raw.Split('\t');
            if (parts.Length < 3 || !IsVideoId(parts[0].Trim()))
            {
                continue;
            }

            var views = 0L;
            if (long.TryParse(parts[1].Trim(), out var parsed) && parsed > 0)
            {
                views = parsed;
            }

            var index = int.TryParse(parts[2].Trim(), out var playlist) && playlist > 0
                ? playlist
                : int.MaxValue;
            list.Add(new PackViewEntry(parts[0].Trim(), views, index));
        }

        return list;
    }

    public static IReadOnlyList<PackViewEntry> Order(IEnumerable<PackViewEntry> entries) =>
        entries
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(entry => entry.Views).ThenBy(entry => entry.PlaylistIndex).First())
            .OrderByDescending(entry => entry.Views)
            .ThenBy(entry => entry.PlaylistIndex)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();
}
