using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Queue;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Queue;

public sealed class RenderQueueTests : IDisposable
{
    private readonly string _tempDir;

    public RenderQueueTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vat-test-queue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public async Task AddJobsFromPlan_AddsAllJobs()
    {
        var (queue, _, plan) = CreateQueue(3);

        queue.AddJobsFromPlan(plan);

        var jobs = queue.GetJobs();
        Assert.Equal(3, jobs.Count);
        Assert.All(jobs, j => Assert.Equal(JobStatus.Pending, j.Status));
    }

    [Fact]
    public async Task AddJobsFromPlan_SkipsExistingFiles()
    {
        var (queue, _, plan) = CreateQueue(2);

        // Create output file for first job
        await File.WriteAllTextAsync(plan.Jobs[0].OutputPath, "existing");

        queue.AddJobsFromPlan(plan, skipExisting: true);

        var jobs = queue.GetJobs();
        Assert.Equal(2, jobs.Count);
        Assert.Equal(JobStatus.Skipped, jobs[0].Status);
        Assert.Equal(JobStatus.Pending, jobs[1].Status);
    }

    [Fact]
    public async Task RunAsync_ExecutesJobsInOrder()
    {
        var (queue, runner, plan) = CreateQueue(3);
        
        queue.AddJobsFromPlan(plan);
        await queue.RunAsync(maxParallel: 1);

        Assert.Equal(3, runner.RenderedJobs.Count);
        
        var jobs = queue.GetJobs();
        Assert.All(jobs, j => Assert.Equal(JobStatus.Done, j.Status));
        Assert.All(jobs, j => Assert.Equal(1.0, j.Progress));
    }

    [Fact]
    public async Task RunAsync_ExecutesJobsInParallel()
    {
        var (queue, runner, plan) = CreateQueue(4);
        runner.RenderDelay = TimeSpan.FromMilliseconds(200);
        
        queue.AddJobsFromPlan(plan);
        
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await queue.RunAsync(maxParallel: 2);
        sw.Stop();

        Assert.Equal(4, runner.RenderedJobs.Count);
        
        // With parallel=2, 4 jobs should take ~2x the render time, not 4x
        // Allow more tolerance for lock overhead and test timing variability
        Assert.True(sw.ElapsedMilliseconds < 800, $"Took {sw.ElapsedMilliseconds}ms, expected <800ms for parallel execution");
        
        var jobs = queue.GetJobs();
        Assert.All(jobs, j => Assert.Equal(JobStatus.Done, j.Status));
    }

    [Fact]
    public async Task CancelAll_CancelsAllJobs()
    {
        var (queue, runner, plan) = CreateQueue(5);  // More jobs to ensure pending ones
        runner.RenderDelay = TimeSpan.FromMilliseconds(1000);  // Longer delay to keep jobs pending
        
        queue.AddJobsFromPlan(plan);
        
        var runTask = queue.RunAsync(maxParallel: 1);
        await Task.Delay(100);  // Wait for first job to start, then cancel
        
        queue.CancelAll();
        await runTask;

        var jobs = queue.GetJobs();
        // CancelAll cancels pending jobs immediately; running job may complete
        var cancelled = jobs.Count(j => j.Status == JobStatus.Cancelled);
        var done = jobs.Count(j => j.Status == JobStatus.Done);
        Assert.True(cancelled > 0, $"Expected some cancelled jobs, got {cancelled} cancelled, {done} done out of {jobs.Count}");
        Assert.True(done <= 1, $"Expected at most 1 done job (the running one), got {done}");
    }

    [Fact]
    public async Task CancelJob_CancelsSpecificJob()
    {
        var (queue, runner, plan) = CreateQueue(2);
        
        queue.AddJobsFromPlan(plan);
        var jobs = queue.GetJobs();
        
        queue.CancelJob(jobs[0].Id);
        
        Assert.Equal(JobStatus.Cancelled, jobs[0].Status);
        Assert.Equal(JobStatus.Pending, jobs[1].Status);
    }

    [Fact]
    public async Task RetryJob_ResetsFailedJobToPending()
    {
        var (queue, runner, plan) = CreateQueue(2);
        
        // Make first job fail
        runner.FailCondition = job => job.JobIndex == 0 ? new InvalidOperationException("Test failure") : null;
        
        queue.AddJobsFromPlan(plan);
        await queue.RunAsync(maxParallel: 1);

        var jobs = queue.GetJobs();
        Assert.Equal(JobStatus.Failed, jobs[0].Status);
        Assert.NotNull(jobs[0].ErrorMessage);
        
        // Retry the failed job
        runner.FailCondition = null;  // Don't fail this time
        queue.RetryJob(jobs[0].Id);
        
        Assert.Equal(JobStatus.Pending, jobs[0].Status);
        Assert.Equal(0.0, jobs[0].Progress);
        
        await queue.RunAsync(maxParallel: 1);
        Assert.Equal(JobStatus.Done, jobs[0].Status);
    }

