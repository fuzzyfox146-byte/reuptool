using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Queue;

/// <summary>
/// Progress information for the overall queue.
/// </summary>
public sealed class QueueProgress
{
    public int TotalJobs { get; init; }
    public int CompletedJobs { get; init; }
    public int FailedJobs { get; init; }
    public int RunningJobs { get; init; }
    public double OverallProgress { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}

/// <summary>
/// Manages a queue of render jobs with parallel execution, pause/resume, and retry.
/// </summary>
public sealed class RenderQueue : IDisposable
{
    private readonly IJobRunner _runner;
    private readonly JobStore _store;
    private readonly Template _template;
    private readonly Dictionary<string, RenderJobPlan> _planLookup;
    private readonly List<RenderJobItem> _jobs;
    
    private bool _isPaused;
    private bool _isDisposed;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when queue progress changes.
    /// </summary>
    public event EventHandler<QueueProgress>? ProgressChanged;

    /// <summary>
    /// Event raised when a job's status changes.
    /// </summary>
    public event EventHandler<RenderJobItem>? JobStatusChanged;

    public RenderQueue(IJobRunner runner, JobStore store, Template template, PlanResult plan)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _template = template ?? throw new ArgumentNullException(nameof(template));
        _planLookup = plan.Jobs.ToDictionary(j => j.DriverPath, j => j);
        _jobs = _store.Load();
    }

    /// <summary>
    /// Adds jobs from the plan to the queue.
    /// </summary>
    public void AddJobsFromPlan(PlanResult plan, bool skipExisting = false)
    {
        lock (_lock)
        {
            foreach (var jobPlan in plan.Jobs)
            {
                if (skipExisting && File.Exists(jobPlan.OutputPath))
                {
                    var item = RenderJobItem.FromPlan(jobPlan);
                    item.Status = JobStatus.Skipped;
                    _jobs.Add(item);
                    continue;
                }

                var existing = _jobs.FirstOrDefault(j => j.DriverPath == jobPlan.DriverPath && j.Status == JobStatus.Pending);
                if (existing == null)
                {
                    _jobs.Add(RenderJobItem.FromPlan(jobPlan));
                }
            }
            
            SaveState();
        }
    }

