namespace VideoAutoTool.Core.Download;

public sealed record DownloadSlice(int PlaylistStart, int PlaylistEnd, int NameStart);

/// <summary>
/// Videos continue after the highest finished number. Subtitles fill every missing number
/// up to the entered end. No archive file is read or written.
/// </summary>
public static class DownloadResume
{
    public static IReadOnlyList<DownloadSlice> MissingVideos(string sourceFolder, ChannelDownloadRequest request) =>
        AfterHighest(sourceFolder, request, IsVideoFile);

    public static IReadOnlyList<DownloadSlice> MissingSubtitles(string textFolder, ChannelDownloadRequest request) =>
        MissingNumbers(textFolder, request, IsSubtitleFile);

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

    public static bool IsSubtitleFile(string fileName)
    {
        if (fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ext = Path.GetExtension(fileName);
        return ext.Equals(".srt", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".json3", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<DownloadSlice> AfterHighest(
        string folder,
        ChannelDownloadRequest request,
        Func<string, bool> isCompletedFile)
    {
        var have = ExistingNumbers(folder, isCompletedFile);
        var endNumber = request.NameStart + (request.PlaylistEnd - request.PlaylistStart);
        var highest = request.NameStart - 1;
        foreach (var number in have)
        {
            if (number >= request.NameStart && number <= endNumber && number > highest)
            {
                highest = number;
            }
        }

        var nextNumber = highest + 1;
        if (nextNumber > endNumber)
        {
            return [];
        }

        var playlistStart = request.PlaylistStart + (nextNumber - request.NameStart);
        return [new DownloadSlice(playlistStart, request.PlaylistEnd, nextNumber)];
    }

    public static IReadOnlyList<DownloadSlice> MissingNumbers(
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

    public static bool HasCompleted(string folder, int number, Func<string, bool> isCompletedFile) =>
        ExistingNumbers(folder, isCompletedFile).Contains(number);

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
