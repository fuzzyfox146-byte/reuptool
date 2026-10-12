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
}
