using VideoAutoTool.Core.FrameBatch;

namespace VideoAutoTool.Core.Tests.FrameBatch;

public class FrameBatchTests
{
    [Fact]
    public void DefaultLayout_IsFixedOnTheReferenceHole()
    {
        var layout = FrameLayoutMath.Create(new FrameSettings());
        Assert.Equal(1024, layout.VideoW);
        Assert.Equal(576, layout.VideoH);
        Assert.Equal(130, layout.X);
        Assert.Equal(72, layout.Y);
        Assert.Equal(642, layout.CenterX);
        Assert.Equal(360, layout.CenterY);
    }

    [Fact]
    public void Output480_Uses854By480Canvas()
    {
        var canvas = FrameDesign.CanvasFor(480);
        Assert.Equal(854, canvas.Width);
        Assert.Equal(480, canvas.Height);
        Assert.Equal(720, FrameDesign.NormalizeOutputHeight(0));
        Assert.Equal(480, FrameDesign.NormalizeOutputHeight(480));
        var settings = new FrameSettings(Width: canvas.Width, Height: canvas.Height, Gpu: true);
        var layout = FrameLayoutMath.Create(settings);
        Assert.Equal(0, layout.VideoW % 2);
        Assert.Equal(0, layout.VideoH % 2);
        Assert.True(layout.VideoW < canvas.Width);
        Assert.True(layout.VideoH < canvas.Height);
        var graph = FrameCommands.FilterGpu(settings, layout);
        Assert.Contains("scale_cuda=854:480:format=yuv420p", graph);
        Assert.Contains($"scale_cuda={layout.VideoW}:{layout.VideoH}", graph);
    }

    [Fact]
    public void SmallerScale_Stays16By9AndCentered()
    {
        var layout = FrameLayoutMath.Create(new FrameSettings(VideoScale: 0.5));
        Assert.Equal(16d / 9d, layout.VideoW / (double)layout.VideoH);
        Assert.Equal(642, layout.CenterX);
        Assert.Equal(360, layout.CenterY);
    }

    [Fact]
    public void RoundRobin_CyclesFramesAndBackgrounds()
    {
        var items = FramePlan.Assign(
            ["v1.mp4", "v2.mp4", "v3.mp4", "v4.mp4"],
            ["a.png", "b.png"],
            ["x.mp4", "y.mp4", "z.mp4"],
            random: false,
            seed: 1,
            previous: null);
        Assert.Equal(["a.png", "b.png", "a.png", "b.png"], items.Select(item => Path.GetFileName(item.Frame)).ToArray());
        Assert.Equal(["x.mp4", "y.mp4", "z.mp4", "x.mp4"], items.Select(item => Path.GetFileName(item.Background)).ToArray());
    }

    [Fact]
    public void Random_UsesEachFrameBeforeRepeating_AndStaysStable()
    {
        var videos = Enumerable.Range(0, 5).Select(index => $"v{index}.mp4").ToArray();
        var first = FramePlan.Assign(videos, ["a.png", "b.png", "c.png"], ["x.mp4", "y.mp4"], true, 7, null);
        var second = FramePlan.Assign(videos, ["a.png", "b.png", "c.png"], ["x.mp4", "y.mp4"], true, 7, null);
        var names = first.Take(3).Select(item => Path.GetFileName(item.Frame)).ToHashSet();
        Assert.Equal(["a.png", "b.png", "c.png"], names.OrderBy(name => name).ToArray());
        Assert.Equal(
            first.Select(item => Path.GetFileName(item.Frame)),
            second.Select(item => Path.GetFileName(item.Frame)));
    }