    [Fact]
    public async Task SaveAndRestore_PreservesQueueState()
    {
        var queuePath = Path.Combine(_tempDir, "queue.json");
        var store = new JobStore(queuePath);
        var (template, plan) = CreatePlan(2);
        
        // Create queue and add jobs
        var runner1 = new MockJobRunner();
        using var queue1 = new RenderQueue(runner1, store, template, plan);
        queue1.AddJobsFromPlan(plan);
        
        var originalJobs = queue1.GetJobs();
        Assert.Equal(2, originalJobs.Count);

        queue1.Dispose();

        // Create new queue with same store - should load previous state
        var runner2 = new MockJobRunner();
        using var queue2 = new RenderQueue(runner2, store, template, plan);
        
        var restoredJobs = queue2.GetJobs();
        Assert.Equal(2, restoredJobs.Count);
        Assert.Equal(originalJobs[0].Id, restoredJobs[0].Id);
        Assert.Equal(originalJobs[1].Id, restoredJobs[1].Id);
    }

    [Fact]
    public async Task RunAsync_ResetsRunningJobsOnLoad()
    {
        var queuePath = Path.Combine(_tempDir, "queue-running.json");
        var store = new JobStore(queuePath);
        var (template, plan) = CreatePlan(1);
        
        // Create a job and save it as Running
        var jobs = new List<RenderJobItem>
        {
            new()
            {
                Id = "job1",
                JobIndex = 0,
                DriverPath = plan.Jobs[0].DriverPath,
                OutputPath = plan.Jobs[0].OutputPath,
                DurationSeconds = plan.Jobs[0].DurationSeconds,
                Status = JobStatus.Running,
                Progress = 0.5,
                StartedAt = DateTime.UtcNow
            }
        };
        store.Save(jobs);

        // Load queue - Running job should be reset to Pending
        var runner = new MockJobRunner();
        using var queue = new RenderQueue(runner, store, template, plan);
        
        var loadedJobs = queue.GetJobs();
        Assert.Single(loadedJobs);
        Assert.Equal(JobStatus.Pending, loadedJobs[0].Status);
        Assert.Equal(0.0, loadedJobs[0].Progress);
        Assert.Null(loadedJobs[0].StartedAt);
    }

    [Fact]
    public async Task GetProgress_ReturnsAccurateProgress()
    {
        var (queue, runner, plan) = CreateQueue(4);
        runner.FailCondition = job => job.JobIndex == 2 ? new InvalidOperationException("Test") : null;
        
        queue.AddJobsFromPlan(plan);
        await queue.RunAsync(maxParallel: 1);

        var progress = queue.GetProgress();
        Assert.Equal(4, progress.TotalJobs);
        Assert.Equal(3, progress.CompletedJobs);  // 3 done/skipped
        Assert.Equal(1, progress.FailedJobs);
        Assert.Equal(0, progress.RunningJobs);
        Assert.Equal(0.75, progress.OverallProgress);  // 3/4
    }

    [Fact]
    public async Task ProgressChanged_EventRaised()
    {
        var (queue, runner, plan) = CreateQueue(2);
        
        int progressEvents = 0;
        queue.ProgressChanged += (s, p) => progressEvents++;
        
        queue.AddJobsFromPlan(plan);
        await queue.RunAsync(maxParallel: 1);

        Assert.True(progressEvents > 0, "ProgressChanged event should have been raised");
    }

    [Fact]
    public async Task JobStatusChanged_EventRaised()
    {
        var (queue, runner, plan) = CreateQueue(2);
        
        var statusChanges = new List<(string JobId, JobStatus Status)>();
        queue.JobStatusChanged += (s, job) => statusChanges.Add((job.Id, job.Status));
        
        queue.AddJobsFromPlan(plan);
        await queue.RunAsync(maxParallel: 1);

        Assert.True(statusChanges.Count >= 4, $"Expected at least 4 status changes (2 Running + 2 Done), got {statusChanges.Count}");
        Assert.Contains(statusChanges, sc => sc.Status == JobStatus.Running);
        Assert.Contains(statusChanges, sc => sc.Status == JobStatus.Done);
    }

    private (RenderQueue Queue, MockJobRunner Runner, PlanResult Plan) CreateQueue(int jobCount)
    {
        var (template, plan) = CreatePlan(jobCount);
        var runner = new MockJobRunner();
        var queuePath = Path.Combine(_tempDir, $"queue-{Guid.NewGuid():N}.json");
        var store = new JobStore(queuePath);
        var queue = new RenderQueue(runner, store, template, plan);
        
        return (queue, runner, plan);
    }

    private (Template Template, PlanResult Plan) CreatePlan(int jobCount)
    {
        var template = TemplateDefaults.CreateCo139();
        var defaultPreset = template.StylePresets.FirstOrDefault() ?? new StylePreset { Id = "default" };
        
        var jobs = new List<RenderJobPlan>();
        for (int i = 0; i < jobCount; i++)
        {
            var driverPath = Path.Combine(_tempDir, $"driver{i}.mp4");
            var outputPath = Path.Combine(_tempDir, $"output{i}.mp4");
            
            jobs.Add(new RenderJobPlan(
                Index: i,
                DriverPath: driverPath,
                DriverStem: $"driver{i}",
                SubPath: null,
                DurationSeconds: 10.0 + i,
                Backgrounds: new List<BackgroundSegment>(),
                AvatarPath: null,
                WavePath: null,
                Side: "left",
                StylePreset: defaultPreset,
                OutputPath: outputPath
            ));
        }

        var plan = new PlanResult(jobs, new List<PlanWarning>());
        return (template, plan);
    }
}
