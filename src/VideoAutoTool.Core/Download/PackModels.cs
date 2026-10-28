namespace VideoAutoTool.Core.Download;

/// <summary>What to fetch as the main media of each numbered item.</summary>
public enum PackMediaMode
{
    /// <summary>Video (mp4, H.264/AAC) into the video folder.</summary>
    Video,

    /// <summary>Sound only (AAC .m4a) into the audio folder.</summary>
    Audio,

    /// <summary>No main media; only thumbnails and/or subtitles.</summary>
    None
}

public sealed record PackChannelRequest(
    string RootFolder,
    string FolderName,
    string ChannelUrl,
    int PlaylistStart,
    int PlaylistEnd,
    int NameStart,
    string Quality,
    bool OrderByViews = false,
    bool DownloadSubtitles = false,
    string SubtitleLanguages = "",
    PackMediaMode Media = PackMediaMode.Video,
    bool DownloadThumbs = true);

public sealed record PackDownloadResult(string ChannelFolder, DownloadStop Stop, string Detail);

public sealed record PackItem(
    int PlaylistIndex,
    int Number,
    bool NeedVideo,
    bool NeedThumb,
    string? VideoId = null,
    bool NeedSub = false);

public static class PackFolders
{
    public const string VideoFolder = "video";
    public const string ThumbFolder = "thumb";
    public const string TempFolder = "temp";
    public const string SubFolder = "sub";
    public const string AudioFolder = "audio";

    public static string? InvalidName(string? folderName)
    {
        var name = (folderName ?? "").Trim();
        if (name.Length == 0)
        {
            return "Nhập tên folder, ví dụ HD036.";
        }

        if (name is "." or ".." || name.Contains("..", StringComparison.Ordinal))
        {
            return "Tên folder không hợp lệ.";
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "Tên folder có ký tự không dùng được.";
        }

        return null;
    }

    public static string Channel(PackChannelRequest request)
    {
        var root = (request.RootFolder ?? "").Trim();
        var name = (request.FolderName ?? "").Trim();
        if (root.Length == 0 || name.Length == 0)
        {
            return root.Length == 0 ? name : Path.Combine(root, name);
        }

        return Path.GetFullPath(Path.Combine(root, name));
    }

    public static string Video(string channelFolder) => Path.Combine(channelFolder, VideoFolder);

    public static string Audio(string channelFolder) => Path.Combine(channelFolder, AudioFolder);

    /// <summary>Folder that holds the main media: sound-only files for the audio edition, video otherwise.</summary>
    public static string Media(string channelFolder, PackChannelRequest request) =>
        request.Media == PackMediaMode.Audio ? Audio(channelFolder) : Video(channelFolder);

    public static Func<string, bool> MediaFilter(PackChannelRequest request) =>
        request.Media == PackMediaMode.Audio ? DownloadResume.IsAudioFile : DownloadResume.IsVideoFile;

    public static string Thumb(string channelFolder) => Path.Combine(channelFolder, ThumbFolder);

    public static string Temp(string channelFolder) => Path.Combine(channelFolder, TempFolder);

    public static string Sub(string channelFolder) => Path.Combine(channelFolder, SubFolder);

    public static IReadOnlyList<PackItem> Pending(string videoDir, string thumbDir, PackChannelRequest request, string? subDir = null)
    {
        var items = new List<PackItem>();
        for (var index = request.PlaylistStart; index <= request.PlaylistEnd; index++)
        {
            var number = request.NameStart + (index - request.PlaylistStart);
            var needVideo = request.Media != PackMediaMode.None && !DownloadResume.HasCompleted(videoDir, number, MediaFilter(request));
            var needThumb = request.DownloadThumbs && !DownloadResume.HasCompleted(thumbDir, number, DownloadResume.IsThumbnailFile);
            var needSub = NeedsSub(subDir, request, number);
            if (needVideo || needThumb || needSub)
            {
                items.Add(new PackItem(index, number, needVideo, needThumb, NeedSub: needSub));
            }
        }

        return items;
    }

    public static IReadOnlyList<PackItem> PendingRanked(
        string videoDir,
        string thumbDir,
        PackChannelRequest request,
        IReadOnlyList<PackViewEntry> ranked,
        string? subDir = null)
    {
        var items = new List<PackItem>();
        var maxRank = request.PlaylistEnd <= 0 ? ranked.Count : Math.Min(request.PlaylistEnd, ranked.Count);
        for (var rank = request.PlaylistStart; rank <= maxRank; rank++)
        {
            var entry = ranked[rank - 1];
            var number = request.NameStart + (rank - request.PlaylistStart);
            var needVideo = request.Media != PackMediaMode.None && !DownloadResume.HasCompleted(videoDir, number, MediaFilter(request));
            var needThumb = request.DownloadThumbs && !DownloadResume.HasCompleted(thumbDir, number, DownloadResume.IsThumbnailFile);
            var needSub = NeedsSub(subDir, request, number);
            if (needVideo || needThumb || needSub)
            {
                items.Add(new PackItem(entry.PlaylistIndex, number, needVideo, needThumb, entry.Id, needSub));
            }
        }

        return items;
    }

    static bool NeedsSub(string? subDir, PackChannelRequest request, int number) =>
        request.DownloadSubtitles
        && !string.IsNullOrWhiteSpace(subDir)
        && PackSubtitleLanguages.Missing(subDir, number, request.SubtitleLanguages).Count > 0;
}
