using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Subtitles;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Planning;

public sealed class JobPlanner
{
    private const double DurationTolerance = 0.02;
    private const double MinBackgroundSeconds = 0.5;

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

        var backgroundPointer = 0;
        var durationCache = new Dictionary<string, MediaInfo>(StringComparer.OrdinalIgnoreCase);

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
            var (bgSegments, nextPointer) = await PickOneBackgroundAsync(
                backgrounds, duration, durationCache, warnings, cancellationToken, backgroundPointer).ConfigureAwait(false);
            backgroundPointer = nextPointer;
            if (bgSegments.Count == 0)
            {
                warnings.Add(new PlanWarning(PlanWarningLevel.Warning,
                    $"Skipped '{driver.FileName}': no background longer than source audio ({duration:0.##}s)."));
                continue;
            }

            var pickIndex = jobs.Count;
            var side = ResolveSide(template.AvatarSide, pickIndex);
            var preset = ResolveStylePreset(template, pickIndex, warnings);
            var subPath = MatchSub(driver, subs, warnings);
            if (subPath is null)
            {
                continue;
            }
            var avatarPath = avatars.Count == 0 ? null : avatars[pickIndex % avatars.Count].AbsolutePath;
            var wavePath = waves.Count == 0 ? null : waves[pickIndex % waves.Count].AbsolutePath;
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

    private static string? MatchSub(
        ScannedFile driver,
        IReadOnlyList<ScannedFile> subs,
        List<PlanWarning> warnings)
    {
        var paired = SubtitleNameMatcher.Resolve(driver, subs);
        if (paired.Match is not null)
        {
            if (paired.MatchCount > 1)
            {
                warnings.Add(new PlanWarning(PlanWarningLevel.Warning,
                    $"Several SRT files match '{driver.FileName}'; using '{paired.Match.FileName}'."));
            }

            return paired.Match.AbsolutePath;
        }

        if (paired.SameNumberMismatch is not null)
        {
            warnings.Add(new PlanWarning(PlanWarningLevel.Warning,
                $"SRT number matches but the title differs: '{driver.FileName}' vs '{paired.SameNumberMismatch.FileName}'. Skipped."));
        }
        else
        {
            warnings.Add(new PlanWarning(PlanWarningLevel.Warning, $"No SRT matched for driver '{driver.FileName}'. Skipped."));
        }

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

    /// <summary>
    /// One background per job (round-robin). Longer clips are trimmed to the driver audio
    /// (background audio is never mapped). Shorter clips are skipped in favor of a longer one;
    /// if none is long enough the caller skips the job.
    /// </summary>
    private async Task<(List<BackgroundSegment> Segments, int NextPointer)> PickOneBackgroundAsync(
        IReadOnlyList<ScannedFile> backgrounds,
        double needSeconds,
        Dictionary<string, MediaInfo> cache,
        List<PlanWarning> warnings,
        CancellationToken cancellationToken,
        int pointer)
    {
        var segments = new List<BackgroundSegment>();
        if (backgrounds.Count == 0)
        {
            warnings.Add(new PlanWarning(PlanWarningLevel.Warning, "No background files available."));
            return (segments, pointer);
        }

        for (var n = 0; n < backgrounds.Count; n++)
        {
            var idx = (pointer + n) % backgrounds.Count;
            var bg = backgrounds[idx];
            var media = await ProbeCachedAsync(bg.AbsolutePath, cache, cancellationToken).ConfigureAwait(false);
            var duration = media.DurationSeconds ?? 0;
            if (duration < MinBackgroundSeconds)
            {
                warnings.Add(new PlanWarning(PlanWarningLevel.Warning, $"Skipping broken/short background '{bg.FileName}'."));
                continue;
            }

            if (duration + DurationTolerance >= needSeconds)
            {
                segments.Add(new BackgroundSegment(bg.AbsolutePath, duration));
                return (segments, (idx + 1) % backgrounds.Count);
            }
        }

        return (segments, pointer);
    }

    private async Task<MediaInfo> ProbeCachedAsync(
        string path,
        Dictionary<string, MediaInfo> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var media = await _probe.ProbeAsync(path, cancellationToken).ConfigureAwait(false);
        cache[path] = media;
        return media;
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
