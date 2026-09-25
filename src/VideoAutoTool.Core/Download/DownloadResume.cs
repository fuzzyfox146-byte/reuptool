namespace VideoAutoTool.Core.Download;

public sealed record DownloadSlice(int PlaylistStart, int PlaylistEnd, int NameStart);

/// <summary>
/// Skips numbers that already have a finished file so a later run keeps the same 001, 002 names.
/// </summary>
public static class DownloadResume
{
    public static IReadOnlyList<DownloadSlice> MissingVideos(string sourceFolder, ChannelDownloadRequest request) =>
        Missing(sourceFolder, request, IsVideoFile);

    public static IReadOnlyList<DownloadSlice> MissingSubtitles(string textFolder, ChannelDownloadRequest request) =>
        Missing(textFolder, request, IsSubtitleFile);

    public static bool IsVideoFile(string fileName)
    {
        if (fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ext = Path.GetExtension(fileName);
        return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webm", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".m4v", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSubtitleFile(string fileName) =>
        !fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
        && Path.GetExtension(fileName).Equals(".srt", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<DownloadSlice> Missing(
        string folder,
        ChannelDownloadRequest request,
        Func<string, bool> isCompletedFile)
    {
        var have = ExistingNumbers(folder, isCompletedFile);
        var missing = new List<int>();
        for (var index = request.PlaylistStart; index <= request.PlaylistEnd; index++)
        {
            var number = request.NameStart + (index - request.PlaylistStart);
            if (!have.Contains(number))
            {
                missing.Add(index);
            }
        }

        var slices = new List<DownloadSlice>();
        for (var i = 0; i < missing.Count;)
        {
            var start = missing[i];
            var end = start;
            i++;
            while (i < missing.Count && missing[i] == end + 1)
            {
                end = missing[i];
                i++;
            }

            slices.Add(new DownloadSlice(start, end, request.NameStart + (start - request.PlaylistStart)));
        }

        return slices;
    }

    /// <summary>
    /// One playlist item per slice so yt-dlp autonumber cannot skip ahead
    /// when a middle video fails (cookie, private, unavailable).
    /// </summary>
    public static IReadOnlyList<DownloadSlice> OneItemEach(IEnumerable<DownloadSlice> slices)
    {
        var items = new List<DownloadSlice>();
        foreach (var slice in slices)
        {
            for (var index = slice.PlaylistStart; index <= slice.PlaylistEnd; index++)
            {
                items.Add(new DownloadSlice(index, index, slice.NameStart + (index - slice.PlaylistStart)));
            }
        }

        return items;
    }

    private static HashSet<int> ExistingNumbers(string folder, Func<string, bool> isCompletedFile)
    {
        var numbers = new HashSet<int>();
        if (!Directory.Exists(folder))
        {
            return numbers;
        }

        foreach (var path in Directory.EnumerateFiles(folder))
        {
            var name = Path.GetFileName(path);
            if (!isCompletedFile(name))
            {
                continue;
            }

            var end = 0;
            while (end < name.Length && char.IsDigit(name[end]))
            {
                end++;
            }

            if (end > 0 && int.TryParse(name[..end], out var number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }
}
