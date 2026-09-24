using VideoAutoTool.Core.Download;

namespace VideoAutoTool.Core.Tests.Download;

public class DownloadTests
{
    [Theory]
    [InlineData("Sign in to confirm you’re not a bot")]
    [InlineData("ERROR: cookies are no longer valid")]
    [InlineData("HTTP Error 401: Unauthorized")]
    [InlineData("LOGIN_REQUIRED")]
    public void CookieLine_IsFatal(string line)
    {
        Assert.True(CookieFailure.IsFatal(line));
    }

    [Fact]
    public void OrdinaryYtDlpError_IsNotCookie()
    {
        Assert.False(CookieFailure.IsFatal("ERROR: unable to download video data"));
    }

    [Fact]
    public void ChannelUrl_BecomesVideosPlaylist()
    {
        Assert.Equal(
            "https://www.youtube.com/@Chosen/videos",
            ChannelPlaylist.ToVideosUrl("https://www.youtube.com/@Chosen/videos/"));
    }

    [Fact]
    public void VideoPlan_UsesSameNumberingAndArchive()
    {
        var tools = new DownloadTools(@"D:\tools\yt-dlp.exe", @"D:\tools\bin", @"D:\tools\cookies.txt");
        var request = new ChannelDownloadRequest(@"D:\RV144", "https://www.youtube.com/@Chosen", 3, 9, 1, "de", "720");

        var video = YtDlpPlan.Videos(tools, request);
        var subs = YtDlpPlan.Subtitles(tools, request);

        Assert.Contains("--playlist-start", video);
        Assert.Contains("3", video);
        Assert.Contains("9", video);
        Assert.Contains("--autonumber-start", video);
        Assert.Contains(YtDlpPlan.OutputTemplate, video);
        Assert.Contains("--no-overwrites", video);
        Assert.Contains("--no-overwrites", subs);
        Assert.DoesNotContain(video, arg => arg.Contains("downloaded_videos.txt", StringComparison.Ordinal));
        Assert.Contains("de", subs);
        Assert.Contains("--newline", video);
        Assert.Contains(VideoQuality.Format("720"), video);
    }

    [Fact]
    public void ClearArchive_DeletesOnlyHistoryFiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vat-archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var videos = Path.Combine(folder, DownloadArchive.VideosFileName);
            var subs = Path.Combine(folder, DownloadArchive.SubsFileName);
            var kept = Path.Combine(folder, "source.txt");
            File.WriteAllText(videos, "a");
            File.WriteAllText(subs, "b");
            File.WriteAllText(kept, "c");

            var removed = DownloadArchive.Clear(folder);

            Assert.Equal(2, removed.Count);
            Assert.False(File.Exists(videos));
            Assert.False(File.Exists(subs));
            Assert.True(File.Exists(kept));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Json3_DropsOverlappingWords()
    {
        const string json = """
            {
              "events": [
                {"tStartMs": 0, "dDurationMs": 1000, "segs": [{"utf8": "hello world"}]},
                {"tStartMs": 1000, "dDurationMs": 1000, "segs": [{"utf8": "world again"}]}
              ]
            }
            """;

        var srt = Json3ToSrt.Convert(json);

        Assert.Contains("hello world", srt);
        Assert.Contains("again", srt);
        Assert.DoesNotContain("world again", srt);
    }

