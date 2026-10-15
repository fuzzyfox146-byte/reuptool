using VideoAutoTool.Core.Download;

namespace VideoAutoTool.Core.Tests.Download;

public class PackDownloadTests
{
    [Fact]
    public void Plan_DownloadsVideoAndThumbSeparately_WithoutSubtitles()
    {
        var tools = Tools(@"D:\tools");
        var request = new PackChannelRequest(@"D:\Kenh", "HD036", "https://www.youtube.com/@Chosen", 3, 5, 1, "720");

        var video = PackPlan.Video(tools, request, 3, 1);
        var thumb = PackPlan.Thumb(tools, request, 3, 1);

        Assert.Equal("https://www.youtube.com/@Chosen/videos", video[0]);
        Assert.Contains("3", video);
        Assert.Contains("001 %(title).100s.%(ext)s", video);
        Assert.Contains(VideoQuality.Format("720"), video);
        Assert.Contains(@"home:D:\Kenh\HD036\video", video);
        Assert.Contains("--no-write-subs", video);
        Assert.Contains("--no-write-auto-subs", video);
        Assert.Contains("--no-write-thumbnail", video);
        Assert.DoesNotContain(video, arg => arg is "--write-subs" or "--write-auto-subs" or "--sub-langs" or "--write-thumbnail");

        Assert.Contains(@"thumbnail:D:\Kenh\HD036\thumb", thumb);
        Assert.Contains("--write-thumbnail", thumb);
        Assert.Contains("jpg", thumb);
        Assert.Contains("--skip-download", thumb);
        Assert.Contains("--no-write-subs", thumb);
        Assert.DoesNotContain(thumb, arg => arg is "--write-subs" or "--write-auto-subs" or "--sub-langs");
    }

