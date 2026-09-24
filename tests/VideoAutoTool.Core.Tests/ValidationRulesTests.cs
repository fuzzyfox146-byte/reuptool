using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Fonts;
using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Subtitles;
using VideoAutoTool.Core.Templates;
using VideoAutoTool.Core.Validation;

namespace VideoAutoTool.Core.Tests;

public class ValidationRulesTests
{
    [Fact]
    public void E030_BlocksOnlyMissingVideo()
    {
        var issue = ValidationRules.E030(2, "driver-002");
        Assert.Equal("E030", issue.Code);
        Assert.Equal(ValidationLevel.Error, issue.Level);
        Assert.Equal("video:2", issue.Scope);
    }

    [Fact]
    public void W042_TriggersWhenBackgroundTooSmall()
    {
        var template = TemplateDefaults.CreateCo139();
        var (reqW, reqH) = ValidationRules.CoverMinimumSize(template);
        var issue = ValidationRules.W042("bg.mp4", 640, 360, reqW, reqH);
        Assert.Equal("W042", issue.Code);
    }

    [Fact]
    public void W070_WhenFontMissing()
    {
        var catalog = new FakeFontCatalog(exists: false);
        var issues = new List<ValidationIssue>();
        ValidationRules.ValidateFonts(TemplateDefaults.CreateCo139(), catalog, issues);
        Assert.Contains(issues, i => i.Code == "W070");
    }

    [Fact]
    public void W070_NotRaised_WhenFontExists()
    {
        var catalog = new FakeFontCatalog(exists: true);
        var issues = new List<ValidationIssue>();
        ValidationRules.ValidateFonts(TemplateDefaults.CreateCo139(), catalog, issues);
        Assert.DoesNotContain(issues, i => i.Code == "W070");
    }

    [Fact]
    public async Task E030_OnlyForVideoWithoutSub()
    {
        var root = CreateValidationRoot(includeSubForVideo1: true, includeSubForVideo2: false);
        var probe = new ValidationFakeProbe();
        var engine = new ValidationEngine(probe, new FakeFontCatalog(true), FakeFfmpeg);
        var report = await engine.RunAsync(TemplateDefaults.CreateCo139(), root);
        var v1Errors = report.Issues.Where(i => i.Scope == "video:1" && i.Level == ValidationLevel.Error).ToList();
        Assert.True(v1Errors.Count == 0, string.Join("; ", v1Errors.Select(e => $"{e.Code}:{e.Message}")));
        Assert.False(report.CanRender(2));
        Assert.Contains(report.Issues, i => i.Code == "E030" && i.Scope == "video:2");
        Assert.DoesNotContain(report.Issues, i => i.Code == "E030" && i.Scope == "video:1");
    }

    [Fact]
    public void SubtitleName_MatchesEnSuffix_RejectsSameNumberDifferentTitle()
    {
        const string video = "056 🔴 Chosen One： Heaven Confirms You Mastered A New Power. ✨👑.mp4";
        const string srt = "056 🔴 Chosen One： Heaven Confirms You Mastered A New Power. ✨👑.en.srt";
        Assert.True(SubtitleNameMatcher.IsMatch(video, srt));
        Assert.True(SubtitleNameMatcher.IsMatch(
            "010 Title.mp4",
            "010 Title.de.srt"));

        const string otherVideo = "047 Chosen One： God Is Bringing The Right Person Into Your Life — Age Is Not A Barrier To His Plan.mp4";
        const string otherSrt = "047 Chosen One, God Says： Your BEST YEARS Are Still Ahead.en.srt";
        Assert.False(SubtitleNameMatcher.IsMatch(otherVideo, otherSrt));

        var driver = new ScannedFile(otherVideo, otherVideo, 47, Path.GetFileName(otherVideo));
        var wrong = new ScannedFile(otherSrt, otherSrt, 47, Path.GetFileName(otherSrt));
        var paired = SubtitleNameMatcher.Resolve(driver, [wrong]);
        Assert.Null(paired.Match);
        Assert.Equal(wrong.FileName, paired.SameNumberMismatch?.FileName);
    }