    /// <summary>
    /// Starts processing the queue with the specified level of parallelism (1-3).
    /// </summary>
    public async Task RunAsync(int maxParallel = 1, CancellationToken cancellationToken = default)
    {
        if (maxParallel < 1 || maxParallel > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallel), "Max parallel must be between 1 and 3");
        }

        using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
        var tasks = new List<Task>();
        var pendingJobs = GetPendingJobs();

        foreach (var job in pendingJobs)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            
            if (cancellationToken.IsCancellationRequested)
            {
                semaphore.Release();
                break;
            }

            tasks.Add(ProcessJobAsync(job, semaphore, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        RaiseProgressChanged();
    }

    /// <summary>
    /// Pauses the queue (running jobs continue, new jobs won't start).
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            _isPaused = true;
        }
    }

    /// <summary>
    /// Resumes the queue.
    /// </summary>
    public void Resume()
    {
        lock (_lock)
        {
            _isPaused = false;
        }
    }

    /// <summary>
    /// Cancels all pending and running jobs.
    /// </summary>
    public void CancelAll()
    {
        lock (_lock)
        {
            foreach (var job in _jobs.Where(j => j.Status == JobStatus.Pending))
            {
                job.Status = JobStatus.Cancelled;
                RaiseJobStatusChanged(job);
            }
            SaveState();
        }
    }

    /// <summary>
    /// Cancels a specific job.
    /// </summary>
    public void CancelJob(string jobId)
    {
        lock (_lock)
        {
            var job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job != null && (job.Status == JobStatus.Pending || job.Status == JobStatus.Running))
            {
                job.Status = JobStatus.Cancelled;
                RaiseJobStatusChanged(job);
                SaveState();
            }
        }
    }

    /// <summary>
    /// Retries a failed job.
    /// </summary>
    public void RetryJob(string jobId)
    {
        lock (_lock)
        {
            var job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job != null && job.Status == JobStatus.Failed)
            {
                job.Status = JobStatus.Pending;
                job.Progress = 0.0;
                job.ErrorMessage = null;
                job.LogTail = null;
                job.StartedAt = null;
                job.FinishedAt = null;
                RaiseJobStatusChanged(job);
                SaveState();
            }
        }
    }

    /// <summary>
    /// Gets the current queue progress.
    /// </summary>
    public QueueProgress GetProgress()
    {
        lock (_lock)
        {
            var total = _jobs.Count;
            var completed = _jobs.Count(j => j.Status == JobStatus.Done || j.Status == JobStatus.Skipped);
            var failed = _jobs.Count(j => j.Status == JobStatus.Failed);
            var running = _jobs.Count(j => j.Status == JobStatus.Running);
            
            var overallProgress = total > 0 ? (double)completed / total : 1.0;

            return new QueueProgress
            {
                TotalJobs = total,
                CompletedJobs = completed,
                FailedJobs = failed,
                RunningJobs = running,
                OverallProgress = overallProgress,
                EstimatedTimeRemaining = null  // TODO: calculate based on running jobs
            };
        }
    }

    /// <summary>
    /// Gets all jobs in the queue.
    /// </summary>
    public IReadOnlyList<RenderJobItem> GetJobs()
    {
        lock (_lock)
        {
            return _jobs.ToList();
        }
    }

    private async Task ProcessJobAsync(RenderJobItem job, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        try
        {
            // Wait if paused
            while (_isPaused && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // Check if job was cancelled before we started (e.g. by CancelAll)
            lock (_lock)
            {
                if (job.Status == JobStatus.Cancelled)
                {
                    return;
                }
            }

            UpdateJobStatus(job, JobStatus.Running);
            job.StartedAt = DateTime.UtcNow;
            SaveState();

            var plan = _planLookup[job.DriverPath];
            var progress = new Progress<double>(p =>
            {
                job.Progress = p;
                RaiseProgressChanged();
            });

            await _runner.RenderAsync(job, _template, plan, cancellationToken, progress).ConfigureAwait(false);

            job.Progress = 1.0;
            job.FinishedAt = DateTime.UtcNow;
            UpdateJobStatus(job, JobStatus.Done);
        }
        catch (OperationCanceledException)
        {
            UpdateJobStatus(job, JobStatus.Cancelled);
            job.FinishedAt ??= DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            job.ErrorMessage = FlattenMessages(ex);
            job.LogTail = ex.ToString();
            job.FinishedAt = DateTime.UtcNow;
            UpdateJobStatus(job, JobStatus.Failed);
        }
        finally
        {
            SaveState();
            semaphore.Release();
            CleanupTempFiles(job);
        }
    }

    private List<RenderJobItem> GetPendingJobs()
    {
        lock (_lock)
        {
            return _jobs.Where(j => j.Status == JobStatus.Pending).ToList();
        }
    }

    private void UpdateJobStatus(RenderJobItem job, JobStatus status)
    {
        lock (_lock)
        {
            job.Status = status;
            RaiseJobStatusChanged(job);
        }
    }

    private void SaveState()
    {
        _store.Save(_jobs);
    }

    private void CleanupTempFiles(RenderJobItem job)
    {
        try
        {
            var partFile = job.OutputPath + ".part";
            if (File.Exists(partFile))
            {
                File.Delete(partFile);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private void RaiseProgressChanged()
    {
        ProgressChanged?.Invoke(this, GetProgress());
    }

    private void RaiseJobStatusChanged(RenderJobItem job)
    {
        JobStatusChanged?.Invoke(this, job);
    }

    private static string FlattenMessages(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                (parts.Count == 0 || parts[^1] != current.Message))
            {
                parts.Add(current.Message);
            }
        }

        return string.Join(Environment.NewLine, parts);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
        }
    }
}
