namespace VideoAutoTool.Core.Download;

public static class PackPlan
{
    public static string OutputName(int number) => $"{number:000} %(title).100s.%(ext)s";

    public static IReadOnlyList<string> Probe(DownloadTools tools, PackChannelRequest request, int playlistItem) =>
    [
        ChannelPlaylist.ToVideosUrl(request.ChannelUrl),
        "--playlist-items", playlistItem.ToString(),
        "--cookies", tools.CookiesPath,
        "--skip-download",
        "--print", "%(id)s",
        "--user-agent", YtDlpPlan.UserAgent,
        "--no-warnings"
    ];

    public static IReadOnlyList<string> ProbeVideo(DownloadTools tools, string videoId) =>
    [
        WatchUrl(videoId),
        "--no-playlist",
        "--cookies", tools.CookiesPath,
        "--skip-download",
        "--print", "%(id)s",
        "--user-agent", YtDlpPlan.UserAgent,
        "--no-warnings"
    ];

    public static IReadOnlyList<string> ViewCatalog(DownloadTools tools, PackChannelRequest request) =>
    [
        ChannelPlaylist.ToVideosUrl(request.ChannelUrl),
        "--flat-playlist",
        "--skip-download",
        "--print", "%(id)s\t%(view_count)s\t%(playlist_index)s",
        "--cookies", tools.CookiesPath,
        "--user-agent", YtDlpPlan.UserAgent,
        "--no-warnings"
    ];

    public static string WatchUrl(string videoId) => "https://www.youtube.com/watch?v=" + videoId;

    public static IReadOnlyList<string> Video(
        DownloadTools tools,
        PackChannelRequest request,
        int playlistIndex,
        int number)
    {
        var folder = PackFolders.Channel(request);
        return Target(tools, request, playlistIndex, number, videoId: null).Concat(
        [
            "--no-overwrites",
            "--no-write-subs",
            "--no-write-auto-subs",
            "--no-write-thumbnail",
            "--ffmpeg-location", tools.FfmpegLocation,
            "--newline",
            "--no-mtime",
            "--format", VideoQuality.Format(request.Quality),
            "--merge-output-format", "mp4",
            "--no-write-playlist-metafiles",
            "--fragment-retries", "10",
            "--concurrent-fragments", "5",
            "--geo-bypass",
            "--match-filter", "!is_live",
            "-P", "home:" + PackFolders.Video(folder),
            "-P", "temp:" + PackFolders.Temp(folder)
        ]).ToList();
    }

    public static IReadOnlyList<string> Thumb(
        DownloadTools tools,
        PackChannelRequest request,
        int playlistIndex,
        int number)
    {
        var folder = PackFolders.Channel(request);
        var thumb = PackFolders.Thumb(folder);
        return Target(tools, request, playlistIndex, number, videoId: null).Concat(
        [
            "--no-overwrites",
            "--no-write-subs",
            "--no-write-auto-subs",
            "--skip-download",
            "--write-thumbnail",
            "--convert-thumbnails", "jpg",
            "--no-write-playlist-metafiles",
            "--ffmpeg-location", tools.FfmpegLocation,
            "--newline",
            "-P", "home:" + thumb,
            "-P", "thumbnail:" + thumb,
            "-P", "temp:" + PackFolders.Temp(folder)
        ]).ToList();
    }

    public static IReadOnlyList<string> VideoById(
        DownloadTools tools,
        PackChannelRequest request,
        string videoId,
        int number)
    {
        var folder = PackFolders.Channel(request);
        return Target(tools, request, playlistIndex: 0, number, videoId).Concat(
        [
            "--no-overwrites",
            "--no-write-subs",
            "--no-write-auto-subs",
            "--no-write-thumbnail",
            "--ffmpeg-location", tools.FfmpegLocation,
            "--newline",
            "--no-mtime",
            "--format", VideoQuality.Format(request.Quality),
            "--merge-output-format", "mp4",
            "--no-write-playlist-metafiles",
            "--fragment-retries", "10",
            "--concurrent-fragments", "5",
            "--geo-bypass",
            "--match-filter", "!is_live",
            "-P", "home:" + PackFolders.Video(folder),
            "-P", "temp:" + PackFolders.Temp(folder)
        ]).ToList();
    }

    public static IReadOnlyList<string> ThumbById(
        DownloadTools tools,
        PackChannelRequest request,
        string videoId,
        int number)
    {
        var folder = PackFolders.Channel(request);
        var thumb = PackFolders.Thumb(folder);
        return Target(tools, request, playlistIndex: 0, number, videoId).Concat(
        [
            "--no-overwrites",
            "--no-write-subs",
            "--no-write-auto-subs",
            "--skip-download",
            "--write-thumbnail",
            "--convert-thumbnails", "jpg",
            "--no-write-playlist-metafiles",
            "--ffmpeg-location", tools.FfmpegLocation,
            "--newline",
            "-P", "home:" + thumb,
            "-P", "thumbnail:" + thumb,
            "-P", "temp:" + PackFolders.Temp(folder)
        ]).ToList();
    }

    private static IReadOnlyList<string> Target(
        DownloadTools tools,
        PackChannelRequest request,
        int playlistIndex,
        int number,
        string? videoId)
    {
        if (!string.IsNullOrWhiteSpace(videoId))
        {
            return
            [
                WatchUrl(videoId),
                "--no-playlist",
                "--cookies", tools.CookiesPath,
                "--user-agent", YtDlpPlan.UserAgent,
                "--windows-filenames",
                "--retries", "10",
                "-o", OutputName(number)
            ];
        }

        return
        [
            ChannelPlaylist.ToVideosUrl(request.ChannelUrl),
            "--playlist-items", playlistIndex.ToString(),
            "--cookies", tools.CookiesPath,
            "--user-agent", YtDlpPlan.UserAgent,
            "--windows-filenames",
            "--retries", "10",
            "-o", OutputName(number)
        ];
    }
}
