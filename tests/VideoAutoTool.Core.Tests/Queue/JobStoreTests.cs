using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Queue;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Queue;

public sealed class JobStoreTests : IDisposable
{
    private readonly string _tempDir;

    public JobStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vat-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        var queuePath = Path.Combine(_tempDir, "queue.json");
        var store = new JobStore(queuePath);

        var jobs = store.Load();

        Assert.Empty(jobs);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var queuePath = Path.Combine(_tempDir, "queue.json");
        var store = new JobStore(queuePath);

        var jobs = new List<RenderJobItem>
        {
            new()
            {
                Id = "job1",
                JobIndex = 0,
                DriverPath = "d:\\test\\video1.mp4",
                OutputPath = "d:\\out\\video1.mp4",
                DurationSeconds = 120.5,
                Status = JobStatus.Pending,
                Progress = 0.0
            },
            new()
            {
                Id = "job2",
                JobIndex = 1,
                DriverPath = "d:\\test\\video2.mp4",
                OutputPath = "d:\\out\\video2.mp4",
                DurationSeconds = 90.0,
                Status = JobStatus.Done,
                Progress = 1.0,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                FinishedAt = DateTime.UtcNow
            }
        };

        store.Save(jobs);
        var loaded = store.Load();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("job1", loaded[0].Id);
        Assert.Equal(JobStatus.Pending, loaded[0].Status);
        Assert.Equal("job2", loaded[1].Id);
        Assert.Equal(JobStatus.Done, loaded[1].Status);
    }

    [Fact]
    public void Load_ResetsRunningJobsToPending()
    {
        var queuePath = Path.Combine(_tempDir, "queue.json");
        var store = new JobStore(queuePath);

        var jobs = new List<RenderJobItem>
        {
            new()
            {
                Id = "job1",
                JobIndex = 0,
                DriverPath = "d:\\test\\video1.mp4",
                OutputPath = "d:\\out\\video1.mp4",
                DurationSeconds = 120.0,
                Status = JobStatus.Running,
                Progress = 0.5,
                StartedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new()
            {
                Id = "job2",
                JobIndex = 1,
                DriverPath = "d:\\test\\video2.mp4",
                OutputPath = "d:\\out\\video2.mp4",
                DurationSeconds = 90.0,
                Status = JobStatus.Done,
                Progress = 1.0
            }
        };

        store.Save(jobs);
        var loaded = store.Load();

        Assert.Equal(JobStatus.Pending, loaded[0].Status);
        Assert.Equal(0.0, loaded[0].Progress);
        Assert.Null(loaded[0].StartedAt);
        Assert.Equal(JobStatus.Done, loaded[1].Status);
    }

    [Fact]
    public void Save_IsAtomic()
    {
        var queuePath = Path.Combine(_tempDir, "queue.json");
        var store = new JobStore(queuePath);

        var jobs1 = new List<RenderJobItem>
        {
            new()
            {
                Id = "job1",
                JobIndex = 0,
                DriverPath = "d:\\test\\video1.mp4",
                OutputPath = "d:\\out\\video1.mp4",
                DurationSeconds = 120.0,
                Status = JobStatus.Pending,
                Progress = 0.0
            }
        };

        store.Save(jobs1);
        Assert.True(File.Exists(queuePath));
        Assert.False(File.Exists(queuePath + ".tmp"));

        var jobs2 = new List<RenderJobItem>
        {
            new()
            {
                Id = "job2",
                JobIndex = 1,
                DriverPath = "d:\\test\\video2.mp4",
                OutputPath = "d:\\out\\video2.mp4",
                DurationSeconds = 90.0,
                Status = JobStatus.Done,
                Progress = 1.0
            }
        };

        store.Save(jobs2);
        var loaded = store.Load();
        
        Assert.Single(loaded);
        Assert.Equal("job2", loaded[0].Id);
        Assert.False(File.Exists(queuePath + ".tmp"));
    }

    [Fact]
    public void Clear_DeletesQueueFile()
    {
        var queuePath = Path.Combine(_tempDir, "queue.json");
        var store = new JobStore(queuePath);

        var jobs = new List<RenderJobItem>
        {
            new()
            {
                Id = "job1",
                JobIndex = 0,
                DriverPath = "d:\\test\\video1.mp4",
                OutputPath = "d:\\out\\video1.mp4",
                DurationSeconds = 120.0,
                Status = JobStatus.Pending,
                Progress = 0.0
            }
        };

        store.Save(jobs);
        Assert.True(File.Exists(queuePath));

        store.Clear();
        Assert.False(File.Exists(queuePath));
    }

    [Fact]
    public void SaveSnapshot_RoundTripsPlansAndIntake()
    {
        var queuePath = Path.Combine(_tempDir, "queue-snap.json");
        var store = new JobStore(queuePath);
        var template = TemplateDefaults.CreateCo139();
        var plan = new RenderJobPlan(
            1,
            @"D:\src\a.mp4",
            "a",
            @"D:\sub\a.srt",
            12,
            [new BackgroundSegment(@"D:\bg\clip.mp4", 20)],
            @"D:\av\a.png",
            @"D:\w\w.mp4",
            "right",
            template.StylePresets[0],
            @"D:\out\a.mp4");

        store.SaveSnapshot(new QueueSnapshot
        {
            Jobs =
            [
                new RenderJobItem
                {
                    Id = "job1",
                    JobIndex = 1,
                    DriverPath = plan.DriverPath,
                    OutputPath = plan.OutputPath,
                    DurationSeconds = 12,
                    Status = JobStatus.Pending,
                    IntakeId = "01",
                    IntakeName = "KenhA",
                    Promoted = true
                }
            ],
            PlansByJobId = { ["job1"] = plan },
            TemplateByIntake = { ["01"] = template },
            IntakeOrder = ["01"],
            IntakeSequence = 1,
            BatchSize = 9,
            LastBatchSize = 9
        });

        var loaded = store.LoadSnapshot();
        Assert.Equal("job1", loaded.Jobs[0].Id);
        Assert.Equal("01", loaded.Jobs[0].IntakeId);
        Assert.True(loaded.PlansByJobId.ContainsKey("job1"));
        Assert.Equal(20, loaded.PlansByJobId["job1"].Backgrounds[0].DurationFull);
        Assert.True(loaded.TemplateByIntake.ContainsKey("01"));
        Assert.Equal(9, loaded.BatchSize);
        Assert.Equal(1, loaded.IntakeSequence);
    }

    [Fact]
    public void LoadSnapshot_ReadsLegacyJobArray()
    {
        var queuePath = Path.Combine(_tempDir, "queue-legacy.json");
        var store = new JobStore(queuePath);
        store.Save(
        [
            new RenderJobItem
            {
                Id = "old",
                JobIndex = 0,
                DriverPath = @"D:\src\a.mp4",
                OutputPath = @"D:\out\a.mp4",
                DurationSeconds = 5,
                Status = JobStatus.Pending
            }
        ]);

        var loaded = store.LoadSnapshot();
        Assert.Single(loaded.Jobs);
        Assert.Equal("old", loaded.Jobs[0].Id);
        Assert.Empty(loaded.PlansByJobId);
    }
}
