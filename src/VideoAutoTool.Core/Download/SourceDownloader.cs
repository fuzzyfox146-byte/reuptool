namespace VideoAutoTool.Core.Download;

public sealed class SourceDownloader
{
    private readonly IYtDlpRunner _runner;

    public SourceDownloader(IYtDlpRunner runner) => _runner = runner;

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
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(textDir);
        Directory.CreateDirectory(Path.Combine(folder, "thum"));
        Directory.CreateDirectory(Path.Combine(folder, "temp"));
        Json3ToSrt.ConvertFolder(textDir, log: null);

        var videoSlices = DownloadResume.MissingVideos(sourceDir, request);
        var textSlices = DownloadResume.MissingSubtitles(textDir, request);
        if (videoSlices.Count == 0 && textSlices.Count == 0)
        {
            var label = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            log?.Report(new DownloadNotice(null, $"{label}  đã có đủ video và phụ đề, không tải lại."));
            return new ChannelDownloadResult(folder, DownloadStop.Completed, "Đã có đủ, không tải lại.");
        }

        var probeItem = videoSlices.Concat(textSlices).Min(slice => slice.PlaylistStart);
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

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var videoLog = new DownloadActivityLog(log, "Video", folder);
        var textLog = new DownloadActivityLog(log, "Text", folder);
        var videoTask = RunSlicesAsync(
            tools,
            videoSlices,
            slice => YtDlpPlan.Videos(tools, request, slice),
            videoLog,
            linked.Token);
        var subsTask = RunSlicesAsync(
            tools,
            textSlices,
            slice => YtDlpPlan.Subtitles(tools, request, slice),
            textLog,
            linked.Token);

        YtDlpRunResult video;
        YtDlpRunResult subs;
        try
        {
            var firstDone = await Task.WhenAny(videoTask, subsTask).ConfigureAwait(false);
            var first = await firstDone.ConfigureAwait(false);
            if (first.CookieDead)
            {
                linked.Cancel();
            }

            video = await videoTask.ConfigureAwait(false);
            subs = await subsTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            video = videoTask.Status == TaskStatus.RanToCompletion
                ? videoTask.Result
                : new YtDlpRunResult(-1, true, null);
            subs = subsTask.Status == TaskStatus.RanToCompletion
                ? subsTask.Result
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

        if (video.ExitCode != 0 || subs.ExitCode != 0)
        {
            var detail = (video.ExitCode != 0, subs.ExitCode != 0) switch
            {
                (true, false) => "Video lỗi hoặc hết. Đã dừng tải video. Phụ đề không bị dừng vì lỗi này.",
                (false, true) => "Phụ đề lỗi. Đã dừng tải phụ đề. Video không bị dừng vì lỗi này.",
                _ => "Video lỗi hoặc hết và phụ đề lỗi. Đã dừng cả hai."
            };
            return new ChannelDownloadResult(folder, DownloadStop.Failed, detail);
        }

        return new ChannelDownloadResult(
            folder,
            DownloadStop.Completed,
            $"Xong. Video và phụ đề đánh số từ {request.NameStart:000}.");
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
                return result;
            }
        }

        return new YtDlpRunResult(exitCode, false, null);
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
}