    [Fact]
    public async Task InvalidFolderName_DoesNotCallYtDlp()
    {
        using var home = new ToolHome();
        var runner = new RecordingYtDlp();
        var downloader = new PackDownloader(runner);
        var request = new PackChannelRequest(home.Root, "HD/036", "https://www.youtube.com/@Chosen", 1, 1, 1, "720");

        var result = await downloader.DownloadAsync(home.Tools, request, 2, null, CancellationToken.None);

        Assert.Equal(DownloadStop.Failed, result.Stop);
        Assert.Contains("ký tự", result.Detail);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task SkipsNumbersThatAlreadyHaveVideoAndThumb()
    {
        using var home = new ToolHome();
        var channel = Path.Combine(home.Root, "HD036");
        Directory.CreateDirectory(Path.Combine(channel, "video"));
        Directory.CreateDirectory(Path.Combine(channel, "thumb"));
        File.WriteAllText(Path.Combine(channel, "video", "001 Title.mp4"), "v");
        File.WriteAllText(Path.Combine(channel, "thumb", "001 Title.jpg"), "t");
        var runner = new RecordingYtDlp();
        var downloader = new PackDownloader(runner);
        var request = new PackChannelRequest(home.Root, "HD036", "https://www.youtube.com/@Chosen", 1, 2, 1, "720");

        var result = await downloader.DownloadAsync(home.Tools, request, 2, null, CancellationToken.None);

        Assert.Equal(DownloadStop.Completed, result.Stop);
        Assert.Contains(runner.Calls, call => call.Contains("002 %(title).100s.%(ext)s"));
        Assert.DoesNotContain(runner.Calls, call => call.Contains("001 %(title).100s.%(ext)s"));
        Assert.Contains(runner.Calls, call => call.Contains("--merge-output-format"));
        Assert.Contains(runner.Calls, call => call.Contains("--write-thumbnail"));
    }

    [Fact]
    public async Task CompletePair_DoesNotDownloadAgain()
    {
        using var home = new ToolHome();
        var channel = Path.Combine(home.Root, "HD036");
        Directory.CreateDirectory(Path.Combine(channel, "video"));
        Directory.CreateDirectory(Path.Combine(channel, "thumb"));
        File.WriteAllText(Path.Combine(channel, "video", "001 Title.mp4"), "v");
        File.WriteAllText(Path.Combine(channel, "thumb", "001 Title.jpg"), "t");
        var runner = new RecordingYtDlp();
        var downloader = new PackDownloader(runner);

        var result = await downloader.DownloadAsync(
            home.Tools,
            new PackChannelRequest(home.Root, "HD036", "https://www.youtube.com/@Chosen", 1, 1, 1, "720"),
            2,
            null,
            CancellationToken.None);

        Assert.Equal(DownloadStop.Completed, result.Stop);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task ParallelVideos_StayWithinTheLimit()
    {
        using var home = new ToolHome();
        var runner = new RecordingYtDlp { Delay = TimeSpan.FromMilliseconds(80) };
        var downloader = new PackDownloader(runner);
        var request = new PackChannelRequest(home.Root, "HD036", "https://www.youtube.com/@Chosen", 1, 4, 1, "720");

        var result = await downloader.DownloadAsync(home.Tools, request, 2, null, CancellationToken.None);

        Assert.Equal(DownloadStop.Completed, result.Stop);
        Assert.Equal(2, runner.MaxInFlight);
        Assert.Equal(9, runner.Calls.Count);
        Assert.Contains("Không tải phụ đề", result.Detail);
    }

    [Fact]
    public async Task CookieFailure_StopsBeforeVideos()
    {
        using var home = new ToolHome();
        var runner = new RecordingYtDlp { ProbeCookieDead = true };
        var downloader = new PackDownloader(runner);

        var result = await downloader.DownloadAsync(
            home.Tools,
            new PackChannelRequest(home.Root, "HD036", "https://www.youtube.com/@Chosen", 1, 3, 1, "720"),
            3,
            null,
            CancellationToken.None);

        Assert.Equal(DownloadStop.CookieDead, result.Stop);
        Assert.Single(runner.Calls);
        Assert.Contains("--print", runner.Calls[0]);
    }

    [Fact]
    public void ViewRank_PutsTheHighestViewFirst_AndKeepsChannelOrderOnATie()
    {
        var ranked = PackViewRank.Order(PackViewRank.Parse(
        [
            "aaaaaaaaaaa\t10\t1",
            "bbbbbbbbbbb\t90\t2",
            "ccccccccccc\t90\t3",
            "không phải dòng view",
            "ddddddddddd\tNA\t4"
        ]));

        Assert.Equal(
            ["bbbbbbbbbbb", "ccccccccccc", "aaaaaaaaaaa", "ddddddddddd"],
            ranked.Select(entry => entry.Id).ToArray());
    }

    [Fact]
    public async Task ViewOrder_NumbersTheHighestViewAs001()
    {
        using var home = new ToolHome();
        var runner = new RecordingYtDlp
        {
            CatalogLines =
            [
                "aaaaaaaaaaa\t10\t1",
                "bbbbbbbbbbb\t90\t2",
                "ccccccccccc\t40\t3"
            ]
        };
        var downloader = new PackDownloader(runner);
        var request = new PackChannelRequest(home.Root, "HD036", "https://www.youtube.com/@Chosen", 1, 2, 1, "720", true);

        var result = await downloader.DownloadAsync(home.Tools, request, 2, null, CancellationToken.None);

        Assert.Equal(DownloadStop.Completed, result.Stop);
        Assert.Contains(runner.Calls, call => call.Contains("https://www.youtube.com/watch?v=bbbbbbbbbbb") && call.Contains("001 %(title).100s.%(ext)s"));
        Assert.Contains(runner.Calls, call => call.Contains("https://www.youtube.com/watch?v=ccccccccccc") && call.Contains("002 %(title).100s.%(ext)s"));
        Assert.DoesNotContain(runner.Calls, call => call.Any(arg => arg.Contains("aaaaaaaaaaa", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ViewOrder_FailsWhenTheChannelHasFewerVideosThanTheRank()
    {
        using var home = new ToolHome();
        var runner = new RecordingYtDlp { CatalogLines = ["aaaaaaaaaaa\t10\t1"] };
        var downloader = new PackDownloader(runner);
        var request = new PackChannelRequest(home.Root, "HD036", "https://www.youtube.com/@Chosen", 1, 2, 1, "720", true);

        var result = await downloader.DownloadAsync(home.Tools, request, 1, null, CancellationToken.None);

        Assert.Equal(DownloadStop.Failed, result.Stop);
        Assert.Contains("không đủ", result.Detail);
        Assert.DoesNotContain(runner.Calls, call => call.Contains("--merge-output-format"));
    }

    private static DownloadTools Tools(string root) =>
        new(Path.Combine(root, "yt-dlp.exe"), Path.Combine(root, "bin"), Path.Combine(root, "cookies.txt"));

    private sealed class ToolHome : IDisposable
    {
        public ToolHome()
        {
            Root = Path.Combine(Path.GetTempPath(), "hd-pack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "bin"));
            File.WriteAllText(Path.Combine(Root, "yt-dlp.exe"), "x");
            File.WriteAllText(Path.Combine(Root, "bin", "ffmpeg.exe"), "x");
            File.WriteAllText(Path.Combine(Root, "cookies.txt"), "x");
            Tools = PackDownloadTests.Tools(Root);
        }

        public string Root { get; }

        public DownloadTools Tools { get; }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class RecordingYtDlp : IYtDlpRunner
    {
        private int _current;

        public List<IReadOnlyList<string>> Calls { get; } = [];

        public int MaxInFlight { get; private set; }

        public TimeSpan Delay { get; init; }

        public bool ProbeCookieDead { get; init; }

        public IReadOnlyList<string> CatalogLines { get; init; } = [];

        public async Task<YtDlpRunResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            IProgress<string>? log,
            CancellationToken cancellationToken)
        {
            lock (Calls)
            {
                Calls.Add(arguments);
            }

            if (arguments.Any(arg => arg.Contains("view_count", StringComparison.Ordinal)))
            {
                foreach (var line in CatalogLines)
                {
                    log?.Report(line);
                }

                return new YtDlpRunResult(0, false, null);
            }

            if (arguments.Contains("--print"))
            {
                return ProbeCookieDead
                    ? new YtDlpRunResult(1, true, "ERROR: cookies are no longer valid")
                    : new YtDlpRunResult(0, false, null);
            }

            var now = Interlocked.Increment(ref _current);
            lock (Calls)
            {
                MaxInFlight = Math.Max(MaxInFlight, now);
            }
            try
            {
                if (Delay > TimeSpan.Zero)
                {
                    await Task.Delay(Delay, cancellationToken);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }

            return new YtDlpRunResult(0, false, null);
        }
    }
}
