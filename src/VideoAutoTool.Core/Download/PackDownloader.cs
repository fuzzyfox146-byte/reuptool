namespace VideoAutoTool.Core.Download;

public sealed class PackDownloader
{
    public const int MaxParallel = 4;
    public const int MaxSpan = 300;

    private readonly IYtDlpRunner _runner;

    public PackDownloader(IYtDlpRunner runner) => _runner = runner;

    public async Task<PackDownloadResult> DownloadAsync(
        DownloadTools tools,
        PackChannelRequest request,
        int parallel,
        IProgress<DownloadNotice>? log,
        CancellationToken cancellationToken)
    {
        var results = await DownloadAllAsync(tools, [request], parallel, log, cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    public async Task<IReadOnlyList<PackDownloadResult>> DownloadAllAsync(
        DownloadTools tools,
        IReadOnlyList<PackChannelRequest> channels,
        int parallel,
        IProgress<DownloadNotice>? log,
        CancellationToken cancellationToken)
    {
        parallel = Math.Clamp(parallel, 1, MaxParallel);
        var results = new PackDownloadResult[channels.Count];
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var gate = new SemaphoreSlim(parallel);
        var cookie = 0;
        var tasks = new Task[channels.Count];
        for (var i = 0; i < channels.Count; i++)
        {
            var index = i;
            tasks[i] = RunChannelAsync(
                tools,
                channels[i],
                gate,
                log,
                linked,
                cancellationToken,
                () => Interlocked.Exchange(ref cookie, 1),
                () => Volatile.Read(ref cookie) == 1,
                value => results[index] = value);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        for (var i = 0; i < results.Length; i++)
        {
            if (results[i] is null)
            {
                results[i] = new PackDownloadResult(PackFolders.Channel(channels[i]), DownloadStop.Cancelled, "Đã dừng.");
            }
        }

        return results;
    }

    private async Task RunChannelAsync(
        DownloadTools tools,
        PackChannelRequest request,
        SemaphoreSlim gate,
        IProgress<DownloadNotice>? log,
        CancellationTokenSource linked,
        CancellationToken userToken,
        Action markCookie,
        Func<bool> cookieSeen,
        Action<PackDownloadResult> set)
    {
        var folder = PackFolders.Channel(request);
        try
        {
            var invalid = Validate(tools, request);
            if (invalid is not null)
            {
                set(new PackDownloadResult(folder, DownloadStop.Failed, invalid));
                return;
            }

            var videoDir = PackFolders.Video(folder);
            var thumbDir = PackFolders.Thumb(folder);
            Directory.CreateDirectory(videoDir);
            Directory.CreateDirectory(thumbDir);
            Directory.CreateDirectory(PackFolders.Temp(folder));

            var label = request.FolderName.Trim();
            IReadOnlyList<PackItem> pending;
            if (request.OrderByViews)
            {
                var ranked = await RankByViewsAsync(tools, request, label, log, linked.Token).ConfigureAwait(false);
                if (ranked.Result is not null)
                {
                    if (ranked.Result.Stop == DownloadStop.CookieDead)
                    {
                        markCookie();
                        linked.Cancel();
                    }

                    set(ranked.Result);
                    return;
                }

                pending = PackFolders.PendingRanked(videoDir, thumbDir, request, ranked.Entries);
            }
            else
            {
                pending = PackFolders.Pending(videoDir, thumbDir, request);
            }

            if (pending.Count == 0)
            {
                log?.Report(new DownloadNotice(null, $"{label}  đã có đủ video và thumb, không tải lại.", folder));
                set(new PackDownloadResult(folder, DownloadStop.Completed, "Đã có đủ video và thumb, không tải lại."));
                return;
            }

            var probe = await _runner.RunAsync(
                tools.YtDlpPath,
                pending[0].VideoId is { Length: > 0 } videoId
                    ? PackPlan.ProbeVideo(tools, videoId)
                    : PackPlan.Probe(tools, request, pending[0].PlaylistIndex),
                log: null,
                linked.Token).ConfigureAwait(false);
            if (probe.CookieDead)
            {
                markCookie();
                linked.Cancel();
                set(Cookie(folder, probe.CookieLine));
                return;
            }

            if (probe.ExitCode != 0)
            {
                set(new PackDownloadResult(folder, DownloadStop.Failed, "Không đọc được kênh hoặc cookie. Đã dừng trước khi tải."));
                return;
            }

            var itemTasks = pending.Select(item => RunItemAsync(
                tools,
                request,
                folder,
                item,
                gate,
                log,
                linked,
                markCookie,
                cookieSeen)).ToArray();
            var itemResults = await Task.WhenAll(itemTasks).ConfigureAwait(false);
            if (itemResults.Any(item => item.CookieDead) || cookieSeen())
            {
                var line = itemResults.Select(item => item.CookieLine).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
                set(Cookie(folder, line));
                return;
            }

            if (userToken.IsCancellationRequested)
            {
                set(new PackDownloadResult(folder, DownloadStop.Cancelled, "Đã dừng."));
                return;
            }

            if (itemResults.Any(item => item.ExitCode != 0))
            {
                set(new PackDownloadResult(folder, DownloadStop.Failed, "Video hoặc thumb lỗi. Các số khác vẫn được tải."));
                return;
            }

            set(new PackDownloadResult(
                folder,
                DownloadStop.Completed,
                $"Xong {label}. Video và thumb đánh số từ {request.NameStart:000}. Không tải phụ đề."));
        }
        catch (OperationCanceledException)
        {
            if (userToken.IsCancellationRequested)
            {
                set(new PackDownloadResult(folder, DownloadStop.Cancelled, "Đã dừng."));
                return;
            }

            set(cookieSeen()
                ? new PackDownloadResult(folder, DownloadStop.StoppedBecauseCookie, "Dừng vì cookie lỗi.")
                : new PackDownloadResult(folder, DownloadStop.Cancelled, "Đã dừng."));
        }
        catch (Exception ex)
        {
            set(new PackDownloadResult(folder, DownloadStop.Failed, ex.Message));
        }
    }

    private async Task<YtDlpRunResult> RunItemAsync(
        DownloadTools tools,
        PackChannelRequest request,
        string folder,
        PackItem item,
        SemaphoreSlim gate,
        IProgress<DownloadNotice>? log,
        CancellationTokenSource linked,
        Action markCookie,
        Func<bool> cookieSeen)
    {
        await gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            var exit = 0;
            if (item.NeedVideo)
            {
                var video = await RunLoggedAsync(
                    tools,
                    item.VideoId is { Length: > 0 } videoId
                        ? PackPlan.VideoById(tools, request, videoId, item.Number)
                        : PackPlan.Video(tools, request, item.PlaylistIndex, item.Number),
                    new DownloadActivityLog(log, "Video", folder),
                    linked.Token).ConfigureAwait(false);
                if (video.CookieDead)
                {
                    markCookie();
                    linked.Cancel();
                    return video;
                }

                if (video.ExitCode != 0)
                {
                    exit = video.ExitCode;
                }
            }

            if (item.NeedThumb && !linked.IsCancellationRequested)
            {
                var thumb = await RunLoggedAsync(
                    tools,
                    item.VideoId is { Length: > 0 } videoId
                        ? PackPlan.ThumbById(tools, request, videoId, item.Number)
                        : PackPlan.Thumb(tools, request, item.PlaylistIndex, item.Number),
                    new DownloadActivityLog(log, "Thumb", folder),
                    linked.Token).ConfigureAwait(false);
                if (thumb.CookieDead)
                {
                    markCookie();
                    linked.Cancel();
                    return thumb;
                }

                if (thumb.ExitCode != 0)
                {
                    exit = thumb.ExitCode;
                }
            }

            return new YtDlpRunResult(exit, false, null);
        }
        catch (OperationCanceledException)
        {
            return new YtDlpRunResult(-1, cookieSeen(), null);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<YtDlpRunResult> RunLoggedAsync(
        DownloadTools tools,
        IReadOnlyList<string> arguments,
        DownloadActivityLog activity,
        CancellationToken cancellationToken) =>
        _runner.RunAsync(tools.YtDlpPath, arguments, new Progress<string>(activity.OnLine), cancellationToken);

    private async Task<(IReadOnlyList<PackViewEntry> Entries, PackDownloadResult? Result)> RankByViewsAsync(
        DownloadTools tools,
        PackChannelRequest request,
        string label,
        IProgress<DownloadNotice>? log,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var catalog = await _runner.RunAsync(
            tools.YtDlpPath,
            PackPlan.ViewCatalog(tools, request),
            new Progress<string>(lines.Add),
            cancellationToken).ConfigureAwait(false);
        if (catalog.CookieDead)
        {
            return ([], Cookie(PackFolders.Channel(request), catalog.CookieLine));
        }

        if (catalog.ExitCode != 0)
        {
            return ([], new PackDownloadResult(PackFolders.Channel(request), DownloadStop.Failed, "Không đọc được view của kênh. Đã dừng trước khi tải."));
        }

        var ranked = PackViewRank.Order(PackViewRank.Parse(lines));
        if (ranked.Count < request.PlaylistEnd)
        {
            return ([], new PackDownloadResult(
                PackFolders.Channel(request),
                DownloadStop.Failed,
                $"Kênh có {ranked.Count} video, không đủ đến hạng {request.PlaylistEnd} theo view."));
        }

        var window = ranked.Skip(request.PlaylistStart - 1).Take(request.PlaylistEnd - request.PlaylistStart + 1).ToList();
        var top = window[0];
        var number = request.NameStart;
        if (window.All(entry => entry.Views == 0))
        {
            log?.Report(new DownloadNotice(null, $"{label}  Không đọc được số view, giữ thứ tự trên kênh.", PackFolders.Channel(request)));
        }
        else
        {
            log?.Report(new DownloadNotice(
                null,
                $"{label}  Theo view: file {number:000} là {top.Views} view, rồi giảm dần.",
                PackFolders.Channel(request)));
        }

        return (ranked, null);
    }

    private static PackDownloadResult Cookie(string folder, string? line)
    {
        var detail = "Cookie lỗi. Đã dừng, không tải tiếp.";
        if (!string.IsNullOrWhiteSpace(line))
        {
            detail += " " + line.Trim();
        }

        return new PackDownloadResult(folder, DownloadStop.CookieDead, detail);
    }

    private static string? Validate(DownloadTools tools, PackChannelRequest request)
    {
        if (string.IsNullOrWhiteSpace(tools.YtDlpPath) || !File.Exists(tools.YtDlpPath))
        {
            return "Không thấy yt-dlp. Hãy chọn file yt-dlp.exe.";
        }

        if (string.IsNullOrWhiteSpace(tools.CookiesPath) || !File.Exists(tools.CookiesPath))
        {
            return "Không thấy file cookie. Hãy chọn cookies.txt dùng chung.";
        }

        var ffmpeg = Path.Combine(tools.FfmpegLocation ?? "", "ffmpeg.exe");
        if (string.IsNullOrWhiteSpace(tools.FfmpegLocation) || !File.Exists(ffmpeg))
        {
            return "Không thấy ffmpeg.exe trong thư mục bin cạnh yt-dlp.";
        }

        if (string.IsNullOrWhiteSpace(request.RootFolder))
        {
            return "Chọn thư mục gốc để chứa folder kênh.";
        }

        var nameError = PackFolders.InvalidName(request.FolderName);
        if (nameError is not null)
        {
            return nameError;
        }

        if (string.IsNullOrWhiteSpace(request.ChannelUrl))
        {
            return "Chưa nhập link kênh.";
        }

        if (request.PlaylistStart < 1 || request.PlaylistEnd < request.PlaylistStart)
        {
            return "Vị trí từ–đến không hợp lệ.";
        }

        if (request.PlaylistEnd - request.PlaylistStart + 1 > MaxSpan)
        {
            return "Một lượt tối đa 300 video.";
        }

        if (request.NameStart < 1)
        {
            return "Số đặt tên phải từ 1 (ra 001).";
        }

        return null;
    }
}
