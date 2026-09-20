using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Planning;

public sealed class JobPlanner
{
    private const double DurationTolerance = 0.02;
    private const double MinBackgroundSeconds = 0.5;
    private const int MaxBackgroundSegments = 500;

    private readonly IMediaProbe _probe;

    public JobPlanner(IMediaProbe probe) => _probe = probe;

    public async Task<PlanResult> PlanAsync(
        Template template,
        string root,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<PlanWarning>();
        var jobs = new List<RenderJobPlan>();

        var drivers = FileScanner.ScanFolder(root, template.Driver.Folder, template.Driver.Extensions, template.Driver.NumberPattern);
        var subLayer = template.Layers.FirstOrDefault(l => l.Type == LayerType.Subtitle);
        var avatarLayer = template.Layers.FirstOrDefault(l => l.Type == LayerType.Image && l.Id == "avatar");
        var waveLayer = template.Layers.FirstOrDefault(l => l.Type == LayerType.LoopVideo);
        var bgLayer = template.Layers.FirstOrDefault(l => l.Type == LayerType.BackgroundChain);

        var subs = subLayer?.Source is null
            ? []
            : FileScanner.ScanFolder(root, subLayer.Source.Folder, subLayer.Source.Extensions, template.Driver.NumberPattern);
        var avatars = avatarLayer?.Source is null || !avatarLayer.Visible
            ? []
            : FileScanner.ScanFolder(root, avatarLayer.Source.Folder, avatarLayer.Source.Extensions, template.Driver.NumberPattern);
        var waves = waveLayer?.Source is null || !waveLayer.Visible
            ? []
            : FileScanner.ScanFolder(root, waveLayer.Source.Folder, waveLayer.Source.Extensions, template.Driver.NumberPattern);
        var backgrounds = bgLayer?.Source is null || !bgLayer.Visible
            ? []
            : FileScanner.ScanFolder(root, bgLayer.Source.Folder, bgLayer.Source.Extensions, template.Driver.NumberPattern);

        var subByNumber = BuildSubLookup(subs, warnings);
        var backgroundPointer = 0;

        for (var i = 0; i < drivers.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var driver = drivers[i];
            var media = await _probe.ProbeAsync(driver.AbsolutePath, cancellationToken).ConfigureAwait(false);
            if (!media.HasAudio || media.AudioDurationSeconds is null or <= 0)
            {
                warnings.Add(new PlanWarning(PlanWarningLevel.Warning, $"Driver '{driver.FileName}' has no audio; skipped."));
                continue;
            }

            var duration = media.AudioDurationSeconds.Value;
            var side = ResolveSide(template.AvatarSide, i);
            var preset = ResolveStylePreset(template, i, warnings);
            var subPath = MatchSub(driver, subs, subByNumber, warnings);
            var avatarPath = avatars.Count == 0 ? null : avatars[i % avatars.Count].AbsolutePath;
            var wavePath = waves.Count == 0 ? null : waves[i % waves.Count].AbsolutePath;
            var bgSegments = BuildBackgroundChain(backgrounds, ref backgroundPointer, duration, warnings);
            var outputPath = BuildOutputPath(root, template, driver);

            jobs.Add(new RenderJobPlan(
                jobs.Count + 1,
                driver.AbsolutePath,
                Path.GetFileNameWithoutExtension(driver.FileName),
                subPath,
                duration,
                bgSegments,
                avatarPath,
                wavePath,
                side,
                preset,
                outputPath));
        }

        return new PlanResult(jobs, warnings);
    }

    private static Dictionary<int, ScannedFile> BuildSubLookup(IReadOnlyList<ScannedFile> subs, List<PlanWarning> warnings)
    {
        var map = new Dictionary<int, ScannedFile>();
        foreach (var group in subs.Where(s => s.Number.HasValue).GroupBy(s => s.Number!.Value))
        {
            var ordered = group.OrderBy(s => s.FileName, new NaturalSortComparer()).ToList();
            map[group.Key] = ordered[0];
            if (ordered.Count > 1)
            {
                warnings.Add(new PlanWarning(PlanWarningLevel.Warning,
                    $"Duplicate SRT number {group.Key}; using '{ordered[0].FileName}'."));
            }
        }

        return map;
    }

