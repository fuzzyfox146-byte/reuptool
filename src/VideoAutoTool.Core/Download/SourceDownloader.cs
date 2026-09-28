namespace VideoAutoTool.Core.Download;

public sealed class SourceDownloader
{
    public static readonly TimeSpan DefaultSubtitleRetryDelay = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan DefaultSubtitleItemDelay = TimeSpan.FromSeconds(3);
    public const int DefaultSubtitleAttempts = 4;

    private readonly IYtDlpRunner _runner;
    private readonly TimeSpan _subtitleRetryDelay;
    private readonly TimeSpan _subtitleItemDelay;
    private readonly int _subtitleAttempts;

    public SourceDownloader(IYtDlpRunner runner)
        : this(runner, DefaultSubtitleRetryDelay, DefaultSubtitleItemDelay, DefaultSubtitleAttempts)
    {
    }

    public SourceDownloader(
        IYtDlpRunner runner,
        TimeSpan subtitleRetryDelay,
        TimeSpan subtitleItemDelay,
        int subtitleAttempts)
    {
        _runner = runner;
        _subtitleRetryDelay = subtitleRetryDelay;
        _subtitleItemDelay = subtitleItemDelay;
        _subtitleAttempts = Math.Max(1, subtitleAttempts);
    }

    public async Task<ChannelDownloadResult> DownloadAsync(
        DownloadTools tools,
        ChannelDownloadRequest request,
        IProgress<DownloadNotice>? log,
        CancellationToken cancellationToken)
    {
        var folder = request.ParentFolder;
        var invalid = Validate(tools, request);
        if (invalid is not null)
        {
            return new ChannelDownloadResult(folder, DownloadStop.Failed, invalid);
        }

        var sourceDir = Path.Combine(folder, "source");
        var textDir = Path.Combine(folder, "text");
        var thumDir = Path.Combine(folder, "thum");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(textDir);
        Directory.CreateDirectory(thumDir);
        Directory.CreateDirectory(Path.Combine(folder, "temp"));
        Json3ToSrt.ConvertFolder(textDir, log: null);
        DownloadResume.RemoveMismatched(textDir, sourceDir, request, DownloadResume.IsSubtitleFile);
        DownloadResume.RemoveMismatched(thumDir, sourceDir, request, DownloadResume.IsThumbnailFile);

        var videoSlices = DownloadResume.OneItemEach(DownloadResume.MissingVideos(sourceDir, request));
        var textSlices = DownloadResume.OneItemEach(DownloadResume.MissingSubtitles(textDir, sourceDir, request));
        var thumbSlices = DownloadResume.OneItemEach(DownloadResume.MissingThumbnails(thumDir, sourceDir, request));
        if (videoSlices.Count == 0 && textSlices.Count == 0 && thumbSlices.Count == 0)
        {
            var label = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            log?.Report(new DownloadNotice(null, $"{label}  đã có đủ video, phụ đề và thumb, không tải lại."));
            return new ChannelDownloadResult(folder, DownloadStop.Completed, "Đã có đủ, không tải lại.");
        }

        var probeItem = videoSlices.Concat(textSlices).Concat(thumbSlices).Min(slice => slice.PlaylistStart);
        var probe = await _runner.RunAsync(
            tools.YtDlpPath,
            YtDlpPlan.Probe(tools, request, probeItem),
            log: null,
            cancellationToken).ConfigureAwait(false);
        if (probe.CookieDead)
        {
            return Cookie(folder, probe.CookieLine);
        }

        if (probe.ExitCode != 0)
        {
            return new ChannelDownloadResult(folder, DownloadStop.Failed, "Không đọc được kênh hoặc cookie. Đã dừng trước khi tải.");
        }

        var catalog = Array.Empty<PlaylistEntry>();
        if (NeedsCatalog(sourceDir, textSlices, thumbSlices))
        {
            var catalogLines = new LineCollector();
            var catalogRun = await _runner.RunAsync(
                tools.YtDlpPath,
                YtDlpPlan.Catalog(tools, request),
                catalogLines,
                cancellationToken).ConfigureAwait(false);
            if (catalogRun.CookieDead)
            {
                return Cookie(folder, catalogRun.CookieLine);
            }

            if (catalogRun.ExitCode != 0)
            {
                return new ChannelDownloadResult(
                    folder,
                    DownloadStop.Failed,
                    "Không đọc được danh sách kênh. Đã dừng trước khi tải phụ đề/thumb.");
            }

            catalog = PlaylistCatalog.Parse(catalogLines.Snapshot()).ToArray();
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var videoLog = new DownloadActivityLog(log, "Video", folder);
        var textLog = new DownloadActivityLog(log, "Text", folder);
        var thumbLog = new DownloadActivityLog(log, "Thumb", folder);
        var videoTask = RunSlicesAsync(
            tools,
            videoSlices,
            slice => YtDlpPlan.Videos(tools, request, slice),
            videoLog,
            linked.Token);
        var subsTask = RunSubtitleSlicesAsync(
            tools,
            request,
            textSlices,
            sourceDir,
            textDir,
            catalog,
            textLog,
            log,
            linked.Token);
        var thumbsTask = RunThumbnailSlicesAsync(
            tools,
            request,
            thumbSlices,
            sourceDir,
            thumDir,
            catalog,
            thumbLog,
            log,
            linked.Token);

        YtDlpRunResult video;
        YtDlpRunResult subs;
        YtDlpRunResult thumbs;
        try
        {
            var firstDone = await Task.WhenAny(videoTask, subsTask, thumbsTask).ConfigureAwait(false);
            var first = await firstDone.ConfigureAwait(false);
            if (first.CookieDead)
            {
                linked.Cancel();
            }

            video = await videoTask.ConfigureAwait(false);
            subs = await subsTask.ConfigureAwait(false);
            thumbs = await thumbsTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            video = videoTask.Status == TaskStatus.RanToCompletion
                ? videoTask.Result
                : new YtDlpRunResult(-1, true, null);
            subs = subsTask.Status == TaskStatus.RanToCompletion
                ? subsTask.Result
                : new YtDlpRunResult(-1, true, null);
            thumbs = thumbsTask.Status == TaskStatus.RanToCompletion
                ? thumbsTask.Result
                : new YtDlpRunResult(-1, true, null);
        }

        Json3ToSrt.ConvertFolder(textDir, log: null);
        if (video.CookieDead)
        {
            return Cookie(folder, video.CookieLine);
        }

        if (subs.CookieDead)
        {
            return Cookie(folder, subs.CookieLine);
        }

        if (thumbs.CookieDead)
        {
            return Cookie(folder, thumbs.CookieLine);
        }

        if (video.ExitCode != 0 || subs.ExitCode != 0 || thumbs.ExitCode != 0)
        {
            var parts = new List<string>();
            if (video.ExitCode != 0)
            {
                parts.Add("video");
            }

            if (subs.ExitCode != 0)
            {
                parts.Add("phụ đề");
            }

            if (thumbs.ExitCode != 0)
            {
                parts.Add("thumb");
            }

            return new ChannelDownloadResult(
                folder,
                DownloadStop.Failed,
                char.ToUpperInvariant(parts[0][0]) + parts[0][1..] + " lỗi hoặc hết. Đã dừng phần đó.");
        }

        return new ChannelDownloadResult(
            folder,
            DownloadStop.Completed,
            $"Xong. Video, phụ đề và thumb đánh số từ {request.NameStart:000}.");
    }

    private async Task<YtDlpRunResult> RunSlicesAsync(
        DownloadTools tools,
        IReadOnlyList<DownloadSlice> slices,
        Func<DownloadSlice, IReadOnlyList<string>> arguments,
        DownloadActivityLog activity,
        CancellationToken cancellationToken)
    {
        var exitCode = 0;
        foreach (var slice in slices)
        {
            var result = await _runner.RunAsync(
                tools.YtDlpPath,
                arguments(slice),
                new Progress<string>(activity.OnLine),
                cancellationToken).ConfigureAwait(false);
            if (result.CookieDead)
            {
                return result;
            }

            if (result.ExitCode != 0)
            {
                exitCode = result.ExitCode;
            }
        }

        return new YtDlpRunResult(exitCode, false, null);
    }

    private async Task<YtDlpRunResult> RunSubtitleSlicesAsync(
        DownloadTools tools,
        ChannelDownloadRequest request,
        IReadOnlyList<DownloadSlice> slices,
        string sourceDir,
        string textDir,
        IReadOnlyList<PlaylistEntry> catalog,
        DownloadActivityLog activity,
        IProgress<DownloadNotice>? log,
        CancellationToken cancellationToken)
    {
        var exitCode = 0;
        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            var videoUrl = SideUrl(sourceDir, slice.NameStart, catalog);
            if (VideoExists(sourceDir, slice.NameStart) && string.IsNullOrWhiteSpace(videoUrl))
            {
                exitCode = 1;
                continue;
            }

            var have = false;
            for (var attempt = 1; attempt <= _subtitleAttempts; attempt++)
            {
                var result = await _runner.RunAsync(
                    tools.YtDlpPath,
                    YtDlpPlan.Subtitles(tools, request, slice, videoUrl),
                    new Progress<string>(activity.OnLine),
                    cancellationToken).ConfigureAwait(false);
                if (result.CookieDead)
                {
                    return result;
                }

                Json3ToSrt.ConvertFolder(textDir, log: null);
                if (DownloadResume.HasMatchingPair(textDir, sourceDir, slice.NameStart, DownloadResume.IsSubtitleFile))
                {
                    have = true;
                    break;
                }

                if (attempt < _subtitleAttempts)
                {
                    var label = Path.GetFileName(
                        request.ParentFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    var wait = _subtitleRetryDelay > TimeSpan.Zero
                        ? $" sau {_subtitleRetryDelay.TotalSeconds:0}s"
                        : "";
                    log?.Report(new DownloadNotice(
                        null,
                        $"{label}  Phụ đề {slice.NameStart:000}: chưa có file, thử lại lần {attempt + 1}/{_subtitleAttempts}{wait}."));
                    if (_subtitleRetryDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(_subtitleRetryDelay, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (!have)
            {
                exitCode = 1;
            }
            else if (i < slices.Count - 1 && _subtitleItemDelay > TimeSpan.Zero)
            {
                await Task.Delay(_subtitleItemDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return new YtDlpRunResult(exitCode, false, null);
    }

    private async Task<YtDlpRunResult> RunThumbnailSlicesAsync(
        DownloadTools tools,
        ChannelDownloadRequest request,
        IReadOnlyList<DownloadSlice> slices,
        string sourceDir,
        string thumDir,
        IReadOnlyList<PlaylistEntry> catalog,
        DownloadActivityLog activity,
        IProgress<DownloadNotice>? log,
        CancellationToken cancellationToken)
    {
        var exitCode = 0;
        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            var videoUrl = SideUrl(sourceDir, slice.NameStart, catalog);
            if (VideoExists(sourceDir, slice.NameStart) && string.IsNullOrWhiteSpace(videoUrl))
            {
                exitCode = 1;
                continue;
            }

            var have = false;
            for (var attempt = 1; attempt <= _subtitleAttempts; attempt++)
            {
                var result = await _runner.RunAsync(
                    tools.YtDlpPath,
                    YtDlpPlan.Thumbnails(tools, request, slice, videoUrl),
                    new Progress<string>(activity.OnLine),
                    cancellationToken).ConfigureAwait(false);
                if (result.CookieDead)
                {
                    return result;
                }

                if (DownloadResume.HasMatchingPair(thumDir, sourceDir, slice.NameStart, DownloadResume.IsThumbnailFile))
                {
                    have = true;
                    break;
                }

                if (attempt < _subtitleAttempts)
                {
                    var label = Path.GetFileName(
                        request.ParentFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    var wait = _subtitleRetryDelay > TimeSpan.Zero
                        ? $" sau {_subtitleRetryDelay.TotalSeconds:0}s"
                        : "";
                    log?.Report(new DownloadNotice(
                        null,
                        $"{label}  Thumb {slice.NameStart:000}: chưa có file, thử lại lần {attempt + 1}/{_subtitleAttempts}{wait}."));
                    if (_subtitleRetryDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(_subtitleRetryDelay, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (!have)
            {
                exitCode = 1;
            }
            else if (i < slices.Count - 1 && _subtitleItemDelay > TimeSpan.Zero)
            {
                await Task.Delay(_subtitleItemDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return new YtDlpRunResult(exitCode, false, null);
    }

    private static bool NeedsCatalog(
        string sourceDir,
        IReadOnlyList<DownloadSlice> textSlices,
        IReadOnlyList<DownloadSlice> thumbSlices) =>
        textSlices.Concat(thumbSlices).Any(slice => VideoExists(sourceDir, slice.NameStart));

    private static bool VideoExists(string sourceDir, int number) =>
        DownloadResume.FileForNumber(sourceDir, number, DownloadResume.IsVideoFile) is not null;

    private static string? SideUrl(string sourceDir, int number, IReadOnlyList<PlaylistEntry> catalog)
    {
        var path = DownloadResume.FileForNumber(sourceDir, number, DownloadResume.IsVideoFile);
        if (path is null)
        {
            return null;
        }

        var entry = PlaylistCatalog.Find(Path.GetFileName(path), number, catalog);
        return entry is null ? null : PlaylistCatalog.WatchUrl(entry.Id);
    }

    private static ChannelDownloadResult Cookie(string folder, string? line)
    {
        var detail = "Cookie lỗi. Đã dừng, không tải tiếp.";
        if (!string.IsNullOrWhiteSpace(line))
        {
            detail += " " + line.Trim();
        }

        return new ChannelDownloadResult(folder, DownloadStop.CookieDead, detail);
    }

    private static string? Validate(DownloadTools tools, ChannelDownloadRequest request)
    {
        if (string.IsNullOrWhiteSpace(tools.YtDlpPath) || !File.Exists(tools.YtDlpPath))
        {
            return "Không thấy yt-dlp. Hãy chọn file yt-dlp.exe.";
        }

        if (string.IsNullOrWhiteSpace(tools.CookiesPath) || !File.Exists(tools.CookiesPath))
        {
            return "Không thấy file cookie. Hãy chọn cookies.txt dùng chung.";
        }

        if (string.IsNullOrWhiteSpace(request.ParentFolder))
        {
            return "Chưa chọn folder tải về.";
        }

        if (string.IsNullOrWhiteSpace(request.ChannelUrl))
        {
            return "Chưa nhập link kênh.";
        }

        if (request.PlaylistStart < 1 || request.PlaylistEnd < request.PlaylistStart)
        {
            return "Vị trí từ–đến không hợp lệ.";
        }

        if (request.NameStart < 1)
        {
            return "Số đặt tên phải từ 1 (ra 001).";
        }

        if (string.IsNullOrWhiteSpace(request.Language) || request.Language.Any(char.IsWhiteSpace))
        {
            return "Mã ngôn ngữ không hợp lệ.";
        }

        return null;
    }

    private sealed class LineCollector : IProgress<string>
    {
        private readonly List<string> _lines = [];

        public void Report(string value) => _lines.Add(value);

        public IReadOnlyList<string> Snapshot() => _lines.ToArray();
    }
}
