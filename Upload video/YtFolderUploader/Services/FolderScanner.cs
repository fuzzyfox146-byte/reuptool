namespace YtFolderUploader;

public static class FolderScanner
{
    static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v", ".avi", ".wmv", ".flv", ".webm", ".mkv", ".mpeg", ".mpg", ".3gp"
    };

    static readonly HashSet<string> ThumbExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
    };

    public static ScanResult Scan(VideoSource source)
    {
        var result = new ScanResult();
        if (string.IsNullOrWhiteSpace(source.VideoFolder) || !Directory.Exists(source.VideoFolder))
        {
            result.Notes.Add($"{Display(source)}: không thấy thư mục video \"{source.VideoFolder}\".");
            return result;
        }

        if (string.IsNullOrWhiteSpace(source.ThumbFolder) || !Directory.Exists(source.ThumbFolder))
            result.Notes.Add($"{Display(source)}: không thấy thư mục thumb \"{source.ThumbFolder}\". Video vẫn được liệt kê.");

        var videos = ListMedia(source.VideoFolder, VideoExtensions);
        var thumbs = string.IsNullOrWhiteSpace(source.ThumbFolder) || !Directory.Exists(source.ThumbFolder)
            ? new List<MediaFile>()
            : ListMedia(source.ThumbFolder, ThumbExtensions);

        var thumbQueues = thumbs
            .Where(file => file.HasIndex)
            .GroupBy(file => file.Number)
            .ToDictionary(
                group => group.Key,
                group => new Queue<MediaFile>(group.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)));

        foreach (var group in videos.Where(file => file.HasIndex).GroupBy(file => file.Number).Where(group => group.Count() > 1))
        {
            var raw = group.First().RawIndex;
            result.Notes.Add($"{Display(source)}: số {raw} có {group.Count()} video, sẽ ghép lần lượt với ảnh cùng số.");
        }

        foreach (var video in videos
            .OrderBy(file => file.HasIndex ? 0 : 1)
            .ThenBy(file => file.Number)
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            MediaFile? thumb = null;
            if (video.HasIndex
                && thumbQueues.TryGetValue(video.Number, out var queue)
                && queue.Count > 0)
            {
                thumb = queue.Dequeue();
            }

            string title;
            var trimmed = false;
            if (thumb != null)
            {
                (title, trimmed) = FileNames.MakeTitle(thumb.Name, stripKhung: false);
            }
            else
            {
                (title, trimmed) = FileNames.MakeTitle(video.Name, stripKhung: true);
            }

            if (string.IsNullOrWhiteSpace(title))
                title = string.IsNullOrWhiteSpace(video.RawIndex) ? "Video" : $"Video {video.RawIndex}";

            var info = new FileInfo(video.FullPath);
            result.Videos.Add(new ScannedVideo
            {
                Index = string.IsNullOrEmpty(video.RawIndex) ? "—" : video.RawIndex,
                IndexNumber = video.HasIndex ? video.Number : int.MaxValue,
                VideoPath = video.FullPath,
                ThumbPath = thumb?.FullPath,
                Title = title,
                TitleTrimmed = trimmed,
                MissingThumb = thumb == null,
                FileSize = info.Exists ? info.Length : 0,
                FileModifiedUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.UnixEpoch
            });

            if (!video.HasIndex)
                result.Notes.Add($"{Display(source)}: \"{video.Name}\" không có số thứ tự ở đầu tên, nên không ghép được thumbnail.");
        }

        var missing = result.Videos.Count(video => video.MissingThumb);
        if (missing > 0)
            result.Notes.Add($"{Display(source)}: {missing} video không có ảnh thumb cùng số.");

        var unused = thumbQueues.Values.Sum(queue => queue.Count);
        if (unused > 0)
            result.Notes.Add($"{Display(source)}: {unused} ảnh thumb không có video cùng số.");

        if (result.Videos.Count == 0)
            result.Notes.Add($"{Display(source)}: không có file video trong \"{source.VideoFolder}\".");

        return result;
    }

    public static void ApplyRoot(VideoSource source, string root)
    {
        source.VideoFolder = FindChild(root, "xuat", "video", "videos", "output") ?? root;
        source.ThumbFolder = FindChild(root, "thumb", "thumbs", "thumbnail", "thumbnails") ?? root;
    }

    static string? FindChild(string root, params string[] names)
    {
        if (!Directory.Exists(root))
            return null;

        foreach (var name in names)
        {
            var candidate = Path.Combine(root, name);
            if (Directory.Exists(candidate))
                return candidate;
        }

        try
        {
            return Directory.GetDirectories(root)
                .FirstOrDefault(dir => names.Contains(Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    static List<MediaFile> ListMedia(string folder, HashSet<string> extensions)
    {
        var list = new List<MediaFile>();
        IEnumerable<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return list;
        }

        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith("~$") || name.StartsWith('.'))
                continue;
            if (!extensions.Contains(Path.GetExtension(name)))
                continue;

            var hasIndex = FileNames.TryGetIndex(name, out var number, out var raw);
            list.Add(new MediaFile(path, name, hasIndex, number, raw));
        }

        return list;
    }

    static string Display(VideoSource source) => string.IsNullOrWhiteSpace(source.Name) ? source.VideoFolder : source.Name;

    sealed record MediaFile(string FullPath, string Name, bool HasIndex, int Number, string RawIndex);
}
