namespace VideoAutoTool.Core.Download;

public static class YtDlpPlan
{
    public const string OutputTemplate = "%(autonumber)03d %(title).100s.%(ext)s";

    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    public static IReadOnlyList<string> Probe(DownloadTools tools, ChannelDownloadRequest request, int playlistItem)
    {
        var playlist = ChannelPlaylist.ToVideosUrl(request.ChannelUrl);
        return
        [
            playlist,
            "--playlist-items", playlistItem.ToString(),
            "--cookies", tools.CookiesPath,
            "--skip-download",
            "--print", "%(id)s",
            "--user-agent", UserAgent,
            "--no-warnings"
        ];
    }

    public static IReadOnlyList<string> Videos(DownloadTools tools, ChannelDownloadRequest request) =>
        Videos(tools, request, new DownloadSlice(request.PlaylistStart, request.PlaylistEnd, request.NameStart));

    public static IReadOnlyList<string> Videos(DownloadTools tools, ChannelDownloadRequest request, DownloadSlice slice)
    {
        var folder = request.ParentFolder;
        return Common(tools, request, slice).Concat(
        [
            "--no-overwrites",
            "--ffmpeg-location", tools.FfmpegLocation,
            "--newline",
            "--no-mtime",
            "--format", VideoQuality.Format(request.Quality),
            "--merge-output-format", "mp4",
            "--write-thumbnail",
            "--convert-thumbnails", "jpg",
            "--no-write-playlist-metafiles",
            "--fragment-retries", "10",
            "--concurrent-fragments", "5",
            "--geo-bypass",
            "--match-filter", "!is_live",
            "-P", "home:" + Path.Combine(folder, "source"),
            "-P", "thumbnail:" + Path.Combine(folder, "thum"),
            "-P", "temp:" + Path.Combine(folder, "temp")
        ]).ToList();
    }

    public static IReadOnlyList<string> Catalog(DownloadTools tools, ChannelDownloadRequest request)
    {
        return
        [
            ChannelPlaylist.ToVideosUrl(request.ChannelUrl),
            "--flat-playlist",
            "--skip-download",
            "--print", "%(id)s\t%(playlist_index)s\t%(title)s",
            "--cookies", tools.CookiesPath,
            "--user-agent", UserAgent,
            "--no-warnings"
        ];
    }

    public static IReadOnlyList<string> Subtitles(DownloadTools tools, ChannelDownloadRequest request) =>
        Subtitles(tools, request, new DownloadSlice(request.PlaylistStart, request.PlaylistEnd, request.NameStart));

    public static IReadOnlyList<string> Subtitles(DownloadTools tools, ChannelDownloadRequest request, DownloadSlice slice) =>
        Subtitles(tools, request, slice, videoUrl: null);

    public static IReadOnlyList<string> Subtitles(
        DownloadTools tools,
        ChannelDownloadRequest request,
        DownloadSlice slice,
        string? videoUrl)
    {
        var textDir = Path.Combine(request.ParentFolder, "text");
        return Target(tools, request, slice, videoUrl).Concat(
        [
            "--no-overwrites",
            "--no-mark-watched",
            "--write-subs",
            "--write-auto-subs",
            "--sub-langs", request.Language,
            "--sub-format", "json3",
            "--skip-download",
            "--newline",
            "--sleep-interval", "2",
            "--max-sleep-interval", "5",
            "--retry-sleep", "3",
            "-P", "home:" + textDir,
            "-P", "subtitle:" + textDir,
            "-P", "temp:" + Path.Combine(request.ParentFolder, "temp")
        ]).ToList();
    }

    public static IReadOnlyList<string> Thumbnails(DownloadTools tools, ChannelDownloadRequest request, DownloadSlice slice) =>
        Thumbnails(tools, request, slice, videoUrl: null);

    public static IReadOnlyList<string> Thumbnails(
        DownloadTools tools,
        ChannelDownloadRequest request,
        DownloadSlice slice,
        string? videoUrl)
    {
        var thumDir = Path.Combine(request.ParentFolder, "thum");
        return Target(tools, request, slice, videoUrl).Concat(
        [
            "--no-overwrites",
            "--skip-download",
            "--write-thumbnail",
            "--convert-thumbnails", "jpg",
            "--no-write-playlist-metafiles",
            "--ffmpeg-location", tools.FfmpegLocation,
            "--newline",
            "-P", "home:" + thumDir,
            "-P", "thumbnail:" + thumDir,
            "-P", "temp:" + Path.Combine(request.ParentFolder, "temp")
        ]).ToList();
    }

    private static IReadOnlyList<string> Target(
        DownloadTools tools,
        ChannelDownloadRequest request,
        DownloadSlice slice,
        string? videoUrl)
    {
        if (!string.IsNullOrWhiteSpace(videoUrl))
        {
            return
            [
                videoUrl,
                "--autonumber-start", slice.NameStart.ToString(),
                "--cookies", tools.CookiesPath,
                "--user-agent", UserAgent,
                "--windows-filenames",
                "--retries", "10",
                "-o", $"{slice.NameStart:000} %(title).100s.%(ext)s"
            ];
        }

        return Common(tools, request, slice);
    }

    private static IReadOnlyList<string> Common(DownloadTools tools, ChannelDownloadRequest request, DownloadSlice slice) =>
    [
        ChannelPlaylist.ToVideosUrl(request.ChannelUrl),
        "--playlist-items", PlaylistItems(slice),
        "--autonumber-start", slice.NameStart.ToString(),
        "--cookies", tools.CookiesPath,
        "--user-agent", UserAgent,
        "--windows-filenames",
        "--retries", "10",
        "-o", OutputTemplate
    ];

    private static string PlaylistItems(DownloadSlice slice) =>
        slice.PlaylistStart == slice.PlaylistEnd
            ? slice.PlaylistStart.ToString()
            : slice.PlaylistStart + "-" + slice.PlaylistEnd;
}
