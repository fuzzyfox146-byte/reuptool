using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private CancellationTokenSource? _cts;

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

    public ObservableCollection<string> LogLines { get; } = new();

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

    public async Task StartAsync(
        Template template,
        PlanResult plan,
        RenderRequest request,
        int maxParallel,
        CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Queue is already running.");
        }

        Jobs.Clear();
        LogLines.Clear();
        SelectedJob = null;
        OverallProgress = 0;
        JobCount = plan.Jobs.Count;
        Status = request.Mode == RenderMode.Clip
            ? $"Render thử {request.ClipSeconds ?? 8:0}s — {plan.Jobs.Count} job."
            : $"Render thật — {plan.Jobs.Count} job.";

        _adapter.ActiveRequest = request;
        var storePath = Path.Combine(Path.GetTempPath(), "vat-ui-queue.json");
        var store = new JobStore(storePath);
        store.Clear();

        _queue?.Dispose();
        _queue = new RenderQueue(_adapter, store, template, plan);
        _queue.ProgressChanged += OnProgressChanged;
        _queue.JobStatusChanged += OnJobStatusChanged;
        _queue.AddJobsFromPlan(plan, skipExisting: request.Mode == RenderMode.Full && template.Output.SkipExisting);

        foreach (var job in _queue.GetJobs())
        {
            Jobs.Add(new QueueJobRowViewModel(job));
            AppendLog($"#{job.JobIndex} {Path.GetFileName(job.OutputPath)} — {job.Status}");
        }

        SelectedJob = Jobs.FirstOrDefault();

        JobCount = Jobs.Count;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsRunning = true;
        IsPaused = false;
        BusyChanged?.Invoke(this, EventArgs.Empty);
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();

        _elapsed.Restart();
        _elapsedTimer.Start();
        RefreshElapsed();
        AppendLog($"Bắt đầu hàng đợi — {Math.Clamp(maxParallel, 1, 3)} video cùng lúc.");

        try
        {
            await _queue.RunAsync(Math.Clamp(maxParallel, 1, 3), _cts.Token).ConfigureAwait(true);
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
            throw;
        }
        finally
        {
            _elapsed.Stop();
            _elapsedTimer.Stop();
            RefreshElapsed();
            IsRunning = false;
            IsPaused = false;
            BusyChanged?.Invoke(this, EventArgs.Empty);
            PauseCommand.NotifyCanExecuteChanged();
            ResumeCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        _queue?.Pause();
        IsPaused = true;
        Status = "Tạm dừng (job đang chạy vẫn tiếp tục đến hết).";
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
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
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
                var row = Jobs.FirstOrDefault(j => j.JobId == job.Id);
                row?.Apply(job);
            }
        });
    }

    private void OnJobStatusChanged(object? sender, RenderJobItem job)
    {
        RunOnUi(() =>
        {
            var row = Jobs.FirstOrDefault(j => j.JobId == job.Id);
            if (row is null)
            {
                row = new QueueJobRowViewModel(job);
                Jobs.Add(row);
            }
            else
            {
                row.Apply(job);
            }

            AppendLog($"#{job.JobIndex} {job.Status}: {Path.GetFileName(job.OutputPath)}");
            if (!string.IsNullOrWhiteSpace(job.ErrorMessage))
            {
                AppendLog($"  {job.ErrorMessage}");
                SelectedJob = row;
            }

            CopySelectedDetailsCommand.NotifyCanExecuteChanged();
        });
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
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
