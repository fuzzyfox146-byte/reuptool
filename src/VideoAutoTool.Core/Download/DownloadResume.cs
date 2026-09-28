namespace VideoAutoTool.Core.Download;

public sealed record DownloadSlice(int PlaylistStart, int PlaylistEnd, int NameStart);

/// <summary>
/// Videos continue after the highest finished number. Subtitles and thumbnails
/// follow an existing same-number video (title match); holes without a video are
/// skipped. After the last video they fill through the entered end. No archive.
/// </summary>
public static class DownloadResume
{
    public static IReadOnlyList<DownloadSlice> MissingVideos(string sourceFolder, ChannelDownloadRequest request) =>
        AfterHighest(sourceFolder, request, IsVideoFile);

    public static IReadOnlyList<DownloadSlice> MissingSubtitles(
        string textFolder,
        string sourceFolder,
        ChannelDownloadRequest request) =>
        MissingPaired(textFolder, sourceFolder, request, IsSubtitleFile);

    public static IReadOnlyList<DownloadSlice> MissingThumbnails(
        string thumbFolder,
        string sourceFolder,
        ChannelDownloadRequest request) =>
        MissingPaired(thumbFolder, sourceFolder, request, IsThumbnailFile);

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

    public static bool IsThumbnailFile(string fileName)
    {
        if (fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ext = Path.GetExtension(fileName);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<DownloadSlice> AfterHighest(
        string folder,
        ChannelDownloadRequest request,
        Func<string, bool> isCompletedFile)
    {
        var have = ExistingNumbers(folder, isCompletedFile);
        var highest = HighestInRange(have, request);
        var endNumber = request.NameStart + (request.PlaylistEnd - request.PlaylistStart);
        var nextNumber = highest + 1;
        if (nextNumber > endNumber)
        {
            return [];
        }

        var playlistStart = request.PlaylistStart + (nextNumber - request.NameStart);
        return [new DownloadSlice(playlistStart, request.PlaylistEnd, nextNumber)];
    }

    public static string? FileForNumber(string folder, int number, Func<string, bool> isCompletedFile)
    {
        var files = FilesByNumber(folder, isCompletedFile);
        return files.TryGetValue(number, out var name) ? Path.Combine(folder, name) : null;
    }

    public static IReadOnlyList<DownloadSlice> MissingPaired(
        string sideFolder,
        string sourceFolder,
        ChannelDownloadRequest request,
        Func<string, bool> isSideFile)
    {
        var videos = FilesByNumber(sourceFolder, IsVideoFile);
        var sides = FilesByNumber(sideFolder, isSideFile);
        var highest = HighestInRange(videos.Keys, request);
        var missing = new List<int>();
        for (var index = request.PlaylistStart; index <= request.PlaylistEnd; index++)
        {
            var number = request.NameStart + (index - request.PlaylistStart);
            if (IsAcceptablePair(number, highest, videos, sides))
            {
                continue;
            }

            missing.Add(index);
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

    public static int RemoveMismatched(
        string sideFolder,
        string sourceFolder,
        ChannelDownloadRequest request,
        Func<string, bool> isSideFile)
    {
        if (!Directory.Exists(sideFolder))
        {
            return 0;
        }

        var videos = FilesByNumber(sourceFolder, IsVideoFile);
        var removed = 0;
        foreach (var path in Directory.EnumerateFiles(sideFolder).ToList())
        {
            var name = Path.GetFileName(path);
            if (!isSideFile(name))
            {
                continue;
            }

            var end = 0;
            while (end < name.Length && char.IsDigit(name[end]))
            {
                end++;
            }

            if (end == 0 || !int.TryParse(name[..end], out var number))
            {
                continue;
            }

            var endNumber = request.NameStart + (request.PlaylistEnd - request.PlaylistStart);
            if (number < request.NameStart || number > endNumber)
            {
                continue;
            }

            if (!videos.TryGetValue(number, out var videoName))
            {
                continue;
            }

            if (DownloadTitleKey.Aligns(name, videoName))
            {
                continue;
            }

            File.Delete(path);
            removed++;
        }

        return removed;
    }

    public static bool HasMatchingPair(
        string sideFolder,
        string sourceFolder,
        int number,
        Func<string, bool> isSideFile)
    {
        var side = FileForNumber(sideFolder, number, isSideFile);
        if (side is null)
        {
            return false;
        }

        var video = FileForNumber(sourceFolder, number, IsVideoFile);
        if (video is null)
        {
            return true;
        }

        return DownloadTitleKey.Aligns(Path.GetFileName(side), Path.GetFileName(video));
    }

    private static bool IsAcceptablePair(
        int number,
        int highest,
        Dictionary<int, string> videos,
        Dictionary<int, string> sides)
    {
        if (!videos.TryGetValue(number, out var videoName))
        {
            return number <= highest || sides.ContainsKey(number);
        }

        if (!sides.TryGetValue(number, out var sideName))
        {
            return false;
        }

        return DownloadTitleKey.Aligns(sideName, videoName);
    }

    private static int HighestInRange(IEnumerable<int> numbers, ChannelDownloadRequest request)
    {
        var endNumber = request.NameStart + (request.PlaylistEnd - request.PlaylistStart);
        var highest = request.NameStart - 1;
        foreach (var number in numbers)
        {
            if (number >= request.NameStart && number <= endNumber && number > highest)
            {
                highest = number;
            }
        }

        return highest;
    }

    private static Dictionary<int, string> FilesByNumber(string folder, Func<string, bool> isCompletedFile)
    {
        var files = new Dictionary<int, string>();
        if (!Directory.Exists(folder))
        {
            return files;
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

            if (end > 0 && int.TryParse(name[..end], out var number) && !files.ContainsKey(number))
            {
                files[number] = name;
            }
        }

        return files;
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
