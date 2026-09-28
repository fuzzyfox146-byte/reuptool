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

        Assert.Contains("--playlist-items", video);
        Assert.Contains("3-9", video);
        Assert.Contains("--autonumber-start", video);
        Assert.Contains("!is_live", video);
        Assert.Contains(YtDlpPlan.OutputTemplate, video);
        Assert.Contains("--no-overwrites", video);
        Assert.Contains("--no-overwrites", subs);
        Assert.DoesNotContain(video, arg => arg.Contains("downloaded_videos.txt", StringComparison.Ordinal));
        Assert.Contains("de", subs);
        Assert.Contains("--newline", video);
        Assert.Contains(VideoQuality.Format("720"), video);
        Assert.Contains("--write-thumbnail", video);
        Assert.Contains("jpg", video);
        Assert.Contains(video, arg => arg.StartsWith("thumbnail:", StringComparison.Ordinal));
        Assert.DoesNotContain("--no-write-thumbnail", video);
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
        var multi = new MultiChannelDownloader(FastDownloader(runner));
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
        var multi = new MultiChannelDownloader(FastDownloader(runner));
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
            var subs = DownloadResume.MissingSubtitles(text, source, request);

            var only = Assert.Single(videos);
            Assert.Equal(new DownloadSlice(50, 50, 50), only);
            Assert.Equal(only, Assert.Single(subs));
            File.WriteAllText(Path.Combine(text, "050 title.en.json3"), "{}");
            Assert.Empty(DownloadResume.MissingSubtitles(text, source, request));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void MissingFiles_ContinuesAfterHighestNumberAndLeavesHoles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vat-resume-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(folder, "source");
        var text = Path.Combine(folder, "text");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(text);
        try
        {
            foreach (var number in new[] { 1, 2, 5 })
            {
                File.WriteAllText(Path.Combine(source, $"{number:000} title.mp4"), "v");
            }

            File.WriteAllText(Path.Combine(text, "001 title.en.srt"), "s");
            File.WriteAllText(Path.Combine(text, "005 title.en.srt"), "s");
            Directory.CreateDirectory(Path.Combine(folder, "thum"));
            File.WriteAllText(Path.Combine(folder, "thum", "001 title.jpg"), "t");
            File.WriteAllText(Path.Combine(folder, "thum", "005 title.jpg"), "t");
            File.WriteAllText(Path.Combine(source, "999 stray.mp4"), "v");
            var request = new ChannelDownloadRequest(folder, "https://www.youtube.com/@Chosen", 1, 10, 1, "en", "144");

            Assert.Equal([new DownloadSlice(6, 10, 6)], DownloadResume.MissingVideos(source, request));
            Assert.Equal(
                [new DownloadSlice(2, 2, 2), new DownloadSlice(6, 10, 6)],
                DownloadResume.MissingSubtitles(text, source, request));
            Assert.Equal(
                [new DownloadSlice(2, 2, 2), new DownloadSlice(6, 10, 6)],
                DownloadResume.MissingThumbnails(Path.Combine(folder, "thum"), source, request));
            Assert.Equal(
                [
                    new DownloadSlice(2, 2, 2),
                    new DownloadSlice(6, 6, 6),
                    new DownloadSlice(7, 7, 7)
                ],
                DownloadResume.OneItemEach(DownloadResume.MissingSubtitles(text, source, request)).Take(3));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void MismatchedSideFile_IsMissingAndDeleted()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vat-pair-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(folder, "source");
        var text = Path.Combine(folder, "text");
        var thum = Path.Combine(folder, "thum");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(text);
        Directory.CreateDirectory(thum);
        try
        {
            File.WriteAllText(Path.Combine(source, "001 Why Didnt You Tell Anyone.mp4"), "v");
            File.WriteAllText(Path.Combine(text, "001 Other Title.en.srt"), "s");
            File.WriteAllText(Path.Combine(thum, "001 Newest Upload.jpg"), "t");
            var request = new ChannelDownloadRequest(folder, "https://www.youtube.com/@Chosen", 1, 1, 1, "en", "144");

            Assert.Equal(
                [new DownloadSlice(1, 1, 1)],
                DownloadResume.MissingSubtitles(text, source, request));
            Assert.Equal(
                [new DownloadSlice(1, 1, 1)],
                DownloadResume.MissingThumbnails(thum, source, request));
            Assert.Equal(1, DownloadResume.RemoveMismatched(text, source, request, DownloadResume.IsSubtitleFile));
            Assert.Equal(1, DownloadResume.RemoveMismatched(thum, source, request, DownloadResume.IsThumbnailFile));
            Assert.False(File.Exists(Path.Combine(text, "001 Other Title.en.srt")));
            Assert.False(File.Exists(Path.Combine(thum, "001 Newest Upload.jpg")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TitleKey_IgnoresNumberAndLanguageSuffix()
    {
        Assert.True(DownloadTitleKey.Aligns(
            "001 Why Didn't You Tell Anyone.mp4",
            "Why Didn't You Tell Anyone"));
        Assert.True(DownloadTitleKey.Aligns(
            "002 Why Didn't You Tell Anyone.en.srt",
            "001 Why Didn't You Tell Anyone.mp4"));
        Assert.False(DownloadTitleKey.Aligns(
            "001 Why Didn't You Tell Anyone.mp4",
            "001 Newest Upload On Channel.mp4"));
    }

    [Fact]
    public void Catalog_FindsWatchUrlByTitleNotPlaylistIndex()
    {
        var entries = PlaylistCatalog.Parse(
        [
            "newvideo001\t1\tNewest Upload On Channel",
            "oldvideo001\t6\tWhy Didn't You Tell Anyone"
        ]);
        var found = PlaylistCatalog.Find("001 Why Didn't You Tell Anyone.mp4", 1, entries);

        Assert.Equal("oldvideo001", found?.Id);
        Assert.Equal("https://www.youtube.com/watch?v=oldvideo001", PlaylistCatalog.WatchUrl(found!.Id));
        Assert.DoesNotContain("--playlist-end", YtDlpPlan.Catalog(
            new DownloadTools(@"D:\tools\yt-dlp.exe", @"D:\tools\bin", @"D:\tools\cookies.txt"),
            new ChannelDownloadRequest(@"D:\RV144", "https://www.youtube.com/@Chosen", 1, 9, 1, "en", "144")));
    }

    [Fact]
    public void SubtitlePlan_ForExistingVideo_UsesWatchUrl()
    {
        var tools = new DownloadTools(@"D:\tools\yt-dlp.exe", @"D:\tools\bin", @"D:\tools\cookies.txt");
        var request = new ChannelDownloadRequest(@"D:\RV144", "https://www.youtube.com/@Chosen", 1, 9, 1, "en", "144");
        var slice = new DownloadSlice(1, 1, 1);
        var args = YtDlpPlan.Subtitles(tools, request, slice, "https://www.youtube.com/watch?v=oldvideo001");

        Assert.Equal("https://www.youtube.com/watch?v=oldvideo001", args[0]);
        Assert.Contains("001 %(title).100s.%(ext)s", args);
        Assert.DoesNotContain("--playlist-items", args);
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
            var result = await FastDownloader(runner).DownloadAsync(
                tools,
                new ChannelDownloadRequest(root, "https://www.youtube.com/@Chosen", 1, 2, 1, "en", "144"),
                log: null,
                CancellationToken.None);

            Assert.Equal(DownloadStop.Completed, result.Stop);
            Assert.Contains(runner.Calls, args => args.Contains("--format") && After(args, "--playlist-items") == "2" && After(args, "--autonumber-start") == "2");
            Assert.DoesNotContain(runner.Calls, args => args.Contains("--sub-langs"));
            Assert.DoesNotContain(runner.Calls, args => args.Contains("--format") && After(args, "--playlist-items") == "1");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task VideoError_ContinuesLaterVideos_AndStillDownloadsSubtitles()
    {
        var (root, tools) = NewTools();
        var third = false;
        var runner = new ScriptedYtDlp
        {
            Handler = (args, _) =>
            {
                if (args.Contains("--print"))
                {
                    return Task.FromResult(new YtDlpRunResult(0, false, null));
                }

                if (args.Contains("--format") && After(args, "--playlist-items") == "3")
                {
                    third = true;
                }

                if (args.Contains("--format") && After(args, "--playlist-items") == "1")
                {
                    return Task.FromResult(new YtDlpRunResult(1, false, null));
                }

                return Task.FromResult(new YtDlpRunResult(0, false, null));
            }
        };

        try
        {
            var result = await FastDownloader(runner).DownloadAsync(
                tools,
                new ChannelDownloadRequest(root, "https://www.youtube.com/@Chosen", 1, 3, 1, "en", "144"),
                log: null,
                CancellationToken.None);

            Assert.Equal(DownloadStop.Failed, result.Stop);
            Assert.Contains("Video", result.Detail);
            Assert.True(third);
            Assert.Contains(runner.Calls, args => args.Contains("--sub-langs"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SubtitleError_ContinuesLaterSubtitles_AndDoesNotRedownloadVideos()
    {
        var (root, tools) = NewTools();
        File.WriteAllText(Path.Combine(root, "source", "001 title.mp4"), "v");
        File.WriteAllText(Path.Combine(root, "source", "002 title.mp4"), "v");
        File.WriteAllText(Path.Combine(root, "source", "003 title.mp4"), "v");
        File.WriteAllText(Path.Combine(root, "text", "001 title.en.srt"), "s");
        var third = false;
        var runner = new ScriptedYtDlp
        {
            Handler = (args, _) =>
            {
                if (args.Contains("--print"))
                {
                    return Task.FromResult(new YtDlpRunResult(0, false, null));
                }

                if (args.Contains("--sub-langs") && After(args, "--autonumber-start") == "3")
                {
                    third = true;
                }

                if (args.Contains("--sub-langs") && After(args, "--autonumber-start") == "2")
                {
                    return Task.FromResult(new YtDlpRunResult(1, false, null));
                }

                return Task.FromResult(new YtDlpRunResult(0, false, null));
            }
        };

        try
        {
            var result = await FastDownloader(runner).DownloadAsync(
                tools,
                new ChannelDownloadRequest(root, "https://www.youtube.com/@Chosen", 1, 3, 1, "en", "144"),
                log: null,
                CancellationToken.None);

            Assert.Equal(DownloadStop.Failed, result.Stop);
            Assert.Contains("Phụ đề", result.Detail);
            Assert.True(third);
            Assert.DoesNotContain(runner.Calls, args => args.Contains("--format"));
            Assert.Contains(
                runner.Calls,
                args => args.Contains("--sub-langs") && args[0].Contains("watch?v=id000000002", StringComparison.Ordinal));
            Assert.DoesNotContain(
                runner.Calls,
                args => args.Contains("--sub-langs") && args.Contains("--playlist-items"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SubtitleMissingFile_RetriesSameNumberUntilWritten()
    {
        var (root, tools) = NewTools();
        var tries = 0;
        var runner = new ScriptedYtDlp
        {
            Handler = (args, _) =>
            {
                if (args.Contains("--print"))
                {
                    return Task.FromResult(new YtDlpRunResult(0, false, null));
                }

                if (!args.Contains("--sub-langs") || After(args, "--autonumber-start") != "2")
                {
                    return Task.FromResult(new YtDlpRunResult(0, false, null));
                }

                tries++;
                if (tries >= 3)
                {
                    File.WriteAllText(Path.Combine(root, "text", "002 title.en.srt"), "s");
                }

                return Task.FromResult(new YtDlpRunResult(tries < 3 ? 1 : 0, false, null));
            }
        };

        try
        {
            File.WriteAllText(Path.Combine(root, "source", "001 title.mp4"), "v");
            File.WriteAllText(Path.Combine(root, "source", "002 title.mp4"), "v");
            File.WriteAllText(Path.Combine(root, "text", "001 title.en.srt"), "s");
            Directory.CreateDirectory(Path.Combine(root, "thum"));
            File.WriteAllText(Path.Combine(root, "thum", "001 title.jpg"), "t");
            File.WriteAllText(Path.Combine(root, "thum", "002 title.jpg"), "t");
            var result = await new SourceDownloader(runner, TimeSpan.Zero, TimeSpan.Zero, 4).DownloadAsync(
                tools,
                new ChannelDownloadRequest(root, "https://www.youtube.com/@Chosen", 1, 2, 1, "en", "144"),
                log: null,
                CancellationToken.None);

            Assert.Equal(DownloadStop.Completed, result.Stop);
            Assert.Equal(3, tries);
            Assert.True(File.Exists(Path.Combine(root, "text", "002 title.en.srt")));
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
    public void ActivityLog_IgnoresThumbnailImages()
    {
        var notices = new List<DownloadNotice>();
        var log = new DownloadActivityLog(new ListProgress(notices), "Video", @"D:\RV144");
        log.OnLine(@"[download] Destination: D:\RV144\thum\001 ChannelLogo.jpg");
        log.OnLine(@"[download] Destination: D:\RV144\source\001 Title.mp4");

        Assert.Equal("RV144  Video: 001 Title.mp4", Assert.Single(notices).Text);
    }

    [Fact]
    public void ActivityLog_ThumbKind_ShowsJpg()
    {
        var notices = new List<DownloadNotice>();
        var log = new DownloadActivityLog(new ListProgress(notices), "Thumb", @"D:\RV144");
        log.OnLine(@"[info] Writing video thumbnail 41 to: D:\RV144\thum\001 Title.jpg");

        Assert.Equal("RV144  Thumb: 001 Title.jpg", Assert.Single(notices).Text);
        Assert.Equal("001", notices[0].FileNumber);
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
            var result = await FastDownloader(runner).DownloadAsync(
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

    private static SourceDownloader FastDownloader(IYtDlpRunner runner) =>
        new(new WriteSubtitleStub(runner), TimeSpan.Zero, TimeSpan.Zero, 1);

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

    private sealed class WriteSubtitleStub(IYtDlpRunner inner) : IYtDlpRunner
    {
        public Task<YtDlpRunResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            IProgress<string>? log,
            CancellationToken cancellationToken) =>
            WriteAfterAsync(inner, executable, arguments, log, cancellationToken);

        private static async Task<YtDlpRunResult> WriteAfterAsync(
            IYtDlpRunner inner,
            string executable,
            IReadOnlyList<string> arguments,
            IProgress<string>? log,
            CancellationToken cancellationToken)
        {
            var result = await inner.RunAsync(executable, arguments, log, cancellationToken);
            if (result.ExitCode != 0)
            {
                return result;
            }

            var numberText = After(arguments, "--autonumber-start");
            if (!int.TryParse(numberText, out var number))
            {
                return result;
            }

            if (arguments.Contains("--sub-langs"))
            {
                WriteSidecar(
                    arguments,
                    "subtitle:",
                    number,
                    $"{number:000} title.en.srt",
                    DownloadResume.IsSubtitleFile);
            }

            if (arguments.Contains("--write-thumbnail"))
            {
                WriteSidecar(
                    arguments,
                    "thumbnail:",
                    number,
                    $"{number:000} title.jpg",
                    DownloadResume.IsThumbnailFile);
            }

            return result;
        }

        private static void WriteSidecar(
            IReadOnlyList<string> arguments,
            string prefix,
            int number,
            string fileName,
            Func<string, bool> isCompleted)
        {
            var folder = arguments.Select(Prefix(prefix)).FirstOrDefault(path => path.Length > 0) ?? "";
            if (folder.Length == 0)
            {
                return;
            }

            Directory.CreateDirectory(folder);
            if (!DownloadResume.HasCompleted(folder, number, isCompleted))
            {
                File.WriteAllText(Path.Combine(folder, fileName), "s");
            }
        }

        private static Func<string, string> Prefix(string prefix) =>
            value => value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : "";
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
            if (arguments.Contains("--flat-playlist"))
            {
                var end = 50;
                var endText = After(arguments, "--playlist-end");
                if (int.TryParse(endText, out var parsed) && parsed > 0)
                {
                    end = parsed;
                }

                for (var i = 1; i <= end; i++)
                {
                    log?.Report($"id{i.ToString().PadLeft(9, '0')}\t{i}\ttitle");
                }
            }

            return Handler?.Invoke(arguments, cancellationToken)
                ?? Task.FromResult(new YtDlpRunResult(0, false, null));
        }
    }
}
