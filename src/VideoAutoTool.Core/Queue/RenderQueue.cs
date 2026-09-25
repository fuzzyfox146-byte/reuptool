using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Render;
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
    private readonly Dictionary<string, RenderJobPlan> _plansByDriver;
    private readonly Dictionary<string, RenderJobPlan> _plansByJobId = new();
    private readonly Dictionary<string, Template> _templateByIntake = new();
    private readonly Dictionary<string, CancellationTokenSource> _jobCancels = new();
    private readonly List<string> _intakeOrder = new();
    private readonly List<RenderJobItem> _jobs;

    private bool _isPaused;
    private bool _cancelRequested;
    private bool _isDisposed;
    private int _parallelLimit = 1;
    private readonly Dictionary<string, int> _startedAtLimit = new();
    private int _batchSize;
    private int _lastBatchSize;
    private int _intakeSequence;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when queue progress changes.
    /// </summary>
    public event EventHandler<QueueProgress>? ProgressChanged;

    /// <summary>
    /// Event raised when a job's status changes.
    /// </summary>
    public event EventHandler<RenderJobItem>? JobStatusChanged;

    /// <summary>
    /// Raised when a full disk forces the queue to run fewer videos at once and retry.
    /// </summary>
    public event EventHandler<string>? Notice;

    public RenderQueue(IJobRunner runner, JobStore store, Template template, PlanResult plan)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _template = template ?? throw new ArgumentNullException(nameof(template));
        var snapshot = store.LoadSnapshot();
        _jobs = snapshot.Jobs;
        _plansByDriver = plan.Jobs
            .GroupBy(j => j.DriverPath)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
        foreach (var pair in snapshot.PlansByJobId)
        {
            _plansByJobId[pair.Key] = pair.Value;
            _plansByDriver[pair.Value.DriverPath] = pair.Value;
        }

        foreach (var pair in snapshot.TemplateByIntake)
        {
            _templateByIntake[pair.Key] = pair.Value;
        }

        _intakeOrder.AddRange(snapshot.IntakeOrder);
        if (_intakeOrder.Count == 0)
        {
            _intakeOrder.AddRange(
                snapshot.Jobs
                    .Select(j => j.IntakeId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal));
        }

        _intakeSequence = Math.Max(snapshot.IntakeSequence, MaxIntakeSequence(_intakeOrder));
        _batchSize = snapshot.BatchSize;
        _lastBatchSize = snapshot.LastBatchSize > 0 ? snapshot.LastBatchSize : snapshot.BatchSize;
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
                    item.Promoted = true;
                    _plansByJobId[item.Id] = jobPlan;
                    _jobs.Add(item);
                    continue;
                }

                var existing = _jobs.FirstOrDefault(j => j.DriverPath == jobPlan.DriverPath && j.Status == JobStatus.Pending);
                if (existing == null)
                {
                    var item = RenderJobItem.FromPlan(jobPlan);
                    item.Promoted = true;
                    _plansByJobId[item.Id] = jobPlan;
                    _jobs.Add(item);
                }
            }
            
            SaveState();
        }
    }

    /// <summary>
    /// Adds one source while a render may already be running.
    /// The first <paramref name="batchSize"/> pending jobs of this source enter the main queue.
    /// The rest stay waiting until the main queue has no pending job left.
    /// </summary>
    public string EnqueueIntake(
        string sourceName,
        Template template,
        PlanResult plan,
        int batchSize,
        bool skipExisting = false,
        double? clipSeconds = null)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (string.IsNullOrWhiteSpace(sourceName)) sourceName = "Nguồn";

        string id;
        lock (_lock)
        {
            if (_batchSize == 0)
            {
                _batchSize = Math.Clamp(batchSize, 1, 99);
                _lastBatchSize = _batchSize;
            }

            id = (++_intakeSequence).ToString("D2");
            _intakeOrder.Add(id);
            _templateByIntake[id] = template;

            var fresh = new List<RenderJobItem>();
            foreach (var jobPlan in plan.Jobs)
            {
                var item = RenderJobItem.FromPlan(jobPlan);
                item.IntakeId = id;
                item.IntakeName = sourceName;
                item.ClipSeconds = clipSeconds;
                item.Promoted = false;
                if (skipExisting && File.Exists(jobPlan.OutputPath))
                {
                    item.Status = JobStatus.Skipped;
                }

                _plansByJobId[item.Id] = jobPlan;
                _jobs.Add(item);
                if (item.Status == JobStatus.Pending)
                {
                    fresh.Add(item);
                }
            }

            PromoteLocked(fresh, _batchSize);
            SaveState();
        }

        RaiseProgressChanged();
        return id;
    }

    public bool HasOpenWork
    {
        get
        {
            lock (_lock)
            {
                return HasOpenWorkLocked();
            }
        }
    }

    public IReadOnlyList<RenderJobItem> GetMainJobs()
    {
        lock (_lock)
        {
            return _jobs.Where(j => j.Promoted && j.Status != JobStatus.Skipped).ToList();
        }
    }

    public IReadOnlyList<RenderJobItem> GetWaitingJobs()
    {
        lock (_lock)
        {
            return _jobs.Where(j => !j.Promoted && j.Status == JobStatus.Pending).ToList();
        }
    }

    /// <summary>
    /// Starts processing the queue with the specified level of parallelism (1-3).
    /// Workers keep pulling until every job is finished, including sources added after start.
    /// </summary>
    public async Task RunAsync(int maxParallel = 1, CancellationToken cancellationToken = default)
    {
        if (maxParallel < 1 || maxParallel > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallel), "Max parallel must be between 1 and 3");
        }

        _cancelRequested = false;
        lock (_lock)
        {
            _parallelLimit = maxParallel;
        }

        var workers = new Task[maxParallel];
        for (var i = 0; i < maxParallel; i++)
        {
            workers[i] = WorkerAsync(cancellationToken);
        }

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        finally
        {
            lock (_lock)
            {
                if (!HasOpenWorkLocked())
                {
                    _batchSize = 0;
                }
            }

            RaiseProgressChanged();
        }
    }

    /// <summary>
    /// Stops ffmpeg on running jobs and holds them so Resume renders those jobs again.
    /// Jobs not started yet stay pending and do not start while paused.
    /// </summary>
    public void Pause()
    {
        CancellationTokenSource[] tokens;
        lock (_lock)
        {
            _isPaused = true;
            tokens = _jobCancels.Values.ToArray();
        }

        CancelTokens(tokens);
    }

    /// <summary>
    /// Resumes held jobs and lets workers take the next main-queue job.
    /// </summary>
    public void Resume()
    {
        lock (_lock)
        {
            _isPaused = false;
            foreach (var job in _jobs)
            {
                if (job.Status != JobStatus.Paused) continue;
                job.Status = JobStatus.Pending;
                job.Promoted = true;
                job.Progress = 0;
                job.StartedAt = null;
                job.FinishedAt = null;
                job.ErrorMessage = null;
                RaiseJobStatusChanged(job);
            }

            SaveState();
        }
    }

    /// <summary>
    /// Cancels pending, paused, and running jobs.
    /// </summary>
    public void CancelAll()
    {
        CancellationTokenSource[] tokens;
        lock (_lock)
        {
            _cancelRequested = true;
            _isPaused = false;
            foreach (var job in _jobs)
            {
                if (job.Status is not (JobStatus.Pending or JobStatus.Paused)) continue;
                job.Status = JobStatus.Cancelled;
                job.FinishedAt ??= DateTime.UtcNow;
                RaiseJobStatusChanged(job);
            }

            tokens = _jobCancels.Values.ToArray();
            SaveState();
        }

        CancelTokens(tokens);
    }

    /// <summary>
    /// Puts cancelled and failed jobs back to pending so the queue can run again.
    /// Finished jobs stay finished. Returns how many jobs were reset.
    /// </summary>
    public int ResetToContinue()
    {
        lock (_lock)
        {
            _cancelRequested = false;
            _isPaused = false;
            if (_batchSize == 0 && _lastBatchSize > 0)
            {
                _batchSize = _lastBatchSize;
            }

            var reset = 0;
            foreach (var job in _jobs)
            {
                if (job.Status is not (JobStatus.Cancelled or JobStatus.Failed))
                {
                    continue;
                }

                job.Status = JobStatus.Pending;
                job.Progress = 0;
                job.ErrorMessage = null;
                job.LogTail = null;
                job.StartedAt = null;
                job.FinishedAt = null;
                CleanupTempFiles(job);
                reset++;
                RaiseJobStatusChanged(job);
            }

            SaveState();
            return reset;
        }
    }

    /// <summary>
    /// Removes every job and intake so a new source can be added on an empty queue.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var job in _jobs)
            {
                CleanupTempFiles(job);
            }

            _jobs.Clear();
            _intakeOrder.Clear();
            _templateByIntake.Clear();
            _plansByJobId.Clear();
            _batchSize = 0;
            _lastBatchSize = 0;
            _intakeSequence = 0;
            _cancelRequested = false;
            _isPaused = false;
            SaveState();
        }
    }

    /// <summary>
    /// Cancels a specific job.
    /// </summary>
    public void CancelJob(string jobId)
    {
        CancellationTokenSource? token = null;
        lock (_lock)
        {
            var job = _jobs.FirstOrDefault(j => j.Id == jobId);
            if (job != null && job.Status is JobStatus.Pending or JobStatus.Running or JobStatus.Paused)
            {
                job.Status = JobStatus.Cancelled;
                job.FinishedAt ??= DateTime.UtcNow;
                if (_jobCancels.TryGetValue(jobId, out var running))
                {
                    token = running;
                }

                RaiseJobStatusChanged(job);
                SaveState();
            }
        }

        if (token is not null)
        {
            CancelTokens([token]);
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

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_cancelRequested)
        {
            RenderJobItem? job = null;
            lock (_lock)
            {
                if (!_isPaused)
                {
                    if (!HasPromotedPendingLocked())
                    {
                        PromoteNextWaveLocked();
                    }

                    job = _jobs.FirstOrDefault(j => j.Promoted && j.Status == JobStatus.Pending);
                    var running = _jobs.Count(j => j.Status == JobStatus.Running);
                    if (job is not null && running >= _parallelLimit)
                    {
                        job = null;
                    }

                    if (job is not null)
                    {
                        job.Status = JobStatus.Running;
                        job.StartedAt = DateTime.UtcNow;
                        job.Progress = 0;
                        _startedAtLimit[job.Id] = _parallelLimit;
                        if (!HasPromotedPendingLocked())
                        {
                            PromoteNextWaveLocked();
                        }

                        RaiseJobStatusChanged(job);
                    }
                }
            }

            if (job is null)
            {
                if (!HasOpenWork || _cancelRequested)
                {
                    break;
                }

                try
                {
                    await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            await ProcessReservedJobAsync(job, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessReservedJobAsync(RenderJobItem job, CancellationToken runToken)
    {
        var jobCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
        lock (_lock)
        {
            if (_cancelRequested || runToken.IsCancellationRequested || job.Status == JobStatus.Cancelled)
            {
                job.Status = JobStatus.Cancelled;
                job.FinishedAt ??= DateTime.UtcNow;
                RaiseJobStatusChanged(job);
                jobCts.Dispose();
                return;
            }

            if (_isPaused)
            {
                job.Progress = 0;
                job.StartedAt = null;
                job.FinishedAt = null;
                job.Status = JobStatus.Paused;
                RaiseJobStatusChanged(job);
                jobCts.Dispose();
                return;
            }

            _jobCancels[job.Id] = jobCts;
        }

        var lastProgressTick = 0L;
        try
        {
            var (template, plan) = Resolve(job);
            var progress = new Progress<double>(p =>
            {
                job.Progress = p;
                var now = Environment.TickCount64;
                if (now - lastProgressTick < 200 && p < 1)
                {
                    return;
                }

                lastProgressTick = now;
                RaiseProgressChanged();
            });

            await _runner.RenderAsync(job, template, plan, jobCts.Token, progress).ConfigureAwait(false);

            job.Progress = 1.0;
            job.FinishedAt = DateTime.UtcNow;
            UpdateJobStatus(job, JobStatus.Done);
        }
        catch (OperationCanceledException)
        {
            if (_isPaused && !runToken.IsCancellationRequested && !_cancelRequested)
            {
                job.Progress = 0;
                job.StartedAt = null;
                job.FinishedAt = null;
                UpdateJobStatus(job, JobStatus.Paused);
            }
            else
            {
                job.FinishedAt ??= DateTime.UtcNow;
                UpdateJobStatus(job, JobStatus.Cancelled);
            }
        }
        catch (Exception ex)
        {
            if (TryRequeueAfterResourcePressure(job, ex))
            {
                return;
            }

            job.ErrorMessage = FlattenMessages(ex);
            job.LogTail = ex.ToString();
            job.FinishedAt = DateTime.UtcNow;
            UpdateJobStatus(job, JobStatus.Failed);
        }
        finally
        {
            lock (_lock)
            {
                _jobCancels.Remove(job.Id);
            }

            jobCts.Dispose();
            SaveState();
            CleanupTempFiles(job);
        }
    }

    private void PromoteLocked(IReadOnlyList<RenderJobItem> waitingInOrder, int count)
    {
        foreach (var job in waitingInOrder.Take(count))
        {
            job.Promoted = true;
            RaiseJobStatusChanged(job);
        }
    }

    private void PromoteNextWaveLocked()
    {
        if (_batchSize <= 0 || HasPromotedPendingLocked())
        {
            return;
        }

        foreach (var intakeId in _intakeOrder)
        {
            var waiting = _jobs
                .Where(j => j.IntakeId == intakeId && !j.Promoted && j.Status == JobStatus.Pending)
                .Take(_batchSize)
                .ToList();
            foreach (var job in waiting)
            {
                job.Promoted = true;
                RaiseJobStatusChanged(job);
            }
        }
    }

    private bool HasPromotedPendingLocked() =>
        _jobs.Any(j => j.Promoted && j.Status == JobStatus.Pending);

    private bool HasOpenWorkLocked() =>
        _jobs.Any(j => j.Status is JobStatus.Pending or JobStatus.Running or JobStatus.Paused);

    private (Template Template, RenderJobPlan Plan) Resolve(RenderJobItem job)
    {
        var template = _template;
        if (!string.IsNullOrEmpty(job.IntakeId) &&
            _templateByIntake.TryGetValue(job.IntakeId, out var intakeTemplate))
        {
            template = intakeTemplate;
        }

        if (_plansByJobId.TryGetValue(job.Id, out var plan))
        {
            return (template, plan);
        }

        if (_plansByDriver.TryGetValue(job.DriverPath, out var byDriver))
        {
            return (template, byDriver);
        }

        throw new InvalidOperationException(
            $"Missing render plan for job {job.Id} ({job.DriverPath}). Re-add the source to the queue.");
    }

    private static void CancelTokens(IEnumerable<CancellationTokenSource> tokens)
    {
        foreach (var token in tokens)
        {
            try
            {
                token.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Job already finished.
            }
        }
    }

    private bool TryRequeueAfterResourcePressure(RenderJobItem job, Exception ex)
    {
        var noSpace = IsNoSpace(ex);
        var locked = IsFileLocked(ex);
        if (!noSpace && !locked)
        {
            return false;
        }

        string? notice = null;
        lock (_lock)
        {
            var startedAt = _startedAtLimit.GetValueOrDefault(job.Id, _parallelLimit);
            if (startedAt <= 1 && _parallelLimit <= 1)
            {
                return false;
            }

            var previous = _parallelLimit;
            if (_parallelLimit > 1)
            {
                _parallelLimit--;
            }

            job.ErrorMessage = null;
            job.LogTail = null;
            job.FinishedAt = null;
            job.StartedAt = null;
            job.Progress = 0;
            job.Status = JobStatus.Pending;
            RaiseJobStatusChanged(job);
            notice = noSpace
                ? previous == _parallelLimit
                    ? "Hết chỗ trống trên đĩa. Các video còn lại sẽ render từng cái một."
                    : $"Hết chỗ trống trên đĩa khi render {previous} video cùng lúc. Chuyển còn {_parallelLimit} video và render lại."
                : previous == _parallelLimit
                    ? "File xuất đang bị khóa. Các video còn lại sẽ render từng cái một."
                    : $"File xuất đang bị khóa khi render {previous} video cùng lúc. Chuyển còn {_parallelLimit} video và render lại.";
        }

        Notice?.Invoke(this, notice);
        return true;
    }

    private static bool IsFileLocked(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("because it is being used", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNoSpace(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("No space left on device", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("not enough space", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateJobStatus(RenderJobItem job, JobStatus status)
    {
        lock (_lock)
        {
            job.Status = status;
            RaiseJobStatusChanged(job);
        }
    }

    public void Persist()
    {
        lock (_lock)
        {
            SaveState();
        }
    }

    private void SaveState()
    {
        _store.SaveSnapshot(new QueueSnapshot
        {
            Jobs = _jobs.ToList(),
            PlansByJobId = new Dictionary<string, RenderJobPlan>(_plansByJobId, StringComparer.Ordinal),
            TemplateByIntake = new Dictionary<string, Template>(_templateByIntake, StringComparer.Ordinal),
            IntakeOrder = _intakeOrder.ToList(),
            IntakeSequence = _intakeSequence,
            BatchSize = _batchSize,
            LastBatchSize = _lastBatchSize
        });
    }

    private static int MaxIntakeSequence(IEnumerable<string> ids)
    {
        var max = 0;
        foreach (var id in ids)
        {
            if (int.TryParse(id, out var n) && n > max)
            {
                max = n;
            }
        }

        return max;
    }

    private void CleanupTempFiles(RenderJobItem job)
    {
        try
        {
            PartFile.TryDelete(job.OutputPath + ".part");
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
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _cancelRequested = true;
        CancellationTokenSource[] tokens;
        lock (_lock)
        {
            tokens = _jobCancels.Values.ToArray();
        }

        CancelTokens(tokens);
    }
}
