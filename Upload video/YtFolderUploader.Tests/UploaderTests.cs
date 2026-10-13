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
        Assert.Equal(21, Local(skipped.Times[0]).Hour);

        var used = new[] { new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.FromHours(7)) };
        var midnight = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.FromHours(7));
        var blocked = SchedulePlanner.Plan(1, options, used, midnight);
        Assert.Equal(21, Local(blocked.Times[0]).Hour);
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

    static DateTimeOffset Local(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(7));
}
