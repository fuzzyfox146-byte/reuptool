namespace YtFolderUploader;

public static class ChannelQueueOrder
{
    public static List<T> InListOrder<T>(
        IEnumerable<T> jobs,
        IReadOnlyList<string> channelIdsTopToBottom,
        Func<T, string> channelId,
        Func<T, DateTimeOffset?> publishAt,
        Func<T, string> index)
    {
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < channelIdsTopToBottom.Count; i++)
            rank.TryAdd(channelIdsTopToBottom[i], i);

        return jobs
            .OrderBy(job => rank.TryGetValue(channelId(job), out var place) ? place : int.MaxValue)
            .ThenBy(job => publishAt(job) ?? DateTimeOffset.MaxValue)
            .ThenBy(job => index(job), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
