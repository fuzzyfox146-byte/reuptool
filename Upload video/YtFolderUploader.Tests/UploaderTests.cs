using System.Text;

namespace YtFolderUploader.Tests;

public class UploaderTests
{
    [Fact]
    public void Title_StripsLeadingIndex_AndKeepsLaterNumbers()
    {
        var (title, trimmed) = FileNames.MakeTitle("007 75年前の事件.jpg", stripKhung: false);
        Assert.False(trimmed);
        Assert.Equal("75年前の事件", title);
    }

    [Fact]
    public void Title_StripsKhungSuffix_AndNormalizesDecomposedKana()
    {
        var decomposed = "001 【1923→2026】103年前の水路未解決事件" + "\u304B\u3099" + "動き出そうとしています_khung_01.mp4";
        var (title, _) = FileNames.MakeTitle(decomposed, stripKhung: true);
        Assert.Equal("【1923→2026】103年前の水路未解決事件が動き出そうとしています", title);
        Assert.DoesNotContain("_khung_", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Title_CutsAt100Runes()
    {
        var (title, trimmed) = FileNames.MakeTitle("001 " + new string('あ', 120) + ".jpg", stripKhung: false);
        Assert.True(trimmed);
        Assert.Equal(100, title.EnumerateRunes().Count());
    }

    [Fact]
    public void Index_ParsesZeroPaddedNumber()
    {
        Assert.True(FileNames.TryGetIndex("010 hello world.mp4", out var number, out var raw));
        Assert.Equal(10, number);
        Assert.Equal("010", raw);
    }

    [Fact]
    public void StartIndex_SkipsLowerNumbers_AndKeepsTheBoundary()
    {
        Assert.True(FileNames.IsBeforeStart("049", 50));
        Assert.False(FileNames.IsBeforeStart("050", 50));
        Assert.False(FileNames.IsBeforeStart("051", 50));
        Assert.False(FileNames.IsBeforeStart("—", 50));
        Assert.False(FileNames.IsBeforeStart("001", 0));
    }

    [Fact]
    public void Schedule_SpreadsVideosAcrossDays_AndSkipsNearSlots()
    {
        var options = UploadOptions.CreateDefault();
        options.ExactDailyTimes = false;
        options.VideosPerDay = 2;
        options.TimeSlotsText = "09:00\n21:00";
        options.ScheduleStartDate = new DateOnly(2026, 10, 14);
        options.TimeZoneId = "SE Asia Standard Time";

        var now = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.FromHours(7));
        var plan = SchedulePlanner.Plan(5, options, [], now);
        Assert.Equal(5, plan.Times.Count);
        Assert.Equal(9, Local(plan.Times[0]).Hour);
        Assert.Equal(new DateOnly(2026, 10, 14), DateOnly.FromDateTime(Local(plan.Times[0]).DateTime));
        Assert.Equal(21, Local(plan.Times[1]).Hour);

        var perDay = plan.Times.GroupBy(time => DateOnly.FromDateTime(Local(time).DateTime));
        Assert.Equal(3, perDay.Count());
        Assert.All(perDay, group => Assert.True(group.Count() <= 2));

        var soon = new DateTimeOffset(2026, 10, 14, 8, 50, 0, TimeSpan.FromHours(7));
        var skipped = SchedulePlanner.Plan(1, options, [], soon);
        Assert.Equal(new DateOnly(2026, 10, 15), DateOnly.FromDateTime(Local(skipped.Times[0]).DateTime));
        Assert.Equal(9, Local(skipped.Times[0]).Hour);

        var used = new[] { new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.FromHours(7)) };
        var midnight = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.FromHours(7));
        var blocked = SchedulePlanner.Plan(1, options, used, midnight);
        Assert.Equal(new DateOnly(2026, 10, 15), DateOnly.FromDateTime(Local(blocked.Times[0]).DateTime));
        Assert.Equal(9, Local(blocked.Times[0]).Hour);
    }

    [Fact]
    public void Schedule_QueueStartsTomorrow_NotLeftoverToday()
    {
        var options = UploadOptions.CreateDefault();
        options.ExactDailyTimes = false;
        options.VideosPerDay = 5;
        options.TimeSlotsText = "06:00\n10:00\n15:00\n18:00\n21:00";
        options.ScheduleStartDate = new DateOnly(2026, 10, 13);
        options.TimeZoneId = "SE Asia Standard Time";
        var now = new DateTimeOffset(2026, 10, 14, 15, 18, 0, TimeSpan.FromHours(7));

        var plan = SchedulePlanner.Plan(6, options, [], now);

        Assert.Equal(6, plan.Times.Count);
        Assert.Equal(new[] { 6, 10, 15, 18, 21 }, plan.Times.Take(5).Select(time => Local(time).Hour).ToArray());
        Assert.All(plan.Times.Take(5), time => Assert.Equal(new DateOnly(2026, 10, 15), DateOnly.FromDateTime(Local(time).DateTime)));
        Assert.Equal(new DateOnly(2026, 10, 16), DateOnly.FromDateTime(Local(plan.Times[5]).DateTime));
        Assert.Equal(6, Local(plan.Times[5]).Hour);
    }

    [Fact]
    public void Schedule_DeferMovesThePublicDay()
    {
        var options = UploadOptions.CreateDefault();
        options.ExactDailyTimes = true;
        options.VideosPerDay = 2;
        options.TimeSlotsText = "08:30\n21:00";
        options.ScheduleStartDate = new DateOnly(2026, 10, 13);
        options.TimeZoneId = "SE Asia Standard Time";
        var now = new DateTimeOffset(2026, 10, 15, 16, 0, 0, TimeSpan.FromHours(7));

        var plan = SchedulePlanner.Plan(2, options, [], now, new DateOnly(2026, 10, 17));

        Assert.Equal(new DateOnly(2026, 10, 17), plan.TargetDay);
        Assert.All(plan.Times, time => Assert.Equal(new DateOnly(2026, 10, 17), DateOnly.FromDateTime(Local(time).DateTime)));
        Assert.Equal(new[] { 8, 21 }, plan.Times.Select(time => Local(time).Hour).ToArray());
    }

    [Fact]
    public void ThumbFailure_DefersUnlessTheImageOrTheLoginIsWrong()
    {
        Assert.True(ApiErrors.ShouldDeferAfterThumbFailure(new InvalidOperationException("quotaExceeded")));
        Assert.True(ApiErrors.ShouldDeferAfterThumbFailure(new InvalidOperationException("uploadLimitExceeded")));
        Assert.False(ApiErrors.ShouldDeferAfterThumbFailure(new OperationCanceledException()));
        Assert.False(ApiErrors.ShouldDeferAfterThumbFailure(new FileNotFoundException("Không thấy file thumbnail.")));
    }

    [Fact]
    public void ScheduleDefer_KeepsTheClockOnTheNextDay()
    {
        var when = new DateTimeOffset(2026, 10, 16, 8, 30, 0, TimeSpan.FromHours(7));
        var next = ScheduleDefer.AddOneDay(when, "SE Asia Standard Time");
        var local = next.ToOffset(TimeSpan.FromHours(7));

        Assert.Equal(new DateOnly(2026, 10, 17), DateOnly.FromDateTime(local.DateTime));
        Assert.Equal(8, local.Hour);
        Assert.Equal(30, local.Minute);
        Assert.Equal(new DateOnly(2026, 10, 17), ScheduleDefer.NextPublicDay(when, "SE Asia Standard Time", when));
    }

    [Fact]
    public void PermanentDelete_RemovesFileAndLeavesNoRecycleCopy()
    {
        var folder = Path.Combine(Path.GetTempPath(), "yt-delete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "001 video.mp4");
        File.WriteAllBytes(file, [1, 2, 3]);
        File.SetAttributes(file, FileAttributes.ReadOnly);

        try
        {
            var thumb = Path.Combine(folder, "001 video.jpg");
            File.WriteAllBytes(thumb, [4, 5]);
            Assert.True(SourceDeleter.TryDeletePermanent(file, out var error), error);
            Assert.True(SourceDeleter.TryDeletePermanent(thumb, out var thumbError), thumbError);
            Assert.False(File.Exists(file));
            Assert.False(File.Exists(thumb));
            Assert.True(SourceDeleter.TryDeletePermanent(file, out _));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void OneDayAhead_UsesTomorrowOnly_AndStopsWhenShort()
    {
        var options = UploadOptions.CreateDefault();
        options.ExactDailyTimes = true;
        options.VideosPerDay = 5;
        options.TimeSlotsText = "05:00\n10:00\n15:00\n18:00\n20:00";
        options.ScheduleStartDate = new DateOnly(2026, 10, 13);
        options.TimeZoneId = "SE Asia Standard Time";
        var now = new DateTimeOffset(2026, 10, 14, 8, 45, 0, TimeSpan.FromHours(7));

        var plan = SchedulePlanner.Plan(120, options, [], now);
        Assert.Equal(new DateOnly(2026, 10, 15), plan.TargetDay);
        Assert.Equal(5, plan.Times.Count);
        Assert.False(plan.ShortSource);
        Assert.Equal(new[] { 5, 10, 15, 18, 20 }, plan.Times.Select(time => Local(time).Hour).ToArray());

        options.VideosPerDay = 3;
        var fewer = SchedulePlanner.Plan(120, options, [], now);
        Assert.Equal(new[] { 5, 10, 15 }, fewer.Times.Select(time => Local(time).Hour).ToArray());
        options.VideosPerDay = 6;
        var capped = SchedulePlanner.Plan(120, options, [], now);
        Assert.Equal(5, capped.Times.Count);
        options.VideosPerDay = 5;
        Assert.All(plan.Times, time => Assert.Equal(new DateOnly(2026, 10, 15), DateOnly.FromDateTime(Local(time).DateTime)));

        var shortPlan = SchedulePlanner.Plan(3, options, [], now);
        Assert.Empty(shortPlan.Times);
        Assert.True(shortPlan.ShortSource);

        var used = plan.Times.Take(1).ToArray();
        var already = SchedulePlanner.Plan(120, options, used, now);
        Assert.True(already.DayAlreadySet);
        Assert.Empty(already.Times);
        Assert.False(already.ShortSource);
        Assert.True(SchedulePlanner.HasUploadOnTargetDay(options, used, now));
        var laterDay = new DateTimeOffset(2026, 10, 16, 10, 0, 0, TimeSpan.FromHours(7));
        Assert.False(SchedulePlanner.HasUploadOnTargetDay(options, [laterDay], now));
    }

    [Fact]
    public void CredentialParser_ReadsDesktopJson_AndPlainLines()
    {
        var json = """
            {"installed":{"client_id":"abc.apps.googleusercontent.com","client_secret":"sec","redirect_uris":["http://localhost"]}}
            """;
        var fromJson = CredentialParser.Parse(json);
        Assert.Equal("abc.apps.googleusercontent.com", fromJson.ClientId);
        Assert.Equal("sec", fromJson.ClientSecret);

        var lines = "client_id=abc.apps.googleusercontent.com\nclient_secret=sec\nrefresh_token=1//token";
        var fromLines = CredentialParser.Parse(lines);
        Assert.Equal("1//token", fromLines.RefreshToken);

        var portable = CredentialParser.ToPortableJson(fromLines, "UCtest", "Kênh thử");
        var roundTrip = CredentialParser.Parse(portable);
        Assert.Equal("UCtest", roundTrip.ChannelId);
        Assert.Equal("Kênh thử", roundTrip.ChannelTitle);
    }

    [Fact]
    public void TagsAndPlaylist_FollowYouTubeLimits()
    {
        var tags = MetadataComposer.Tags("một hai, " + new string('a', 400) + ", cuối");
        var cost = string.Join(",", tags.Select(tag => tag.Contains(' ') ? tag.Length + 2 : tag.Length));
        Assert.True(cost.Length <= 500 || tags.Count == 1);

        Assert.Equal(
            "PLabcdefghijklmnop",
            MetadataComposer.NormalizePlaylistId("https://www.youtube.com/watch?v=abc&list=PLabcdefghijklmnop"));
    }

    [Fact]
    public void SavedSourceWithFolder_TurnsOnOnce()
    {
        var data = new AppData
        {
            Channels =
            {
                new ChannelProfile
                {
                    Sources =
                    {
                        new VideoSource { Name = "HD036", VideoFolder = @"D:\HD036\xuat", Enabled = false },
                        new VideoSource { Name = "trống", Enabled = false }
                    }
                }
            }
        };

        Assert.True(SettingsStore.EnableSavedSources(data));
        Assert.True(data.Channels[0].Sources[0].Enabled);
        Assert.False(data.Channels[0].Sources[1].Enabled);
        Assert.Equal(1, data.SettingsRevision);

        data.Channels[0].Sources[0].Enabled = false;
        Assert.False(SettingsStore.EnableSavedSources(data));
        Assert.False(data.Channels[0].Sources[0].Enabled);
    }

    [Fact]
    public void RealHd036Folder_PairsThumbAndStripsIndex()
    {
        if (!Directory.Exists(@"D:\HD036\xuat") || !Directory.Exists(@"D:\HD036\thumb"))
            return;

        var result = FolderScanner.Scan(new VideoSource
        {
            Name = "HD036",
            VideoFolder = @"D:\HD036\xuat",
            ThumbFolder = @"D:\HD036\thumb",
            Enabled = true
        });

        Assert.True(result.Videos.Count > 50);
        var first = result.Videos.First(video => video.Index == "001");
        Assert.False(first.MissingThumb);
        Assert.StartsWith("【", first.Title);
        Assert.DoesNotContain("_khung_", first.Title, StringComparison.OrdinalIgnoreCase);
        Assert.True(first.Title.Length <= 100);
        Assert.All(result.Videos, video => Assert.True(video.Title.Length <= 100 && video.Title.Length > 0));
        Assert.Contains("001", Path.GetFileName(first.ThumbPath));

        var detected = new VideoSource();
        FolderScanner.ApplyRoot(detected, @"D:\HD036");
        Assert.Equal(@"D:\HD036\xuat", detected.VideoFolder, ignoreCase: true);
        Assert.Equal(@"D:\HD036\thumb", detected.ThumbFolder, ignoreCase: true);
    }

    [Fact]
    public void Template_SameSettingsReuseOne_DifferentKeyCreatesAnother()
    {
        var data = new AppData();
        var first = new ChannelProfile { Name = "A", Options = SampleOptions(5) };
        var second = new ChannelProfile { Name = "B", Options = SampleOptions(5) };
        data.Channels.Add(first);
        data.Channels.Add(second);

        Assert.True(UploadTemplates.EnsureMissing(data));
        Assert.Single(data.Templates);
        Assert.Equal(first.TemplateId, second.TemplateId);
        Assert.False(UploadTemplates.EnsureMissing(data));

        second.Options.VideosPerDay = 3;
        var created = UploadTemplates.Sync(data, second);
        Assert.True(created.Created);
        Assert.Equal(2, data.Templates.Count);
        Assert.NotEqual(first.TemplateId, second.TemplateId);
        Assert.Contains("3 video/ngày", created.Template.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_KeyIgnoresCategoryList_AndLineEndings()
    {
        var left = SampleOptions(5);
        left.TimeSlotsText = "05:00\r\n10:00";
        left.Categories = [new CategoryChoice { Id = "24", Title = "Giải trí" }];
        var right = SampleOptions(5);
        right.TimeSlotsText = "05:00\n10:00";
        right.Categories = [new CategoryChoice { Id = "22", Title = "Blog" }];

        Assert.Equal(UploadTemplates.KeyOf(left), UploadTemplates.KeyOf(right));
    }

    [Fact]
    public void Template_ApplyCopiesSettings_WithoutSharingTheObject()
    {
        var data = new AppData();
        var source = new ChannelProfile { Options = SampleOptions(5) };
        var sync = UploadTemplates.Sync(data, source);
        var target = new ChannelProfile { Options = SampleOptions(1) };

        UploadTemplates.Apply(target, sync.Template);
        target.Options.VideosPerDay = 9;

        Assert.Equal(sync.Template.Id, target.TemplateId);
        Assert.Equal(9, target.Options.VideosPerDay);
        Assert.Equal(5, sync.Template.Options.VideosPerDay);
        Assert.Equal("05:00", target.Options.TimeSlotsText.Split('\n')[0]);
    }

    [Fact]
    public void Template_SaveInto_SkipsTheChannelThatIsUploading()
    {
        var data = new AppData();
        var uploading = new ChannelProfile { Name = "A", Options = SampleOptions(5) };
        var other = new ChannelProfile { Name = "B", Options = SampleOptions(5) };
        data.Channels.Add(uploading);
        data.Channels.Add(other);
        UploadTemplates.EnsureMissing(data);

        other.Options.VideosPerDay = 2;
        UploadTemplates.SaveInto(data, other, channel => channel.Id == uploading.Id);

        Assert.Equal(5, uploading.Options.VideosPerDay);
        Assert.Equal(2, other.Options.VideosPerDay);
        Assert.Equal(2, data.Templates.Single(template => template.Id == other.TemplateId).Options.VideosPerDay);
    }

    [Fact]
    public void Template_EditUpdatesSameTemplate_AndOtherChannels()
    {
        var data = new AppData();
        var first = new ChannelProfile { Name = "A", Options = SampleOptions(5) };
        var second = new ChannelProfile { Name = "B", Options = SampleOptions(5) };
        data.Channels.Add(first);
        data.Channels.Add(second);
        UploadTemplates.EnsureMissing(data);

        first.Options.TimeSlotsText = "05:00\n22:00";
        UploadTemplates.SaveInto(data, first);

        Assert.Single(data.Templates);
        Assert.Equal(first.TemplateId, second.TemplateId);
        Assert.Contains("22:00", second.Options.TimeSlotsText, StringComparison.Ordinal);
        Assert.Contains("22:00", data.Templates[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_AddCopy_LeavesThePreviousTemplate()
    {
        var data = new AppData();
        var channel = new ChannelProfile { Options = SampleOptions(5) };
        data.Channels.Add(channel);
        UploadTemplates.EnsureMissing(data);
        var originalId = channel.TemplateId;

        channel.Options.VideosPerDay = 2;
        var copy = UploadTemplates.AddCopy(data, channel);
        UploadTemplates.SaveInto(data, channel);

        Assert.Equal(2, data.Templates.Count);
        Assert.Equal(copy.Id, channel.TemplateId);
        Assert.Equal(5, data.Templates.Single(template => template.Id == originalId).Options.VideosPerDay);
        Assert.Equal(2, copy.Options.VideosPerDay);
    }

    [Fact]
    public void Template_CustomName_StaysWhenHoursChange()
    {
        var data = new AppData();
        var channel = new ChannelProfile { Options = SampleOptions(5) };
        data.Channels.Add(channel);
        var sync = UploadTemplates.Sync(data, channel);
        sync.Template.Name = "Khung giờ tối";
        sync.Template.NameIsCustom = true;

        channel.Options.TimeSlotsText = "22:00";
        UploadTemplates.SaveInto(data, channel);

        Assert.Equal("Khung giờ tối", sync.Template.Name);
        Assert.Contains("05:00", sync.Template.Options.TimeSlotsText, StringComparison.Ordinal);
        Assert.Equal("22:00", channel.Options.TimeSlotsText);
    }

    [Fact]
    public void Template_RepairRestoresScheduleWrittenInTheName()
    {
        var data = new AppData();
        var template = new UploadTemplate
        {
            Name = "4 video/ngày · 08:30, 12:30, 16:30, 21:00",
            NameIsCustom = true,
            Options = SampleOptions(5)
        };
        template.Options.TimeSlotsText = "05:00\n10:00\n15:00\n18:00\n20:00";
        data.Templates.Add(template);

        Assert.True(UploadTemplates.RepairNamedSchedules(data));
        Assert.Equal(4, template.Options.VideosPerDay);
        Assert.Equal("08:30\r\n12:30\r\n16:30\r\n21:00", template.Options.TimeSlotsText);
        Assert.False(UploadTemplates.RepairNamedSchedules(data));
    }

    [Fact]
    public void AutoSchedule_NewChannelDoesNotUploadUntilUserStartsIt()
    {
        Assert.False(AutoScheduleGate.IsDue(userStarted: false, enabled: true, exactDaily: true, stopped: false, alreadyDone: false));
        Assert.True(AutoScheduleGate.IsDue(userStarted: true, enabled: true, exactDaily: true, stopped: false, alreadyDone: false));
        Assert.False(AutoScheduleGate.IsDue(userStarted: true, enabled: true, exactDaily: true, stopped: false, alreadyDone: true));
        Assert.False(AutoScheduleGate.IsDue(userStarted: true, enabled: true, exactDaily: true, stopped: true, alreadyDone: false));
    }

    [Fact]
    public void DayBatch_UploadsTheTopChannelBeforeTheOneBelow()
    {
        var jobs = new[]
        {
            new BatchJob("bottom", new DateTimeOffset(2026, 10, 19, 8, 30, 0, TimeSpan.FromHours(7)), "001"),
            new BatchJob("top", new DateTimeOffset(2026, 10, 19, 21, 0, 0, TimeSpan.FromHours(7)), "002"),
            new BatchJob("top", new DateTimeOffset(2026, 10, 19, 8, 30, 0, TimeSpan.FromHours(7)), "001")
        };

        var ordered = ChannelQueueOrder.InListOrder(jobs, ["top", "bottom"], job => job.ChannelId, job => job.When, job => job.Index);

        Assert.Equal(["top", "top", "bottom"], ordered.Select(job => job.ChannelId).ToArray());
        Assert.Equal("001", ordered[0].Index);
        Assert.Equal("002", ordered[1].Index);
    }

    sealed record BatchJob(string ChannelId, DateTimeOffset When, string Index);

    static UploadOptions SampleOptions(int perDay)
    {
        var options = UploadOptions.CreateDefault();
        options.VideosPerDay = perDay;
        options.TimeSlotsText = "05:00\n10:00\n15:00";
        options.DescriptionTemplate = "{title}";
        options.ScheduleStartDate = new DateOnly(2026, 10, 13);
        return options;
    }

    static DateTimeOffset Local(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(7));
}
