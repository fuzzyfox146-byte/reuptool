using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoTool.Core.Queue;
using VideoAutoTool.Core.Render;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class QueueJobRowViewModel : ObservableObject
{
    public QueueJobRowViewModel(RenderJobItem job)
    {
        JobId = job.Id;
        JobIndex = job.JobIndex;
        DriverPath = job.DriverPath;
        Format = "MP4";
        Preset = "VAT";
        Apply(job);
    }

    public string JobId { get; }

    public int JobIndex { get; }

    public string DriverPath { get; }

    public string Format { get; }

    public string Preset { get; }

    [ObservableProperty]
    private string _sourceLabel = "—";

    [ObservableProperty]
    private string _outputFile = "";

    [ObservableProperty]
    private string _outputShort = "";

    [ObservableProperty]
    private string _statusShort = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _errorFull = "";

    [ObservableProperty]
    private string _logTail = "";

    [ObservableProperty]
    private string _detailBody = "";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private double _progress;

    public void Apply(RenderJobItem job)
    {
        SourceLabel = string.IsNullOrEmpty(job.IntakeId)
            ? "—"
            : string.IsNullOrEmpty(job.IntakeName) ? job.IntakeId : $"{job.IntakeId} {job.IntakeName}";
        OutputFile = job.OutputPath;
        OutputShort = ShortOutputName(job.OutputPath);
        Progress = job.Progress;
        ErrorFull = job.ErrorMessage ?? "";
        LogTail = string.IsNullOrWhiteSpace(job.LogTail) ? "" : job.LogTail;
        HasError = job.Status == JobStatus.Failed && !string.IsNullOrWhiteSpace(ErrorFull);
        StatusShort = job.Status switch
        {
            JobStatus.Pending => "Chờ",
            JobStatus.Paused => "Tạm dừng",
            JobStatus.Running => $"Đang encode {job.Progress:P0}",
            JobStatus.Done => DoneLabel(job),
            JobStatus.Failed => FailedLabel(job),
            JobStatus.Cancelled => "Đã hủy",
            JobStatus.Skipped => "Bỏ qua",
            _ => job.Status.ToString()
        };
        StatusText = StatusShort;
        DetailBody = BuildDetail(job);
    }

    internal static string ShortOutputName(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name) || name.Length <= 36)
        {
            return name;
        }

        var ext = Path.GetExtension(name);
        if (string.IsNullOrEmpty(ext))
        {
            ext = ".mp4";
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        var prefixLen = Math.Min(18, stem.Length);
        return stem[..prefixLen] + "..." + ext;
    }

    private string BuildDetail(RenderJobItem job)
    {
        var lines = new List<string>
        {
            string.IsNullOrEmpty(job.IntakeId) ? $"Job #{job.JobIndex}" : $"Nguồn {job.IntakeId} {job.IntakeName} · job #{job.JobIndex}",
            $"Trạng thái: {StatusShort}",
            $"File xuất: {job.OutputPath}",
            $"Video nguồn: {job.DriverPath}",
            $"Thời lượng: {job.DurationSeconds:0.##}s"
        };

        var elapsed = RenderTiming.Elapsed(job.StartedAt, job.FinishedAt);
        if (elapsed is { } took)
        {
            lines.Add($"Render: {RenderTiming.Describe(took, job.DurationSeconds)}");
        }

        if (!string.IsNullOrWhiteSpace(ErrorFull))
        {
            lines.Add("");
            lines.Add("--- Lỗi ---");
            lines.Add(ErrorFull);
        }

        if (!string.IsNullOrWhiteSpace(LogTail))
        {
            lines.Add("");
            lines.Add("--- Chi tiết kỹ thuật ---");
            lines.Add(LogTail);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string DoneLabel(RenderJobItem job)
    {
        var elapsed = RenderTiming.Elapsed(job.StartedAt, job.FinishedAt);
        return elapsed is { } took ? $"Xong · {RenderTiming.FormatElapsed(took)}" : "Xong";
    }

    private static string FailedLabel(RenderJobItem job)
    {
        var elapsed = RenderTiming.Elapsed(job.StartedAt, job.FinishedAt);
        return elapsed is { } took ? $"Lỗi · {RenderTiming.FormatElapsed(took)}" : "Lỗi";
    }
}
