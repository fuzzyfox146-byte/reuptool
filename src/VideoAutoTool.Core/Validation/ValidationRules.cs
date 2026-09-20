using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Fonts;
using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Subtitles;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Validation;

public static class ValidationRules
{
    public static ValidationIssue E001() => new(
        "E001", ValidationLevel.Error, "global",
        "Không tìm thấy ffmpeg/ffprobe.",
        "FFmpeg đã kèm trong app (tools\\ffmpeg). Nếu lỗi, chạy scripts\\fetch-ffmpeg.ps1 rồi build lại.");

    public static ValidationIssue E010(string layerId, string folder) => new(
        "E010", ValidationLevel.Error, $"layer:{layerId}",
        $"Thư mục layer '{folder}' không tồn tại hoặc không đọc được.",
        "Tạo thư mục hoặc sửa đường dẫn trong template.");

    public static ValidationIssue E011() => new(
        "E011", ValidationLevel.Error, "driver",
        "Thư mục driver rỗng.",
        "Thêm file video/audio nguồn vào thư mục driver.");

    public static ValidationIssue W012(string layerId) => new(
        "W012", ValidationLevel.Warning, $"layer:{layerId}",
        $"Thư mục layer '{layerId}' rỗng — layer sẽ bị bỏ qua khi render.",
        "Thêm file vào thư mục hoặc tắt layer nếu không dùng.");

    public static ValidationIssue E020(int videoIndex, string fileName) => new(
        "E020", ValidationLevel.Error, $"video:{videoIndex}",
        $"Không đọc được driver '{fileName}'.",
        "Kiểm tra file hỏng hoặc thử mở bằng ffprobe.");

    public static ValidationIssue E021(int videoIndex, string fileName) => new(
        "E021", ValidationLevel.Error, $"video:{videoIndex}",
        $"Driver '{fileName}' không có audio.",
        "Chỉ dùng file có track audio.");

    public static ValidationIssue E030(int videoIndex, string driverStem) => new(
        "E030", ValidationLevel.Error, $"video:{videoIndex}",
        $"Video '{driverStem}' không có file SRT tương ứng.",
        "Thêm file .srt khớp số hoặc tên driver.");

    public static ValidationIssue E031(int videoIndex, string fileName) => new(
        "E031", ValidationLevel.Error, $"video:{videoIndex}",
        $"SRT '{fileName}' không đọc được hoặc không có cue.",
        "Sửa định dạng SRT hoặc thay file.");

    public static ValidationIssue W032(int number, string usedFile) => new(
        "W032", ValidationLevel.Warning, $"sub:{number}",
        $"Trùng số SRT {number:D3}; dùng file đầu tiên '{usedFile}'.",
        "Xóa hoặc đổi tên file trùng số.");

    public static ValidationIssue W033(int videoIndex, string fileName) => new(
        "W033", ValidationLevel.Warning, $"video:{videoIndex}",
        $"SRT '{fileName}' có cue bắt đầu sau độ dài audio.",
        "Cắt hoặc sửa thời gian cue trong SRT.");

    public static ValidationIssue W034(int videoIndex, string fileName) => new(
        "W034", ValidationLevel.Warning, $"video:{videoIndex}",
        $"Cue cuối trong '{fileName}' kết thúc sớm hơn audio quá 20%.",
        "Kiểm tra sub có bị thiếu đoạn cuối.");

    public static ValidationIssue E040(string fileName) => new(
        "E040", ValidationLevel.Error, "layer:bg",
        $"Background '{fileName}' hỏng hoặc không đọc được.",
        "Thay file background.");

    public static ValidationIssue W041(string fileName) => new(
        "W041", ValidationLevel.Warning, "layer:bg",
        $"Background '{fileName}' ngắn hơn 2 giây — dễ giật khi nối.",
        "Dùng clip nền dài hơn 2 giây.");

    public static ValidationIssue W042(string fileName, int width, int height, int requiredW, int requiredH) => new(
        "W042", ValidationLevel.Warning, "layer:bg",
        $"Background '{fileName}' ({width}x{height}) nhỏ hơn khung yêu cầu (~{requiredW}x{requiredH}).",
        "Dùng clip độ phân giải cao hơn.");