    [Fact]
    public void CheckSubtitleNames_ReportsSameNumberTitleMismatch()
    {
        var root = CreateValidationRoot(includeSubForVideo1: true, includeSubForVideo2: false);
        WriteSrt(root, "002 Chosen One, God Says.en.srt");
        var issues = ValidationRules.CheckSubtitleNames(TemplateDefaults.CreateCo139(), root);
        Assert.Contains(issues, i => i.Code == "E035" && i.Message.Contains("002 d2.mp4", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, i => i.Code == "E030" && i.Scope == "video:1");
    }

    [Fact]
    public async Task Report_IsDeterministicallySorted()
    {
        var root = CreateValidationRoot(includeSubForVideo1: true, includeSubForVideo2: false);
        var engine = new ValidationEngine(new ValidationFakeProbe(), new FakeFontCatalog(true), FakeFfmpeg);
        var r1 = await engine.RunAsync(TemplateDefaults.CreateCo139(), root);
        var r2 = await engine.RunAsync(TemplateDefaults.CreateCo139(), root);
        Assert.Equal(
            r1.Issues.Select(i => $"{i.Code}|{i.Scope}"),
            r2.Issues.Select(i => $"{i.Code}|{i.Scope}"));
    }

    [Fact]
    public void DiskSpaceEstimator_UsesDocumentedFormula()
    {
        var template = TemplateDefaults.CreateCo139();
        var bytes = DiskSpaceEstimator.EstimateBytes(template, 100);
        Assert.True(bytes > 0);
    }

    private static string CreateValidationRoot(bool includeSubForVideo1, bool includeSubForVideo2)
    {
        var root = Path.Combine(Path.GetTempPath(), $"vat-val-{Guid.NewGuid():N}");
        Write(root, @"source\NGUON", "001 d1.mp4");
        Write(root, @"source\NGUON", "002 d2.mp4");
        Write(root, "background", "bg1.mp4");
        Write(root, "avatar", "a1.png");
        Write(root, "soundwave", "w1.mov");
        if (includeSubForVideo1)
        {
            WriteSrt(root, "001 d1.en.srt");
        }

        if (includeSubForVideo2)
        {
            WriteSrt(root, "002 d2.en.srt");
        }

        Directory.CreateDirectory(Path.Combine(root, "xuat_render"));
        return root;
    }

    private static void Write(string root, string folder, string name)
    {
        var dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), "x");
    }

    private static void WriteSrt(string root, string name)
    {
        var dir = Path.Combine(root, @"text\SUB");
        Directory.CreateDirectory(dir);
        var srt = "1\r\n00:00:00,000 --> 00:00:02,000\r\nhello\r\n\r\n";
        File.WriteAllText(Path.Combine(dir, name), srt, System.Text.Encoding.UTF8);
    }

    private sealed class FakeFontCatalog : IFontCatalog
    {
        private readonly bool _exists;

        public FakeFontCatalog(bool exists) => _exists = exists;

        public IReadOnlyList<FontEntry> ListAll() => [];

        public bool Exists(StylePreset preset) => _exists;

        public string? ResolveFilePath(StylePreset preset) => _exists ? "C:\\fake.ttf" : null;
    }

    private static FfmpegPaths FakeFfmpeg() => new(@"C:\ffmpeg\bin\ffmpeg.exe", @"C:\ffmpeg\bin\ffprobe.exe", "test");

    private sealed class ValidationFakeProbe : IMediaProbe
    {
        public Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
        {
            var name = Path.GetFileName(path);
            MediaInfo info = name switch
            {
                var n when n.Contains("d1") => new MediaInfo(path, 10, true, 10, true, 320, 180, 25, "yuv420p", false, "h264"),
                var n when n.Contains("d2") => new MediaInfo(path, 10, true, 10, true, 320, 180, 25, "yuv420p", false, "h264"),
                var n when n.StartsWith("bg") => new MediaInfo(path, 7, false, null, true, 1920, 1080, 30, "yuv420p", false, "h264"),
                var n when n.StartsWith("a1") => new MediaInfo(path, null, false, null, true, 500, 700, null, "rgba", true, null),
                var n when n.StartsWith("w1") => new MediaInfo(path, 2.5, false, null, true, 640, 160, 60, "argb", true, null),
                _ => new MediaInfo(path, 5, true, 5, true, 1280, 720, 25, "yuv420p", false, "h264")
            };
            return Task.FromResult(info);
        }
    }
}