    private static string? MatchSub(
        ScannedFile driver,
        IReadOnlyList<ScannedFile> subs,
        Dictionary<int, ScannedFile> subByNumber,
        List<PlanWarning> warnings)
    {
        if (driver.Number is int number && subByNumber.TryGetValue(number, out var numbered))
        {
            return numbered.AbsolutePath;
        }

        var stem = Path.GetFileNameWithoutExtension(driver.FileName);
        var match = subs.FirstOrDefault(s => Path.GetFileNameWithoutExtension(s.FileName).StartsWith(stem, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match.AbsolutePath;
        }

        warnings.Add(new PlanWarning(PlanWarningLevel.Warning, $"No SRT matched for driver '{driver.FileName}'."));
        return null;
    }

    private static StylePreset ResolveStylePreset(Template template, int index, List<PlanWarning> warnings)
    {
        var subLayer = template.Layers.First(l => l.Type == LayerType.Subtitle);
        var assignment = subLayer.StyleAssignment ?? new StyleAssignment();
        var presetIds = assignment.Presets ?? [];
        if (presetIds.Count == 0)
        {
            warnings.Add(new PlanWarning(PlanWarningLevel.Warning, "No style presets configured; using first preset."));
            return template.StylePresets[0];
        }

        string presetId = assignment.Mode switch
        {
            StyleAssignmentMode.Fixed => presetIds[0],
            StyleAssignmentMode.RoundRobin => presetIds[index % presetIds.Count],
            StyleAssignmentMode.ByRange => ResolveByRange(assignment, index + 1, presetIds[0]),
            _ => presetIds[0]
        };

        return template.StylePresets.FirstOrDefault(p => p.Id == presetId) ?? template.StylePresets[0];
    }

    private static string ResolveByRange(StyleAssignment assignment, int videoNumber, string fallback)
    {
        foreach (var range in assignment.Ranges ?? [])
        {
            if (videoNumber >= range.From && videoNumber <= range.To)
            {
                return range.Preset;
            }
        }

        return fallback;
    }

    private static string ResolveSide(AvatarSideMode mode, int index) => mode switch
    {
        AvatarSideMode.Left => "left",
        AvatarSideMode.Alternate => index % 2 == 0 ? "right" : "left",
        _ => "right"
    };

    private List<BackgroundSegment> BuildBackgroundChain(
        IReadOnlyList<ScannedFile> backgrounds,
        ref int pointer,
        double duration,
        List<PlanWarning> warnings)
    {
        var segments = new List<BackgroundSegment>();
        if (backgrounds.Count == 0)
        {
            warnings.Add(new PlanWarning(PlanWarningLevel.Warning, "No background files available."));
            return segments;
        }

        var total = 0.0;
        var guard = 0;
        while (total < duration - DurationTolerance)
        {
            if (++guard > MaxBackgroundSegments)
            {
                throw new InvalidOperationException($"Background chain exceeded {MaxBackgroundSegments} segments.");
            }

            var bg = backgrounds[pointer % backgrounds.Count];
            pointer++;

            var media = _probe.ProbeAsync(bg.AbsolutePath).GetAwaiter().GetResult();
            if (media.DurationSeconds is null or < MinBackgroundSeconds)
            {
                warnings.Add(new PlanWarning(PlanWarningLevel.Warning, $"Skipping broken/short background '{bg.FileName}'."));
                continue;
            }

            segments.Add(new BackgroundSegment(bg.AbsolutePath, media.DurationSeconds.Value));
            total += media.DurationSeconds.Value;
        }

        return segments;
    }

    private static string BuildOutputPath(string root, Template template, ScannedFile driver)
    {
        var outputFolder = FileScanner.ResolveFolder(root, template.Output.Folder);
        Directory.CreateDirectory(outputFolder);
        var stem = Path.GetFileNameWithoutExtension(driver.FileName);
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(c, '_');
        }

        var name = template.Output.NamePattern.Replace("{sourceStem}", stem, StringComparison.Ordinal);
        return Path.Combine(outputFolder, $"{name}.mp4");
    }
}