    public static ValidationIssue I043(string fileName) => new(
        "I043", ValidationLevel.Info, "layer:bg",
        $"Background '{fileName}' có audio — sẽ bị bỏ khi render.",
        "Không cần sửa nếu chỉ dùng hình nền.");

    public static ValidationIssue E050(string fileName) => new(
        "E050", ValidationLevel.Error, "layer:avatar",
        $"Avatar '{fileName}' hỏng hoặc không phải ảnh.",
        "Dùng file PNG/WebP hợp lệ.");

    public static ValidationIssue W051(string fileName) => new(
        "W051", ValidationLevel.Warning, "layer:avatar",
        $"Avatar '{fileName}' không có kênh alpha.",
        "Dùng PNG trong suốt để tránh nền chữ nhật.");

    public static ValidationIssue E060(string fileName) => new(
        "E060", ValidationLevel.Error, "layer:wave",
        $"Soundwave '{fileName}' hỏng hoặc không đọc được.",
        "Thay file wave.");

    public static ValidationIssue W061(string fileName) => new(
        "W061", ValidationLevel.Warning, "layer:wave",
        $"Wave '{fileName}' được cấu hình alpha nhưng file không có alpha.",
        "Đổi alpha mode sang auto/blackKey hoặc dùng file có alpha.");

    public static ValidationIssue W062(string fileName) => new(
        "W062", ValidationLevel.Warning, "layer:wave",
        $"Wave '{fileName}' ngắn hơn 1 giây — lặp có thể giật.",
        "Dùng clip wave dài hơn 1 giây.");

    public static ValidationIssue I063(string fileName, double fps, int canvasFps) => new(
        "I063", ValidationLevel.Info, "layer:wave",
        $"Wave '{fileName}' fps {fps:0.##} khác canvas {canvasFps} — sẽ chuẩn hóa.",
        "Không cần sửa.");

    public static ValidationIssue W070(string presetId, string font) => new(
        "W070", ValidationLevel.Warning, $"preset:{presetId}",
        $"Font '{font}' trong preset '{presetId}' không tìm thấy.",
        "Import font, chọn font bundled, hoặc cài font Windows.");

    public static ValidationIssue W071f(string presetId, string font) => new(
        "W071f", ValidationLevel.Warning, $"preset:{presetId}",
        $"Font imported '{font}' không còn trong thư mục fonts.",
        "Import lại file .ttf/.otf.");

    public static ValidationIssue E071(string folder) => new(
        "E071", ValidationLevel.Error, "output",
        $"Thư mục xuất '{folder}' không ghi được.",
        "Chọn thư mục khác hoặc kiểm tra quyền ghi.");

    public static ValidationIssue W072(long requiredBytes, long freeBytes) => new(
        "W072", ValidationLevel.Warning, "output",
        $"Dung lượng trống ước tính cần ~{requiredBytes / 1_000_000} MB, ổ còn ~{freeBytes / 1_000_000} MB.",
        "Giải phóng dung lượng hoặc đổi thư mục xuất.");

    public static ValidationIssue W073(string outputName) => new(
        "W073", ValidationLevel.Warning, "output",
        $"Tên file xuất trùng nhau: '{outputName}'.",
        "Đổi namePattern hoặc tên driver.");

    public static ValidationIssue W074(int videoIndex, string path) => new(
        "W074", ValidationLevel.Warning, $"video:{videoIndex}",
        $"Video xuất đã tồn tại: '{Path.GetFileName(path)}'.",
        "Bật skipExisting hoặc xóa file cũ.");

    public static ValidationIssue W090() => new(
        "W090", ValidationLevel.Warning, "layer:bg",
        "Tổng thời lượng background có thể không đủ — sẽ lặp lại từ đầu.",
        "Thêm clip nền hoặc chấp nhận lặp.");

    public static (int RequiredW, int RequiredH) CoverMinimumSize(Template template)
    {
        var bg = template.Layers.First(l => l.Type == LayerType.BackgroundChain);
        var w = (int)Math.Ceiling(template.Canvas.Width * bg.Scale);
        var h = (int)Math.Ceiling(template.Canvas.Height * bg.Scale);
        return (w, h);
    }