    [Fact]
    public void RenderCommand_UsesMainAudioAndFixedLayers()
    {
        var settings = new FrameSettings();
        var layout = FrameLayoutMath.Create(settings);
        var args = FrameCommands.Render("main.mp4", "bg.mp4", "frame.png", "out.mp4", 3.5, true, settings, layout);
        var graph = args[args.ToList().IndexOf("-filter_complex") + 1];
        Assert.Contains("colorchannelmixer=aa=0.90", graph);
        Assert.Contains("overlay=130:72", graph);
        Assert.DoesNotContain("blend=", graph);
        Assert.DoesNotContain("noise.mp4", args);
        Assert.Contains("-an", FrameCommands.BackgroundCache("bg.mp4", "out.mp4", settings));
        Assert.Contains("0:a:0", args);
        Assert.Contains("libx264", args);
        Assert.DoesNotContain("h264_nvenc", args);
    }

    [Fact]
    public void PromoteInitial_SkipsExistingFilesAndTakesTheNextNumbers()
    {
        var jobs = new List<FrameQueueSlot>
        {
            new() { IntakeId = "01", Status = FrameDesign.Skipped },
            new() { IntakeId = "01", Status = FrameDesign.Skipped },
            new() { IntakeId = "01", Status = FrameDesign.Skipped },
            new() { IntakeId = "01" },
            new() { IntakeId = "01" },
            new() { IntakeId = "01" },
            new() { IntakeId = "01" }
        };

        FrameIntake.PromoteInitial(jobs, 3);

        Assert.False(jobs[0].Promoted);
        Assert.False(jobs[1].Promoted);
        Assert.False(jobs[2].Promoted);
        Assert.True(jobs[3].Promoted);
        Assert.True(jobs[4].Promoted);
        Assert.True(jobs[5].Promoted);
        Assert.False(jobs[6].Promoted);
    }

    [Fact]
    public void DifferentFolders_RotateThreeThenThree()
    {
        var jobs = new List<FrameQueueSlot>();
        var order = new List<string> { "01", "02" };
        AddFolder(jobs, "01", 5);
        FrameIntake.PromoteInitial(jobs.Where(job => job.IntakeId == "01"), FrameIntake.DefaultBatchSize);
        AddFolder(jobs, "02", 5);
        FrameIntake.PromoteInitial(jobs.Where(job => job.IntakeId == "02"), FrameIntake.DefaultBatchSize);

        var pulled = new List<string>();
        while (jobs.Any(job => job.Status == FrameDesign.Wait))
        {
            FrameIntake.PromoteNextWave(jobs, order, FrameIntake.DefaultBatchSize);
            var next = jobs.First(job => job.Promoted && job.Status == FrameDesign.Wait);
            pulled.Add(next.IntakeId);
            next.Status = FrameDesign.Done;
        }

        Assert.Equal(["01", "01", "01", "02", "02", "02", "01", "01", "02", "02"], pulled);
    }

    [Fact]
    public void NextWave_WaitsUntilTheMainQueueHasNoPendingJob()
    {
        var jobs = new List<FrameQueueSlot>
        {
            new() { IntakeId = "01", Promoted = true },
            new() { IntakeId = "01" }
        };
        FrameIntake.PromoteNextWave(jobs, ["01"], 3);
        Assert.False(jobs[1].Promoted);
        jobs[0].Status = FrameDesign.Running;
        FrameIntake.PromoteNextWave(jobs, ["01"], 3);
        Assert.True(jobs[1].Promoted);
    }

