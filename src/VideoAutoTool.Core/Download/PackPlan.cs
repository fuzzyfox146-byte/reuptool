namespace VideoAutoTool.Core.Download;

public static class PackPlan
{
    public static string OutputName(int number) => $"{number:000} %(title).100s.%(ext)s";

    public static IReadOnlyList<string> Probe(DownloadTools tools, PackChannelRequest request, int playlistItem) =>
    [
        ChannelPlaylist.ToVideosUrl(request.ChannelUrl),
        "--playlist-items", playlistItem.ToString(),
        ..CookieArgs(tools),
        "--skip-download",
        "--print", "%(id)s",
        "--user-agent", YtDlpPlan.UserAgent,
        "--no-warnings"
    ];

    public static IReadOnlyList<string> ProbeVideo(DownloadTools tools, string videoId) =>
    [
        WatchUrl(videoId),
        "--no-playlist",
        ..CookieArgs(tools),
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
        ..CookieArgs(tools),
        "--user-agent", YtDlpPlan.UserAgent,
        "--no-warnings"
    ];

    public static IReadOnlyList<string> ChannelCatalog(DownloadTools tools, PackChannelRequest request) =>
    [
        ChannelPlaylist.ToVideosUrl(request.ChannelUrl),
        "--flat-playlist",
        "--skip-download",
        "--print", "%(id)s\t%(playlist_index)s",
        ..CookieArgs(tools),
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
        if (request.Media == PackMediaMode.Audio)
        {
            return AudioArgs(tools, folder, Target(tools, request, playlistIndex, number, videoId: null));
        }

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
        if (request.Media == PackMediaMode.Audio)
        {
            return AudioArgs(tools, folder, Target(tools, request, playlistIndex: 0, number, videoId));
        }

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

    public static IReadOnlyList<string> Sub(
        DownloadTools tools,
        PackChannelRequest request,
        int playlistIndex,
        int number) =>
        SubArgs(tools, request, Target(tools, request, playlistIndex, number, videoId: null), languages: null);

    public static IReadOnlyList<string> SubById(
        DownloadTools tools,
        PackChannelRequest request,
        string videoId,
        int number) =>
        SubById(tools, request, videoId, number, languages: null);

    public static IReadOnlyList<string> SubById(
        DownloadTools tools,
        PackChannelRequest request,
        string videoId,
        int number,
        string? languages) =>
        SubArgs(tools, request, Target(tools, request, playlistIndex: 0, number, videoId), languages);

    public static IReadOnlyList<string> Sub(
        DownloadTools tools,
        PackChannelRequest request,
        int playlistIndex,
        int number,
        string? languages) =>
        SubArgs(tools, request, Target(tools, request, playlistIndex, number, videoId: null), languages);

    static IReadOnlyList<string> SubArgs(
        DownloadTools tools,
        PackChannelRequest request,
        IReadOnlyList<string> target,
        string? languages)
    {
        var folder = PackFolders.Channel(request);
        var sub = PackFolders.Sub(folder);
        var langs = string.IsNullOrWhiteSpace(languages)
            ? PackSubtitleLanguages.ForYtDlp(request.SubtitleLanguages)
            : languages;
        return target.Concat(
        [
            "--no-overwrites",
            "--skip-download",
            "--no-write-thumbnail",
            "--write-subs",
            "--write-auto-subs",
            "--sub-langs", langs,
            "--sub-format", "json3/vtt/best",
            "--convert-subs", "srt",
            "--ffmpeg-location", tools.FfmpegLocation,
            "--newline",
            "-P", "home:" + sub,
            "-P", "subtitle:" + sub,
            "-P", "temp:" + PackFolders.Temp(folder)
        ]).ToList();
    }

    /// <summary>
    /// Sound only, as AAC in .m4a so Premiere imports it directly. No video stream, no thumbnail or subtitles here.
    /// </summary>
    private static IReadOnlyList<string> AudioArgs(DownloadTools tools, string folder, IReadOnlyList<string> target) =>
        target.Concat(
        [
            "--no-overwrites",
            "--no-write-subs",
            "--no-write-auto-subs",
            "--no-write-thumbnail",
            "--ffmpeg-location", tools.FfmpegLocation,
            "--newline",
            "--no-mtime",
            "--format", "bestaudio[ext=m4a]/bestaudio/best",
            "--extract-audio",
            "--audio-format", "m4a",
            "--audio-quality", "0",
            "--no-write-playlist-metafiles",
            "--fragment-retries", "10",
            "--concurrent-fragments", "5",
            "--geo-bypass",
            "--match-filter", "!is_live",
            "-P", "home:" + PackFolders.Audio(folder),
            "-P", "temp:" + PackFolders.Temp(folder)
        ]).ToList();

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
                ..CookieArgs(tools),
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
            ..CookieArgs(tools),
            "--user-agent", YtDlpPlan.UserAgent,
            "--windows-filenames",
            "--retries", "10",
            "-o", OutputName(number)
        ];
    }
    private static IEnumerable<string> CookieArgs(DownloadTools tools) =>
        !string.IsNullOrWhiteSpace(tools.CookiesPath) && File.Exists(tools.CookiesPath)
            ? ["--cookies", tools.CookiesPath]
            : [];
}