    public static async Task ValidateBackgroundsAsync(
        ValidationContext ctx,
        IMediaProbe probe,
        List<ValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (!ctx.LayerFiles.TryGetValue("bg", out var files) || files.Count == 0)
        {
            return;
        }

        var (reqW, reqH) = CoverMinimumSize(ctx.Template);
        double totalDuration = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var media = await probe.ProbeAsync(file.AbsolutePath, cancellationToken).ConfigureAwait(false);
                if (media.DurationSeconds is null or <= 0 || !media.HasVideo)
                {
                    issues.Add(E040(file.FileName));
                    continue;
                }

                totalDuration += media.DurationSeconds.Value;
                if (media.DurationSeconds < 2)
                {
                    issues.Add(W041(file.FileName));
                }

                if (media.Width is int w && media.Height is int h && (w < reqW || h < reqH))
                {
                    issues.Add(W042(file.FileName, w, h, reqW, reqH));
                }

                if (media.HasAudio)
                {
                    issues.Add(I043(file.FileName));
                }
            }
            catch
            {
                issues.Add(E040(file.FileName));
            }
        }

        var totalDriverDuration = 0.0;
        foreach (var driver in ctx.Drivers)
        {
            try
            {
                var media = await probe.ProbeAsync(driver.AbsolutePath, cancellationToken).ConfigureAwait(false);
                totalDriverDuration += media.AudioDurationSeconds ?? media.DurationSeconds ?? 0;
            }
            catch
            {
                // handled elsewhere
            }
        }

        if (totalDuration > 0 && totalDuration < totalDriverDuration * 0.5)
        {
            issues.Add(W090());
        }
    }

    public static async Task ValidateAvatarsAsync(
        ValidationContext ctx,
        IMediaProbe probe,
        List<ValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (!ctx.LayerFiles.TryGetValue("avatar", out var files))
        {
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var media = await probe.ProbeAsync(file.AbsolutePath, cancellationToken).ConfigureAwait(false);
                if (!media.HasVideo)
                {
                    issues.Add(E050(file.FileName));
                }
                else if (!media.HasAlpha)
                {
                    issues.Add(W051(file.FileName));
                }
            }
            catch
            {
                issues.Add(E050(file.FileName));
            }
        }
    }

    public static async Task ValidateWavesAsync(
        ValidationContext ctx,
        IMediaProbe probe,
        List<ValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        var waveLayer = ctx.Template.Layers.FirstOrDefault(l => l.Type == LayerType.LoopVideo);
        if (waveLayer is null || !ctx.LayerFiles.TryGetValue("wave", out var files))
        {
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var media = await probe.ProbeAsync(file.AbsolutePath, cancellationToken).ConfigureAwait(false);
                if (media.DurationSeconds is null or <= 0)
                {
                    issues.Add(E060(file.FileName));
                    continue;
                }

                if (media.DurationSeconds < 1)
                {
                    issues.Add(W062(file.FileName));
                }

                if (waveLayer.Alpha == AlphaMode.Alpha && !media.HasAlpha)
                {
                    issues.Add(W061(file.FileName));
                }

                if (media.Fps is double fps && Math.Abs(fps - ctx.Template.Canvas.Fps) > 0.5)
                {
                    issues.Add(I063(file.FileName, fps, ctx.Template.Canvas.Fps));
                }
            }
            catch
            {
                issues.Add(E060(file.FileName));
            }
        }
    }

    public static void ValidateFonts(Template template, IFontCatalog catalog, List<ValidationIssue> issues)
    {
        foreach (var preset in template.StylePresets)
        {
            if (!catalog.Exists(preset))
            {
                issues.Add(W070(preset.Id, preset.Font));
            }

            if (preset.FontSource == FontSource.Imported && catalog.ResolveFilePath(preset) is null)
            {
                issues.Add(W071f(preset.Id, preset.Font));
            }
        }
    }

    public static void ValidateDuplicateSubs(ValidationContext ctx, List<ValidationIssue> issues)
    {
        foreach (var group in ctx.Subs.Where(s => s.Number.HasValue).GroupBy(s => s.Number!.Value))
        {
            var ordered = group.OrderBy(s => s.FileName, new NaturalSortComparer()).ToList();
            if (ordered.Count > 1)
            {
                issues.Add(W032(group.Key, ordered[0].FileName));
            }
        }
    }

    public static async Task ValidateDriverSubsAsync(
        ValidationContext ctx,
        IMediaProbe probe,
        List<ValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < ctx.Drivers.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var driver = ctx.Drivers[i];
            var videoIndex = i + 1;
            MediaInfo? media = null;
            try
            {
                media = await probe.ProbeAsync(driver.AbsolutePath, cancellationToken).ConfigureAwait(false);
                if (!media.HasAudio)
                {
                    issues.Add(E021(videoIndex, driver.FileName));
                    continue;
                }
            }
            catch
            {
                issues.Add(E020(videoIndex, driver.FileName));
                continue;
            }

            ScannedFile? subFile = null;
            if (driver.Number is int num && ctx.SubByNumber.TryGetValue(num, out var numbered))
            {
                subFile = numbered;
            }
            else
            {
                var stem = Path.GetFileNameWithoutExtension(driver.FileName);
                subFile = ctx.Subs.FirstOrDefault(s =>
                    Path.GetFileNameWithoutExtension(s.FileName).StartsWith(stem, StringComparison.OrdinalIgnoreCase));
            }

            if (subFile is null)
            {
                issues.Add(E030(videoIndex, Path.GetFileNameWithoutExtension(driver.FileName)));
                continue;
            }

            List<Cue> cues;
            try
            {
                cues = SrtParser.ParseFile(subFile.AbsolutePath);
            }
            catch
            {
                issues.Add(E031(videoIndex, subFile.FileName));
                continue;
            }

            if (cues.Count == 0)
            {
                issues.Add(E031(videoIndex, subFile.FileName));
                continue;
            }

            var duration = media.AudioDurationSeconds ?? media.DurationSeconds ?? 0;
            if (cues.Any(c => c.Start.TotalSeconds > duration))
            {
                issues.Add(W033(videoIndex, subFile.FileName));
            }

            var lastEnd = cues.Max(c => c.End.TotalSeconds);
            if (duration > 0 && lastEnd < duration * 0.8)
            {
                issues.Add(W034(videoIndex, subFile.FileName));
            }
        }
    }

    public static void ValidateOutput(Template template, string root, IReadOnlyList<ScannedFile> drivers, IMediaProbe probe, List<ValidationIssue> issues)
    {
        var outputFolder = FileScanner.ResolveFolder(root, template.Output.Folder);
        try
        {
            Directory.CreateDirectory(outputFolder);
            var testFile = Path.Combine(outputFolder, $".vat-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "x");
            File.Delete(testFile);
        }
        catch
        {
            issues.Add(E071(template.Output.Folder));
            return;
        }

        var outputNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        double totalDuration = 0;
        for (var i = 0; i < drivers.Count; i++)
        {
            var driver = drivers[i];
            try
            {
                var media = probe.ProbeAsync(driver.AbsolutePath).GetAwaiter().GetResult();
                totalDuration += media.AudioDurationSeconds ?? media.DurationSeconds ?? 0;
            }
            catch
            {
                // skip
            }

            var stem = Path.GetFileNameWithoutExtension(driver.FileName);
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                stem = stem.Replace(c, '_');
            }

            var name = template.Output.NamePattern.Replace("{sourceStem}", stem, StringComparison.Ordinal) + ".mp4";
            outputNames[name] = outputNames.GetValueOrDefault(name) + 1;
            var outPath = Path.Combine(outputFolder, name);
            if (template.Output.SkipExisting && File.Exists(outPath))
            {
                issues.Add(W074(i + 1, outPath));
            }
        }

        foreach (var pair in outputNames.Where(p => p.Value > 1))
        {
            issues.Add(W073(pair.Key));
        }

        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(outputFolder))!);
            var required = DiskSpaceEstimator.EstimateBytes(template, totalDuration);
            if (drive.AvailableFreeSpace < required)
            {
                issues.Add(W072(required, drive.AvailableFreeSpace));
            }
        }
        catch
        {
            // ignore drive info errors
        }
    }
}