    [Fact]
    public void SameDisk_WritesPartBesideTheOutput()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var final = Path.Combine(root, "FrameRender", "unit", "clip.mp4");
        var part = FrameStaging.ResolvePartPath(final, 1024);
        Assert.Equal(FrameStaging.SiblingPart(final), part);
        Assert.EndsWith(".part.mp4", part, StringComparison.OrdinalIgnoreCase);
        Assert.True(FrameStaging.EstimateBytes(10, 20) > 0);
        Assert.True(FramePressure.IsRecoverable(new IOException("There is not enough space on the disk.")));
        Assert.False(FramePressure.IsRecoverable(new InvalidOperationException("ffmpeg lỗi.")));
    }

    private static void AddFolder(List<FrameQueueSlot> jobs, string intakeId, int count)
    {
        for (var index = 0; index < count; index++)
        {
            jobs.Add(new FrameQueueSlot { IntakeId = intakeId });
        }
    }

    [Fact]
    public void GpuCommand_UsesNvencAndDropsAudioWhenMissing()
    {
        var settings = new FrameSettings(Gpu: true);
        var args = FrameCommands.Render("main.mp4", "bg.mp4", "frame.png", "out.mp4", 1, false, settings, FrameLayoutMath.Create(settings));
        Assert.Contains("h264_nvenc", args);
        Assert.Contains("p1", args);
        Assert.Contains("cuda", args);
        var graph = string.Join('\n', args);
        Assert.Contains("scale_cuda=1280:720:format=yuv420p", graph);
        Assert.Contains("overlay_cuda", graph);
        Assert.Contains("colorchannelmixer=aa=0.90", graph);
        Assert.Contains("hwupload_cuda", graph);
        Assert.DoesNotContain("color=c=black", graph);
        Assert.DoesNotContain("blend=", graph);
        Assert.DoesNotContain("-pix_fmt", args);
        Assert.Contains("-an", args);
        Assert.DoesNotContain("0:a:0", args);
    }

    [Fact]
    public void SourceFolderNamedVideo_UsesParentName()
    {
        Assert.Equal("HD037", FramePlan.SourceLabel(@"D:\HD037\video"));
        Assert.Equal("HD037", FramePlan.SourceLabel(@"D:\HD037\Video\"));
        Assert.Equal("HD037", FramePlan.SourceLabel(@"D:\HD037"));
        Assert.Equal("clips", FramePlan.SourceLabel(@"D:\HD037\clips"));
        Assert.Equal(@"D:\HD044\xuat", FramePlan.SiblingOutputFolder(@"D:\HD044\video"));
        Assert.Equal(@"D:\HD044\xuat", FramePlan.SiblingOutputFolder(@"D:\HD044\Video\"));
        Assert.Null(FramePlan.SiblingOutputFolder(@"D:\HD044"));
        Assert.Null(FramePlan.SiblingOutputFolder(@"D:\HD044\clips"));
    }

    [Fact]
    public void ParallelVideos_KeepsTheNumberTypedInTheBox()
    {
        Assert.Equal(5, FrameDesign.ParallelVideos(5));
        Assert.Equal(1, FrameDesign.ParallelVideos(0));
        Assert.Equal(FrameDesign.MaxParallelVideos, FrameDesign.ParallelVideos(40));
    }

    [Fact]
    public void InterruptedStatus_KeepsPausedAndCancelled()
    {
        Assert.Equal(FrameDesign.Wait, FrameLayoutMath.NormalizeStatus(FrameDesign.Running));
        Assert.Equal(FrameDesign.Paused, FrameLayoutMath.NormalizeStatus(FrameDesign.Paused));
        Assert.Equal(FrameDesign.Cancelled, FrameLayoutMath.NormalizeStatus(FrameDesign.Cancelled));
        Assert.Equal(FrameDesign.Done, FrameLayoutMath.NormalizeStatus(FrameDesign.Done));
        Assert.Equal(FrameDesign.Failed, FrameLayoutMath.NormalizeStatus(FrameDesign.Failed));
    }

    [Fact]
    public void FolderProgress_CountsFinishedOverAll_InFolderOrder()
    {
        var jobs = new (string Name, string State)[]
        {
            ("HD038", FrameDesign.Wait),
            ("HD037", FrameDesign.Done),
            ("HD038", FrameDesign.Skipped),
            ("HD037", FrameDesign.Cancelled),
            ("HD037", FrameDesign.Paused),
            ("HD037", FrameDesign.Failed),
        };
        var progress = FrameFolderProgress.Count(jobs);
        Assert.Equal(["HD038", "HD037"], progress.Select(item => item.Name).ToArray());
        Assert.Equal(1, progress[0].Done);
        Assert.Equal(2, progress[0].Total);
        Assert.Equal(1, progress[1].Done);
        Assert.Equal(4, progress[1].Total);
        Assert.Equal("HD038 1/2" + Environment.NewLine + "HD037 1/4", FrameFolderProgress.Format(jobs));
        var withKept = FrameFolderProgress.Count(jobs, new Dictionary<string, int> { ["HD038"] = 90 });
        Assert.Equal(91, withKept[0].Done);
        Assert.Equal(92, withKept[0].Total);
    }

    [Fact]
    public void StandardThreeLayerPreset_UsesExactFastHardwareGraph()
    {
        var preset = FrameTemplatePreset.CreateDefault();
        Assert.True(preset.IsStandardThreeLayer());

        var job = new ResolvedTemplateJob(
            "main.mp4",
            "out.mp4",
            MirroredLeft: false,
            [
                new ResolvedLayerAsset(preset.Layers[0], "bg.mp4", "#FFFFFF"),
                new ResolvedLayerAsset(preset.Layers[1], "main.mp4", "#FFFFFF"),
                new ResolvedLayerAsset(preset.Layers[2], "frame.png", "#FFFFFF")
            ]);

        var settings = new FrameSettings(Gpu: true);
        var templateArgs = FrameTemplateRenderer.BuildRenderArgs(job, preset, "out.mp4", 10.0, true, settings);
        var directArgs = FrameCommands.Render("main.mp4", "bg.mp4", "frame.png", "out.mp4", 10.0, true, settings, FrameLayoutMath.Create(settings));
        Assert.Equal(directArgs, templateArgs);
    }

    [Fact]
    public void MultiLayerTemplate_MatchesSubtitleByNumber_MirrorsAndAppliesEffectControls()
    {
        var temp = Path.Combine(Path.GetTempPath(), "vat-tpl-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);
        try
        {
            var bgDir = Path.Combine(temp, "bg");
            var avDir = Path.Combine(temp, "avatar");
            var waveDir = Path.Combine(temp, "wave");
            var subDir = Path.Combine(temp, "sub");
            Directory.CreateDirectory(bgDir);
            Directory.CreateDirectory(avDir);
            Directory.CreateDirectory(waveDir);
            Directory.CreateDirectory(subDir);

            var bg1 = Path.Combine(bgDir, "bg1.mp4");
            var av1 = Path.Combine(avDir, "av1.png");
            var av2 = Path.Combine(avDir, "av2.png");
            var wv1 = Path.Combine(waveDir, "wave1.mp4");
            var srt1 = Path.Combine(subDir, "001 Story One.en.srt");
            var srt2 = Path.Combine(subDir, "002 Story Two.en.srt");
            File.WriteAllText(bg1, "bg");
            File.WriteAllText(av1, "av1");
            File.WriteAllText(av2, "av2");
            File.WriteAllText(wv1, "wv1");
            File.WriteAllText(srt1, "1\n00:00:00,500 --> 00:00:02,000\nHello world\n");
            File.WriteAllText(srt2, "1\n00:00:00,500 --> 00:00:02,000\nSecond clip\n");

            var preset = FrameTemplatePreset.CreateStoryPreset(bgDir, avDir, waveDir, subDir);
            preset.AudioSpeed = 1.1;
            preset.Layers[1].FlipHorizontal = true;
            preset.Layers[1].CropLeft = 0.05;
            preset.Layers[1].Brightness = 0.1;
            preset.Layers[1].KeyMode = FrameKeyMode.CustomColorKey;
            preset.Layers[1].KeyColor = "#00FFAA";
            preset.Layers[1].KeyTolerance = 0.22;
            preset.Layers[1].KeySoftness = 0.10;

            var drivers = new[]
            {
                Path.Combine(temp, "001 Story One.mp4"),
                Path.Combine(temp, "002 Story Two.mp4")
            };

            var jobs = FrameTemplatePlanner.AssignJobs(drivers, preset, baseSeed: 42);
            Assert.Equal(2, jobs.Count);
            Assert.False(jobs[0].MirroredLeft);
            Assert.True(jobs[1].MirroredLeft);
            Assert.Equal(av1, jobs[0].Layers[1].AssetPath);
            Assert.Equal(av2, jobs[1].Layers[1].AssetPath);
            Assert.Equal(srt1, jobs[0].Layers[3].AssetPath);
            Assert.Equal(srt2, jobs[1].Layers[3].AssetPath);
            Assert.Equal("#F5A623", jobs[0].Layers[3].ChosenTextColor);
            Assert.Equal("#FFFFFF", jobs[1].Layers[3].ChosenTextColor);

            var assPath = Path.Combine(temp, "sub001.ass");
            FrameTemplateRenderer.WriteSubtitleAss(
                assPath,
                jobs[0].Layers[3].Layer,
                jobs[0].Layers[3].AssetPath,
                jobs[0].Layers[3].ChosenTextColor,
                new FrameSettings(),
                durationSeconds: 10,
                jobs[0].MirroredLeft);
            Assert.True(File.Exists(assPath));

            var args = FrameTemplateRenderer.BuildRenderArgs(
                jobs[0],
                preset,
                Path.Combine(temp, "out.mp4"),
                duration: 10,
                withAudio: true,
                new FrameSettings(Gpu: true),
                new Dictionary<string, string> { ["sub"] = assPath });

            var joined = string.Join(' ', args);
            Assert.Contains("-vn", args);
            Assert.Contains("hflip", joined);
            Assert.Contains("crop=iw*0.95", joined);
            Assert.Contains("eq=brightness=0.1", joined);
            Assert.Contains("colorkey=0x00FFAA:0.22:0.1", joined);
            Assert.Contains("lumakey=threshold=0", joined);
            Assert.Contains("subtitles=filename=", joined);
            Assert.Contains("atempo=1.1", joined);
            Assert.Contains("h264_nvenc", args);
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    [Fact]
    public void TemplateHistoryUndoAndRedoRestoresLayerResizeAndUltraKey()
    {
        var preset = FrameTemplatePreset.CreateStoryPreset();
        var history = new FrameTemplateHistory();
        history.Reset(preset);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);

        // Step 1: Resize avatar layer
        var avatar = preset.Layers.First(l => l.Kind == FrameLayerKind.ImageOverlay);
        var originalWidth = avatar.Width;
        avatar.Width = 640;
        avatar.Height = 520;
        Assert.True(history.RecordChange(preset));
        Assert.True(history.CanUndo);

        // Step 2: Sample Ultra Key color
        avatar.KeyMode = FrameKeyMode.CustomColorKey;
        avatar.KeyColor = "#12AB34";
        Assert.True(history.RecordChange(preset));

        // Undo Step 2 -> back to resized layer without custom key
        var afterUndo1 = history.Undo();
        Assert.NotNull(afterUndo1);
        var avatarUndo1 = afterUndo1!.Layers.First(l => l.Kind == FrameLayerKind.ImageOverlay);
        Assert.Equal(640, avatarUndo1.Width);
        Assert.Equal(FrameKeyMode.None, avatarUndo1.KeyMode);
        Assert.True(history.CanRedo);

        // Undo Step 1 -> back to original width
        var afterUndo2 = history.Undo();
        Assert.NotNull(afterUndo2);
        var avatarUndo2 = afterUndo2!.Layers.First(l => l.Kind == FrameLayerKind.ImageOverlay);
        Assert.Equal(originalWidth, avatarUndo2.Width);
        Assert.False(history.CanUndo);

        // Redo Step 1 & Step 2
        var afterRedo1 = history.Redo();
        Assert.NotNull(afterRedo1);
        Assert.Equal(640, afterRedo1!.Layers.First(l => l.Kind == FrameLayerKind.ImageOverlay).Width);

        var afterRedo2 = history.Redo();
        Assert.NotNull(afterRedo2);
        var avatarRedo2 = afterRedo2!.Layers.First(l => l.Kind == FrameLayerKind.ImageOverlay);
        Assert.Equal(FrameKeyMode.CustomColorKey, avatarRedo2.KeyMode);
        Assert.Equal("#12AB34", avatarRedo2.KeyColor);
    }
}
