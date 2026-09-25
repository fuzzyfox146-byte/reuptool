using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoTool.App.Services;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Queue;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class QueueViewModel : ObservableObject
{
    private readonly JobRendererAdapter _adapter;
    private readonly DispatcherTimer _elapsedTimer;
    private readonly Stopwatch _elapsed = new();
    private RenderQueue? _queue;
    private JobStore? _store;
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _syncTimer;
    private bool _loopRunning;
    private int _lastParallel = 2;
    private long _lastUiProgress;

    private readonly EncoderSelector _encoders;

    public QueueViewModel(JobRendererAdapter adapter, EncoderSelector encoders)
    {
        _adapter = adapter;
        _encoders = encoders;
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) => RefreshElapsed();
        _ = DescribeEncoderAsync();
    }

    private async Task DescribeEncoderAsync()
    {
        try
        {
            var settings = await _encoders.SelectAsync(preferNvenc: true).ConfigureAwait(true);
            EncoderLabel = settings.Encoder == VideoEncoderKind.H264Nvenc
                ? $"NVENC GPU · decode {settings.HwAccel ?? "CPU"}"
                : $"libx264 CPU · decode {settings.HwAccel ?? "CPU"}";
        }
        catch
        {
            EncoderLabel = "FFmpeg (chưa dò được GPU)";
        }
    }

    public ObservableCollection<QueueJobRowViewModel> Jobs { get; } = new();

    public ObservableCollection<IntakeGroupViewModel> Intakes { get; } = new();

    public ObservableCollection<string> LogLines { get; } = new();

    public bool CanEditBatchSize => !IsRunning;

    [ObservableProperty]
    private int _jobCount;

    [ObservableProperty]
    private string _elapsedText = "00:00:00";

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _status = "Chưa encode.";

    [ObservableProperty]
    private string _encoderLabel = "FFmpeg (CPU/GPU tự chọn)";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private QueueJobRowViewModel? _selectedJob;

    public bool CanPause => IsRunning && !IsPaused;

    public bool CanResume => IsRunning && IsPaused;

    public event EventHandler? BusyChanged;

    public Task EnqueueAsync(
        string sourceName,
        Template template,
        PlanResult plan,
        RenderRequest request,
        int maxParallel,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        EnsureQueue(template, plan);
        var queue = _queue ?? throw new InvalidOperationException("Queue was not created.");

        var clipSeconds = request.Mode == RenderMode.Clip ? request.ClipSeconds : null;
        var id = queue.EnqueueIntake(
            sourceName,
            template,
            plan,
            batchSize,
            skipExisting: request.Mode == RenderMode.Full && template.Output.SkipExisting,
            clipSeconds: clipSeconds);

        SyncFromQueue();
        SelectedJob ??= Jobs.FirstOrDefault();
        Status = request.Mode == RenderMode.Clip
            ? $"Đã thêm nguồn {id} · {sourceName} (render thử)."
            : $"Đã thêm nguồn {id} · {sourceName}: {plan.Jobs.Count} video.";
        AppendLog($"{Status} Hàng chính nhận tối đa {Math.Clamp(batchSize, 1, 99)} video mỗi lượt.");
        _lastParallel = Math.Clamp(maxParallel, 1, 3);
        StartLoop(maxParallel, cancellationToken);
        return Task.CompletedTask;
    }

    private void StartLoop(int maxParallel, CancellationToken cancellationToken)
    {
        if (_loopRunning || _queue is null)
        {
            return;
        }

        _loopRunning = true;
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsRunning = true;
        IsPaused = false;
        BusyChanged?.Invoke(this, EventArgs.Empty);
        NotifyQueueCommands();
        if (!_elapsed.IsRunning)
        {
            _elapsed.Restart();
        }

        _elapsedTimer.Start();
        EnsureSyncTimer();
        RefreshElapsed();
        var parallel = Math.Clamp(maxParallel, 1, 3);
        var token = _cts.Token;
        _ = RunLoopAsync(parallel, token);
    }

    private async Task RunLoopAsync(int maxParallel, CancellationToken token)
    {
        var restarted = false;
        try
        {
            AppendLog($"Hàng đợi chạy — {maxParallel} video cùng lúc.");
            await _queue!.RunAsync(maxParallel, token).ConfigureAwait(true);
            if (_queue.HasOpenWork && !token.IsCancellationRequested)
            {
                _loopRunning = false;
                restarted = true;
                StartLoop(maxParallel, CancellationToken.None);
                return;
            }

            var progress = _queue.GetProgress();
            Status = progress.FailedJobs > 0
                ? $"Xong: {progress.CompletedJobs}/{progress.TotalJobs} thành công, {progress.FailedJobs} lỗi."
                : $"Xong: {progress.CompletedJobs}/{progress.TotalJobs} job.";
            AppendLog(Status);
        }
        catch (OperationCanceledException)
        {
            Status = "Đã hủy hàng đợi.";
            AppendLog(Status);
        }
        catch (Exception ex)
        {
            Status = $"Lỗi hàng đợi: {ex.Message}";
            AppendLog(Status);
        }
        finally
        {
            if (!restarted)
            {
                _loopRunning = false;
                _elapsed.Stop();
                _elapsedTimer.Stop();
                _syncTimer?.Stop();
                RefreshElapsed();
                IsRunning = false;
                IsPaused = false;
                BusyChanged?.Invoke(this, EventArgs.Empty);
                NotifyQueueCommands();
                SyncFromQueue();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        _queue?.Pause();
        IsPaused = true;
        Status = "Đã tạm dừng. Video đang encode bị dừng và sẽ render lại từ đầu khi bấm Tiếp tục.";
        AppendLog(Status);
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume()
    {
        _queue?.Resume();
        IsPaused = false;
        Status = "Tiếp tục hàng đợi.";
        AppendLog(Status);
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _queue?.CancelAll();
        _cts?.Cancel();
        Status = "Đang hủy…";
        AppendLog("Hủy hàng đợi.");
    }

    private bool CanCancel() => IsRunning;

    public void RestorePersisted(int maxParallel = 2)
    {
        if (_queue is not null)
        {
            return;
        }

        _lastParallel = Math.Clamp(maxParallel, 1, 3);
        var store = new JobStore(PersistentQueuePath);
        var snapshot = store.LoadSnapshot();
        if (snapshot.Jobs.Count == 0)
        {
            return;
        }

        var template = snapshot.TemplateByIntake.Values.FirstOrDefault() ?? new Template();
        EnsureQueue(template, new PlanResult([], []));
        SyncFromQueue();
        var pending = snapshot.Jobs.Count(job => job.Status is JobStatus.Pending or JobStatus.Paused);
        var done = snapshot.Jobs.Count(job => job.Status == JobStatus.Done);
        Status = $"Đã khôi phục hàng đợi: {done} xong, {pending} chờ. Bấm Render tiếp để chạy phần còn lại.";
        AppendLog(Status);
        NotifyQueueCommands();
        SelectedJob ??= Jobs.FirstOrDefault();
    }

    public void Persist()
    {
        _queue?.Persist();
    }

    private void EnsureQueue(Template template, PlanResult plan)
    {
        if (_queue is not null)
        {
            return;
        }

        _store ??= new JobStore(PersistentQueuePath);
        _queue = new RenderQueue(_adapter, _store, template, plan);
        _queue.ProgressChanged += OnProgressChanged;
        _queue.JobStatusChanged += OnJobStatusChanged;
        _queue.Notice += OnQueueNotice;
    }

    private static string PersistentQueuePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ProductEdition.DataFolder,
        "queue.json");

    private bool CanRenderAgain =>
        !IsRunning && _queue is not null && _queue.GetJobs().Any(job =>
            job.Status is JobStatus.Cancelled or JobStatus.Failed or JobStatus.Pending or JobStatus.Paused);

    private bool CanClearQueue =>
        !IsRunning && _queue is not null && _queue.GetJobs().Count > 0;

    [RelayCommand(CanExecute = nameof(CanRenderAgain))]
    private void RenderAgain()
    {
        if (_queue is null) return;
        var reset = _queue.ResetToContinue();
        SyncFromQueue();
        var pending = _queue.GetJobs().Count(job => job.Status == JobStatus.Pending);
        Status = reset > 0
            ? $"Render tiếp {reset} video đã hủy hoặc lỗi. Video đã xong giữ nguyên."
            : $"Render tiếp {pending} video đang chờ. Video đã xong giữ nguyên.";
        AppendLog(Status);
        NotifyQueueCommands();
        StartLoop(_lastParallel, CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanClearQueue))]
    private void ClearQueue()
    {
        _queue?.Clear();
        _queue?.Dispose();
        _queue = null;
        Jobs.Clear();
        Intakes.Clear();
        SelectedJob = null;
        OverallProgress = 0;
        JobCount = 0;
        _elapsed.Reset();
        ElapsedText = "00:00:00";
        Status = "Đã xóa hàng đợi. Thêm nguồn mới để render.";
        AppendLog(Status);
        NotifyQueueCommands();
    }

    private bool CanCopySelectedDetails() => SelectedJob is not null;

    [RelayCommand(CanExecute = nameof(CanCopySelectedDetails))]
    private void CopySelectedDetails()
    {
        if (SelectedJob is null) return;
        Clipboard.SetText(SelectedJob.DetailBody);
        Status = "Đã copy chi tiết job (lỗi + đường dẫn) vào clipboard.";
        AppendLog("Đã copy chi tiết job vào clipboard.");
    }

    partial void OnSelectedJobChanged(QueueJobRowViewModel? value) =>
        CopySelectedDetailsCommand.NotifyCanExecuteChanged();

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanEditBatchSize));
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RenderAgainCommand.NotifyCanExecuteChanged();
        ClearQueueCommand.NotifyCanExecuteChanged();
    }

    private void NotifyQueueCommands()
    {
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanEditBatchSize));
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RenderAgainCommand.NotifyCanExecuteChanged();
        ClearQueueCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
    }

    private void OnProgressChanged(object? sender, QueueProgress progress)
    {
        var now = Environment.TickCount64;
        if (now - _lastUiProgress < 200)
        {
            return;
        }

        _lastUiProgress = now;
        RunOnUi(() =>
        {
            OverallProgress = progress.OverallProgress;
            JobCount = progress.TotalJobs;
            if (IsRunning && !IsPaused)
            {
                Status = $"Đang encode {progress.RunningJobs} — xong {progress.CompletedJobs}/{progress.TotalJobs}, lỗi {progress.FailedJobs}.";
            }

            if (_queue is null) return;
            foreach (var job in _queue.GetJobs())
            {
                if (job.Status != JobStatus.Running) continue;
                Jobs.FirstOrDefault(j => j.JobId == job.Id)?.Apply(job);
            }
        });
    }

    private void OnQueueNotice(object? sender, string message)
    {
        RunOnUi(() =>
        {
            Status = message;
            AppendLog(message);
        });
    }

    private void OnJobStatusChanged(object? sender, RenderJobItem job)
    {
        RunOnUi(() =>
        {
            SyncFromQueue();
            if (job.Status == JobStatus.Pending)
            {
                return;
            }

            var row = Jobs.FirstOrDefault(j => j.JobId == job.Id);
            var elapsed = RenderTiming.Elapsed(job.StartedAt, job.FinishedAt);
            var timing = elapsed is { } took
                ? " — " + RenderTiming.Describe(took, job.DurationSeconds)
                : "";
            var who = string.IsNullOrEmpty(job.IntakeId) ? $"#{job.JobIndex}" : $"{job.IntakeId} #{job.JobIndex}";
            var label = row?.StatusShort ?? job.Status.ToString();
            AppendLog($"{who} {label}: {Path.GetFileName(job.OutputPath)}{timing}");
            if (!string.IsNullOrWhiteSpace(job.ErrorMessage))
            {
                AppendLog($"  {job.ErrorMessage}");
                if (row is not null)
                {
                    SelectedJob = row;
                }
            }

            CopySelectedDetailsCommand.NotifyCanExecuteChanged();
        });
    }

    private void EnsureSyncTimer()
    {
        if (_syncTimer is null)
        {
            _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _syncTimer.Tick += (_, _) => SyncFromQueue();
        }

        _syncTimer.Start();
    }

    private void SyncFromQueue()
    {
        if (_queue is null) return;

        var main = _queue.GetMainJobs();
        var mainIds = new HashSet<string>(main.Select(job => job.Id));
        for (var i = Jobs.Count - 1; i >= 0; i--)
        {
            if (!mainIds.Contains(Jobs[i].JobId))
            {
                Jobs.RemoveAt(i);
            }
        }

        for (var i = 0; i < main.Count; i++)
        {
            var job = main[i];
            var existing = Jobs.FirstOrDefault(row => row.JobId == job.Id);
            if (existing is null)
            {
                Jobs.Insert(Math.Min(i, Jobs.Count), new QueueJobRowViewModel(job));
                continue;
            }

            existing.Apply(job);
            var current = Jobs.IndexOf(existing);
            if (current >= 0 && current != i)
            {
                Jobs.Move(current, Math.Min(i, Jobs.Count - 1));
            }
        }

        SyncIntakes(_queue.GetJobs());
        var progress = _queue.GetProgress();
        OverallProgress = progress.OverallProgress;
        JobCount = progress.TotalJobs;
        if (SelectedJob is not null && !Jobs.Contains(SelectedJob))
        {
            SelectedJob = Jobs.FirstOrDefault();
        }
    }

    private void SyncIntakes(IReadOnlyList<RenderJobItem> jobs)
    {
        var groups = jobs
            .Where(job => !string.IsNullOrEmpty(job.IntakeId))
            .GroupBy(job => job.IntakeId)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var group in groups)
        {
            var waiting = group
                .Where(job => !job.Promoted && job.Status == JobStatus.Pending)
                .Select(job => Path.GetFileName(job.OutputPath))
                .ToList();
            var done = group.Count(job => job.Status == JobStatus.Done);
            var row = Intakes.FirstOrDefault(intake => intake.Id == group.Key);
            if (row is null)
            {
                row = new IntakeGroupViewModel(group.Key, group.First().IntakeName);
                Intakes.Add(row);
            }

            row.Update(waiting, done, group.Count());
        }
    }

    private void AppendLog(string line)
    {
        var stamped = $"{DateTime.Now:HH:mm:ss}  {line}";
        LogLines.Add(stamped);
        while (LogLines.Count > 400)
        {
            LogLines.RemoveAt(0);
        }
    }

    private void RefreshElapsed()
    {
        var t = _elapsed.Elapsed;
        ElapsedText = t.ToString(@"hh\:mm\:ss");
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            action();
            return;
        }

        // Always defer. Status events are raised while the queue lock is held;
        // running the handler inline on the UI thread would deadlock on that lock.
        dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }
}
