using System.Text.Json;
using VideoAutoTool.Core.Cache;
using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Fonts;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Queue;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Subtitles;
using VideoAutoTool.Core.Templates;
using VideoAutoTool.Core.Validation;

return await CliApp.RunAsync(args);

internal static class CliApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args is ["--version"] or ["-v"] or ["version"])
        {
            Console.WriteLine("vat 1.0.0");
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1).ToArray());

        try
        {
            return command switch
            {
                "probe" => await ProbeCommand(options),
                "scan" => ScanCommand(options),
                "plan" => await PlanCommand(options),
                "validate" => await ValidateCommand(options),
                "preview" => await PreviewCommand(options),
                "render" => await RenderCommand(options),
                "bench" => await BenchCommand(options),
                "cache" => CacheCommand(options),
                "run" => await RunCommand(options),
                _ => Unknown(command)
            };
        }
        catch (FfmpegNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        return 1;
    }

    private static async Task<int> ProbeCommand(Dictionary<string, string?> options)
    {
        var file = Require(options, "file") ?? options.Values.FirstOrDefault(v => v is not null && !v.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(file))
        {
            throw new InvalidOperationException("Usage: vat probe <file>");
        }

        var paths = FfmpegLocator.Locate(options.GetValueOrDefault("ffmpeg-dir"));
        var probe = new FfmpegProbe(paths);
        var info = await probe.ProbeAsync(file!).ConfigureAwait(false);
        Console.WriteLine(JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int ScanCommand(Dictionary<string, string?> options)
    {
        var root = Require(options, "root")!;
        var template = LoadTemplate(options);
        PrintFiles("Drivers", FileScanner.ScanFolder(root, template.Driver.Folder, template.Driver.Extensions, template.Driver.NumberPattern));
        foreach (var layer in template.Layers.Where(l => l.Source is not null))
        {
            PrintFiles(layer.Name, FileScanner.ScanFolder(root, layer.Source!.Folder, layer.Source.Extensions, template.Driver.NumberPattern));
        }

        return 0;
    }

    private static async Task<int> PlanCommand(Dictionary<string, string?> options)
    {
        var root = Require(options, "root")!;
        var template = LoadTemplate(options);
        var paths = FfmpegLocator.Locate(options.GetValueOrDefault("ffmpeg-dir"));
        var planner = new JobPlanner(new FfmpegProbe(paths));
        var plan = await planner.PlanAsync(template, root).ConfigureAwait(false);
        if (options.ContainsKey("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            foreach (var job in plan.Jobs)
            {
                Console.WriteLine($"#{job.Index} {job.DriverStem} D={job.DurationSeconds:0.##}s bg={job.Backgrounds.Count} side={job.Side} preset={job.StylePreset.Id} out={job.OutputPath}");
            }
        }

        return 0;
    }

    private static async Task<int> ValidateCommand(Dictionary<string, string?> options)
    {
        var root = Require(options, "root")!;
        var template = LoadTemplate(options);
        var paths = FfmpegLocator.Locate(options.GetValueOrDefault("ffmpeg-dir"));
        var engine = new ValidationEngine(new FfmpegProbe(paths), new FontCatalog());
        var report = await engine.RunAsync(template, root).ConfigureAwait(false);
        if (options.ContainsKey("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(report.Issues, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            foreach (var issue in report.Issues.OrderBy(i => i.Code).ThenBy(i => i.Scope))
            {
                Console.WriteLine($"[{issue.Level}] {issue.Code} {issue.Scope}: {issue.Message}");
            }
        }

        return report.ErrorCount > 0 ? 2 : 0;
    }

    private static async Task<int> PreviewCommand(Dictionary<string, string?> options)
    {
        var root = Require(options, "root")!;
        var template = LoadTemplate(options);
        var index = int.Parse(Require(options, "index")!);
        var outPath = Require(options, "out")!;
        var paths = FfmpegLocator.Locate(options.GetValueOrDefault("ffmpeg-dir"));
        var probe = new FfmpegProbe(paths);
        var planner = new JobPlanner(probe);
        var plan = await planner.PlanAsync(template, root).ConfigureAwait(false);
        var job = plan.Jobs.FirstOrDefault(j => j.Index == index) ?? throw new InvalidOperationException($"Job index {index} not found.");
        var time = options.TryGetValue("time", out var t) ? double.Parse(t!) : 0.4;
        if (!options.ContainsKey("time") && job.SubPath is not null)
        {
            var cues = SrtParser.ParseFile(job.SubPath);
            if (cues.Count > 0)
            {
                time = cues[0].Start.TotalSeconds + 0.4;
            }
        }

        var renderer = CreateRenderer(paths);
        await renderer.RenderAsync(template, job, new RenderRequest(RenderMode.Frame, FrameTimeSeconds: time), outPath).ConfigureAwait(false);
        Console.WriteLine(outPath);
        return 0;
    }

    private static async Task<int> RenderCommand(Dictionary<string, string?> options)
    {
        var root = Require(options, "root")!;
        var template = LoadTemplate(options);
        var index = int.Parse(Require(options, "index")!);
        var paths = FfmpegLocator.Locate(options.GetValueOrDefault("ffmpeg-dir"));
        var probe = new FfmpegProbe(paths);
        var planner = new JobPlanner(probe);
        var plan = await planner.PlanAsync(template, root).ConfigureAwait(false);
        var job = plan.Jobs.FirstOrDefault(j => j.Index == index) ?? throw new InvalidOperationException($"Job index {index} not found.");
        var outPath = options.GetValueOrDefault("out") ?? job.OutputPath;
        var seconds = options.TryGetValue("seconds", out var s) ? double.Parse(s!) : (double?)null;
        var renderer = CreateRenderer(paths);
        await renderer.RenderAsync(
            template,
            job,
            seconds is null ? new RenderRequest(RenderMode.Full) : new RenderRequest(RenderMode.Clip, ClipSeconds: seconds),
            outPath).ConfigureAwait(false);
        Console.WriteLine(outPath);
        return 0;
    }


    private static async Task<int> BenchCommand(Dictionary<string, string?> options)
    {
        var root = Require(options, "root")!;
        var template = LoadTemplate(options);
        var index = int.Parse(options.GetValueOrDefault("index") ?? "0");
        
        var paths = FfmpegLocator.Locate(options.GetValueOrDefault("ffmpeg-dir"));
        var probe = new FfmpegProbe(paths);
        var planner = new JobPlanner(probe);
        var plan = await planner.PlanAsync(template, root).ConfigureAwait(false);
        var job = plan.Jobs.FirstOrDefault(j => j.Index == index) ?? throw new InvalidOperationException($"Job index {index} not found.");
        
        var runner = new FfmpegRunner(paths);
        var catalog = new FontCatalog();
        var cacheStore = new VideoAutoTool.Core.Cache.CacheStore();
        var assetService = new VideoAutoTool.Core.Cache.PreparedAssetService(runner, cacheStore, probe);
        var renderer = new JobRenderer(runner, probe, new AssBuilder(new SkiaTextMeasurer(), catalog), new EncoderSelector(runner), catalog, assetService);
        
        var tempDir = Path.Combine(Path.GetTempPath(), "vat-bench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var baselinePath = Path.Combine(tempDir, "baseline.mp4");
            var cachedPath = Path.Combine(tempDir, "cached.mp4");
            
            Console.WriteLine("=== Baseline Render (no cache) ===");
            var baselineStart = DateTime.UtcNow;
            await renderer.RenderAsync(template, job, new RenderRequest(RenderMode.Full), baselinePath, useCache: false).ConfigureAwait(false);
            var baselineElapsed = (DateTime.UtcNow - baselineStart).TotalSeconds;
            
            Console.WriteLine($"\n=== Cached Render ===");
            Console.WriteLine("Preparing assets...");
            var prepStart = DateTime.UtcNow;
            await assetService.PrepareBackgroundsAsync(template, job, cancellationToken: default).ConfigureAwait(false);
            var prepElapsed = (DateTime.UtcNow - prepStart).TotalSeconds;
            
            Console.WriteLine("Rendering with cache...");
            var cachedStart = DateTime.UtcNow;
            await renderer.RenderAsync(template, job, new RenderRequest(RenderMode.Full), cachedPath, useCache: true).ConfigureAwait(false);
            var cachedElapsed = (DateTime.UtcNow - cachedStart).TotalSeconds;
            
            Console.WriteLine($"\n=== Results ===");
            Console.WriteLine($"Baseline:  {baselineElapsed:F2}s render");
            Console.WriteLine($"Cached:    {prepElapsed:F2}s prep + {cachedElapsed:F2}s render = {(prepElapsed + cachedElapsed):F2}s total");
            
            var baselineInfo = await probe.ProbeAsync(baselinePath).ConfigureAwait(false);
            var cachedInfo = await probe.ProbeAsync(cachedPath).ConfigureAwait(false);
            var durationDiff = Math.Abs((baselineInfo.DurationSeconds ?? 0) - (cachedInfo.DurationSeconds ?? 0));
            Console.WriteLine($"Duration diff: {durationDiff:F3}s");
            
            if (durationDiff > 0.1)
            {
                Console.WriteLine($"WARNING: Duration difference exceeds 0.1s!");
                return 1;
            }
            
            Console.WriteLine($"\nOutput files:");
            Console.WriteLine($"  Baseline: {baselinePath}");
            Console.WriteLine($"  Cached:   {cachedPath}");
            
            return 0;
        }
        finally
        {
            // Keep temp files for inspection
            Console.WriteLine($"\nTemp directory: {tempDir}");
        }
    }

    private static int CacheCommand(Dictionary<string, string?> options)
    {
        var cacheStore = new VideoAutoTool.Core.Cache.CacheStore();
        
        if (options.ContainsKey("clear"))
        {
            Console.Write("Clear cache? (y/n): ");
            var confirm = Console.ReadLine();
            if (confirm?.ToLower() == "y")
            {
                cacheStore.Clear();
                Console.WriteLine("Cache cleared.");
            }
            else
            {
                Console.WriteLine("Cancelled.");
            }
            return 0;
        }
        
        var (count, totalBytes) = cacheStore.GetInfo();
        Console.WriteLine($"Cache: {count} file(s), {totalBytes / 1024.0 / 1024.0:F2} MB");
        return 0;
    }

    private static async Task<int> RunCommand(Dictionary<string, string?> options)
    {
        var root = Require(options, "root")!;
        var template = LoadTemplate(options);
        
        var paths = FfmpegLocator.Locate(options.GetValueOrDefault("ffmpeg-dir"));
        var probe = new FfmpegProbe(paths);
        var planner = new JobPlanner(probe);
        
        Console.WriteLine("Planning jobs...");
        var plan = await planner.PlanAsync(template, root).ConfigureAwait(false);
        
        var from = options.TryGetValue("from", out var f) ? int.Parse(f!) : 0;
        var limit = options.TryGetValue("limit", out var l) ? int.Parse(l!) : int.MaxValue;
        var parallel = options.TryGetValue("parallel", out var p) ? int.Parse(p!) : 1;
        var skipExisting = options.ContainsKey("skip-existing");
        
        var filteredJobs = plan.Jobs.Skip(from).Take(limit).ToList();
        var filteredPlan = new PlanResult(filteredJobs, plan.Warnings);
        
        Console.WriteLine($"Found {filteredJobs.Count} job(s) to render (parallel={parallel})");
        
        var renderer = CreateRenderer(paths);
        var assetService = new PreparedAssetService(new FfmpegRunner(paths), new CacheStore(), probe);
        var rendererWithCache = new JobRenderer(
            new FfmpegRunner(paths), 
            probe, 
            new AssBuilder(new SkiaTextMeasurer(), new FontCatalog()), 
            new EncoderSelector(new FfmpegRunner(paths)), 
            new FontCatalog(), 
            assetService);
        
        var jobRunner = new JobRendererAdapter(rendererWithCache);
        var queuePath = Path.Combine(Path.GetTempPath(), "vat-run-queue.json");
        var store = new JobStore(queuePath);
        
        using var queue = new RenderQueue(jobRunner, store, template, filteredPlan);
        queue.AddJobsFromPlan(filteredPlan, skipExisting);
        
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nCancelling...");
            cts.Cancel();
        };
        
        int lastProgress = -1;
        queue.ProgressChanged += (s, p) =>
        {
            var percent = (int)(p.OverallProgress * 100);
            if (percent != lastProgress)
            {
                lastProgress = percent;
                Console.Write($"\rProgress: {percent}% ({p.CompletedJobs}/{p.TotalJobs} jobs, {p.FailedJobs} failed)");
            }
        };
        
        try
        {
            await queue.RunAsync(parallel, cts.Token).ConfigureAwait(false);
            Console.WriteLine();
            
            var finalProgress = queue.GetProgress();
            if (finalProgress.FailedJobs > 0)
            {
                Console.WriteLine($"Completed with {finalProgress.FailedJobs} failed job(s)");
                return 1;
            }
            
            Console.WriteLine($"All {finalProgress.CompletedJobs} job(s) completed successfully");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nCancelled by user");
            return 2;
        }
        finally
        {
            store.Clear();
        }
    }
    
    private static JobRenderer CreateRenderer(FfmpegPaths paths)
    {
        var runner = new FfmpegRunner(paths);
        var probe = new FfmpegProbe(paths);
        var catalog = new FontCatalog();
        return new JobRenderer(runner, probe, new AssBuilder(new SkiaTextMeasurer(), catalog), new EncoderSelector(runner), catalog);
    }

    private static Template LoadTemplate(Dictionary<string, string?> options) =>
        options.TryGetValue("template", out var path) && !string.IsNullOrWhiteSpace(path)
            ? TemplateStore.Load(path!)
            : TemplateDefaults.CreateCo139();

    private static void PrintFiles(string title, IReadOnlyList<ScannedFile> files)
    {
        Console.WriteLine($"[{title}] {files.Count} file(s)");
        foreach (var file in files)
        {
            Console.WriteLine($"  {file.RelativePath}");
        }
    }

    private static string? Require(Dictionary<string, string?> options, string key) =>
        options.TryGetValue(key, out var value) ? value : null;

    private static Dictionary<string, string?> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (!options.ContainsKey("file"))
                {
                    options["file"] = arg;
                }

                continue;
            }

            var key = arg[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = args[++i];
            }
            else
            {
                options[key] = "true";
            }
        }

        return options;
    }
}