    [Fact]
    public async Task CookieFailure_StopsOnlyThatChannel()
    {
        var root = Path.Combine(Path.GetTempPath(), "vat-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var yt = Path.Combine(root, "yt-dlp.exe");
        var cookies = Path.Combine(root, "cookies.txt");
        File.WriteAllText(yt, "");
        File.WriteAllText(cookies, "");
        var tools = new DownloadTools(yt, root, cookies);
        var runner = new ScriptedYtDlp
        {
            Handler = (args, _) =>
            {
                if (args[0].Contains("@channel-a", StringComparison.Ordinal))
                {
                    return Task.FromResult(new YtDlpRunResult(1, true, "Sign in to confirm"));
                }

                return Task.FromResult(new YtDlpRunResult(0, false, null));
            }
        };
        var multi = new MultiChannelDownloader(new SourceDownloader(runner));
        var channels = new[]
        {
            new ChannelDownloadRequest(Path.Combine(root, "a"), "https://www.youtube.com/@channel-a", 1, 2, 1, "en", "144"),
            new ChannelDownloadRequest(Path.Combine(root, "b"), "https://www.youtube.com/@channel-b", 1, 2, 1, "en", "144")
        };

        try
        {
            var results = await multi.DownloadAllAsync(tools, channels, parallel: 2, log: null, CancellationToken.None);

            Assert.Equal(DownloadStop.CookieDead, results[0].Stop);
            Assert.Equal(DownloadStop.Completed, results[1].Stop);
            Assert.Contains(runner.Calls, args => args[0].Contains("@channel-b", StringComparison.Ordinal) && args.Contains("--format"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FailedChannel_DoesNotStopTheOther()
    {
        var root = Path.Combine(Path.GetTempPath(), "vat-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var yt = Path.Combine(root, "yt-dlp.exe");
        var cookies = Path.Combine(root, "cookies.txt");
        File.WriteAllText(yt, "");
        File.WriteAllText(cookies, "");
        var tools = new DownloadTools(yt, root, cookies);
        var runner = new ScriptedYtDlp
        {
            Handler = (args, _) =>
            {
                if (args[0].Contains("@bad", StringComparison.Ordinal))
                {
                    return Task.FromResult(new YtDlpRunResult(1, false, null));
                }

                return Task.FromResult(new YtDlpRunResult(0, false, null));
            }
        };
        var multi = new MultiChannelDownloader(new SourceDownloader(runner));
        var channels = new[]
        {
            new ChannelDownloadRequest(Path.Combine(root, "bad"), "https://www.youtube.com/@bad", 1, 1, 1, "en", "144"),
            new ChannelDownloadRequest(Path.Combine(root, "ok"), "https://www.youtube.com/@ok", 4, 6, 1, "de", "144")
        };

        try
        {
            var results = await multi.DownloadAllAsync(tools, channels, parallel: 2, log: null, CancellationToken.None);

            Assert.Equal(DownloadStop.Failed, results[0].Stop);
            Assert.Equal(DownloadStop.Completed, results[1].Stop);
            Assert.Contains(runner.Calls, args => args.Contains("de") && args.Contains("--sub-langs"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingFiles_ContinueAfterLastNumberWithoutRepeating()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vat-resume-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(folder, "source");
        var text = Path.Combine(folder, "text");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(text);
        try
        {
            for (var number = 1; number <= 49; number++)
            {
                File.WriteAllText(Path.Combine(source, $"{number:000} title.mp4"), "v");
                File.WriteAllText(Path.Combine(text, $"{number:000} title.en.srt"), "s");
            }

            File.WriteAllText(Path.Combine(source, "040 title.mp4.part"), "partial");
            var request = new ChannelDownloadRequest(folder, "https://www.youtube.com/@Chosen", 1, 50, 1, "en", "144");

            var videos = DownloadResume.MissingVideos(source, request);
            var subs = DownloadResume.MissingSubtitles(text, request);

            var only = Assert.Single(videos);
            Assert.Equal(new DownloadSlice(50, 50, 50), only);
            Assert.Equal(only, Assert.Single(subs));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void MissingFiles_FillsGapsAndSkipsEachSideSeparately()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vat-resume-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(folder, "source");
        var text = Path.Combine(folder, "text");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(text);
        try
        {
            foreach (var number in new[] { 1, 2, 3, 5 })
            {
                File.WriteAllText(Path.Combine(source, $"{number:000} title.mp4"), "v");
            }

            File.WriteAllText(Path.Combine(text, "001 title.en.srt"), "s");
            var request = new ChannelDownloadRequest(folder, "https://www.youtube.com/@Chosen", 10, 14, 1, "en", "144");

            Assert.Equal([new DownloadSlice(13, 13, 4)], DownloadResume.MissingVideos(source, request));
            Assert.Equal(
                [new DownloadSlice(11, 14, 2)],
                DownloadResume.MissingSubtitles(text, request));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task ExistingFiles_AreNotRequestedAgain()
    {
        var root = Path.Combine(Path.GetTempPath(), "vat-dl-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var text = Path.Combine(root, "text");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(text);
        var yt = Path.Combine(root, "yt-dlp.exe");
        var cookies = Path.Combine(root, "cookies.txt");
        File.WriteAllText(yt, "");
        File.WriteAllText(cookies, "");
        File.WriteAllText(Path.Combine(source, "001 title.mp4"), "v");
        File.WriteAllText(Path.Combine(text, "001 title.en.srt"), "s");
        File.WriteAllText(Path.Combine(text, "002 title.en.srt"), "s");
        var tools = new DownloadTools(yt, root, cookies);
        var runner = new ScriptedYtDlp
        {
            Handler = (_, _) => Task.FromResult(new YtDlpRunResult(0, false, null))
        };

        try
        {
            var result = await new SourceDownloader(runner).DownloadAsync(
                tools,
                new ChannelDownloadRequest(root, "https://www.youtube.com/@Chosen", 1, 2, 1, "en", "144"),
                log: null,
                CancellationToken.None);

            Assert.Equal(DownloadStop.Completed, result.Stop);
            Assert.Contains(runner.Calls, args => args.Contains("--format") && After(args, "--playlist-start") == "2" && After(args, "--autonumber-start") == "2");
            Assert.DoesNotContain(runner.Calls, args => args.Contains("--sub-langs"));
            Assert.DoesNotContain(runner.Calls, args => args.Contains("--format") && After(args, "--playlist-start") == "1");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task VideoError_StopsLaterVideos_ButNotSubtitles()
    {
        var (root, tools) = NewTools();
        File.WriteAllText(Path.Combine(root, "source", "002 title.mp4"), "v");
        var third = false;
        var runner = new ScriptedYtDlp
        {
            Handler = (args, _) =>
            {
                if (args.Contains("--print"))
                {
                    return Task.FromResult(new YtDlpRunResult(0, false, null));
                }

                if (args.Contains("--format") && After(args, "--playlist-start") == "3")
                {
                    third = true;
                }

                if (args.Contains("--format") && After(args, "--playlist-start") == "1")
                {
                    return Task.FromResult(new YtDlpRunResult(1, false, null));
                }

                return Task.FromResult(new YtDlpRunResult(0, false, null));
            }
        };

        try
        {
            var result = await new SourceDownloader(runner).DownloadAsync(
                tools,
                new ChannelDownloadRequest(root, "https://www.youtube.com/@Chosen", 1, 3, 1, "en", "144"),
                log: null,
                CancellationToken.None);

            Assert.Equal(DownloadStop.Failed, result.Stop);
            Assert.Contains("Video", result.Detail);
            Assert.False(third);
            Assert.Contains(runner.Calls, args => args.Contains("--sub-langs"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SubtitleError_StopsLaterSubtitles_ButNotVideos()
    {
        var (root, tools) = NewTools();
        File.WriteAllText(Path.Combine(root, "source", "001 title.mp4"), "v");
        File.WriteAllText(Path.Combine(root, "source", "002 title.mp4"), "v");
        File.WriteAllText(Path.Combine(root, "source", "003 title.mp4"), "v");
        File.WriteAllText(Path.Combine(root, "text", "002 title.en.srt"), "s");
        var third = false;
        var runner = new ScriptedYtDlp
        {
            Handler = (args, _) =>
            {
                if (args.Contains("--print"))
                {
                    return Task.FromResult(new YtDlpRunResult(0, false, null));
                }

                if (args.Contains("--sub-langs") && After(args, "--playlist-start") == "3")
                {
                    third = true;
                }

                if (args.Contains("--sub-langs") && After(args, "--playlist-start") == "1")
                {
                    return Task.FromResult(new YtDlpRunResult(1, false, null));
                }

                return Task.FromResult(new YtDlpRunResult(0, false, null));
            }
        };

        try
        {
            var result = await new SourceDownloader(runner).DownloadAsync(
                tools,
                new ChannelDownloadRequest(root, "https://www.youtube.com/@Chosen", 1, 3, 1, "en", "144"),
                log: null,
                CancellationToken.None);

            Assert.Equal(DownloadStop.Failed, result.Stop);
            Assert.Contains("Phụ đề", result.Detail);
            Assert.False(third);
            Assert.DoesNotContain(runner.Calls, args => args.Contains("--format"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ActivityLog_ShowsFileAndSpeedWithoutYtDlpDetail()
    {
        var notices = new List<DownloadNotice>();
        IProgress<DownloadNotice> progress = new ListProgress(notices);
        var log = new DownloadActivityLog(progress, "Video", @"D:\RV144");

        log.OnLine("ERROR: unable to download video data");
        log.OnLine(@"[download] Destination: D:\RV144\source\001 Title.f137.mp4.part");
        log.OnLine("[download]  10.0% of 10.00MiB at  1.50MiB/s ETA 00:10");
        log.OnLine(@"[Merger] Merging formats into ""D:\RV144\source\001 Title.mp4""");
        log.OnLine(@"[download] Destination: D:\RV144\source\002 Other.mp4");

        var keys = notices.Select(notice => notice.ReplaceKey).Distinct().ToList();
        Assert.Equal(2, keys.Count);
        Assert.Equal("001", notices[0].FileNumber);
        Assert.Equal("Video", notices[0].Kind);
        Assert.Equal("RV144  Video: 001 Title.mp4  1.50MiB/s", notices.Last(notice => notice.ReplaceKey == keys[0]).Text);
        Assert.Equal("RV144  Video: 002 Other.mp4", notices.Last(notice => notice.ReplaceKey == keys[1]).Text);
        Assert.DoesNotContain(notices, notice => notice.Text.Contains("ERROR", StringComparison.Ordinal));
    }

    [Fact]
    public void ActivityLog_SubtitleJson_ShowsSrtName()
    {
        var notices = new List<DownloadNotice>();
        var log = new DownloadActivityLog(new ListProgress(notices), "Text", @"D:\RV144");
        log.OnLine(@"[info] Writing video subtitles to: D:\RV144\text\001 Title.en.json3");

        Assert.Equal("RV144  Text: 001 Title.en.srt", Assert.Single(notices).Text);
    }

    [Fact]
    public async Task VideoAndSubtitles_DownloadTogether()
    {
        var root = Path.Combine(Path.GetTempPath(), "vat-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var yt = Path.Combine(root, "yt-dlp.exe");
        var cookies = Path.Combine(root, "cookies.txt");
        File.WriteAllText(yt, "");
        File.WriteAllText(cookies, "");
        var tools = new DownloadTools(yt, root, cookies);
        var videoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ScriptedYtDlp
        {
            Handler = async (args, token) =>
            {
                if (args.Contains("--print"))
                {
                    return new YtDlpRunResult(0, false, null);
                }

                if (args.Contains("--format"))
                {
                    videoStarted.TrySetResult();
                    await subsStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), token);
                    return new YtDlpRunResult(0, false, null);
                }

                if (args.Contains("--sub-langs"))
                {
                    subsStarted.TrySetResult();
                    await videoStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), token);
                    return new YtDlpRunResult(0, false, null);
                }

                return new YtDlpRunResult(0, false, null);
            }
        };

        try
        {
            var result = await new SourceDownloader(runner).DownloadAsync(
                tools,
                new ChannelDownloadRequest(root, "https://www.youtube.com/@together", 1, 2, 1, "en", "360"),
                log: null,
                CancellationToken.None);

            Assert.Equal(DownloadStop.Completed, result.Stop);
            Assert.Contains(runner.Calls, args => args.Contains(VideoQuality.Format("360")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (string Root, DownloadTools Tools) NewTools()
    {
        var root = Path.Combine(Path.GetTempPath(), "vat-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "source"));
        Directory.CreateDirectory(Path.Combine(root, "text"));
        var yt = Path.Combine(root, "yt-dlp.exe");
        var cookies = Path.Combine(root, "cookies.txt");
        File.WriteAllText(yt, "");
        File.WriteAllText(cookies, "");
        return (root, new DownloadTools(yt, root, cookies));
    }

    private static string After(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return "";
    }

    private sealed class ListProgress(List<DownloadNotice> notices) : IProgress<DownloadNotice>
    {
        public void Report(DownloadNotice value) => notices.Add(value);
    }

    private sealed class ScriptedYtDlp : IYtDlpRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Func<IReadOnlyList<string>, CancellationToken, Task<YtDlpRunResult>>? Handler { get; init; }

        public Task<YtDlpRunResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            IProgress<string>? log,
            CancellationToken cancellationToken)
        {
            Calls.Add(arguments);
            return Handler?.Invoke(arguments, cancellationToken)
                ?? Task.FromResult(new YtDlpRunResult(0, false, null));
        }
    }
}
