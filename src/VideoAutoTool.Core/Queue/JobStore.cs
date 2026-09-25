using System.Text.Json;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Queue;

/// <summary>
/// Stores and loads the render queue from a JSON file with atomic writes.
/// </summary>
public sealed class JobStore
{
    private readonly string _queueFilePath;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a JobStore with the specified queue file path.
    /// </summary>
    /// <param name="queueFilePath">Path to queue.json file. If null, uses %TEMP%\vat-queue.json</param>
    public JobStore(string? queueFilePath = null)
    {
        _queueFilePath = queueFilePath ?? Path.Combine(Path.GetTempPath(), "vat-queue.json");
    }

    /// <summary>
    /// Loads jobs from the queue file. Running jobs are reset to Pending.
    /// Returns empty list if file doesn't exist.
    /// </summary>
    public List<RenderJobItem> Load() => LoadSnapshot().Jobs;

    /// <summary>
    /// Loads the full snapshot (jobs + plans + intake templates). Missing file is empty.
    /// A legacy file that is only a job array still loads the rows.
    /// </summary>
    public QueueSnapshot LoadSnapshot()
    {
        if (!File.Exists(_queueFilePath))
        {
            return new QueueSnapshot();
        }

        try
        {
            var json = File.ReadAllText(_queueFilePath);
            using var doc = JsonDocument.Parse(json);
            QueueSnapshot snapshot;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                snapshot = new QueueSnapshot
                {
                    Jobs = JsonSerializer.Deserialize<List<RenderJobItem>>(json, TemplateJsonContext.Options)
                        ?? []
                };
            }
            else
            {
                snapshot = JsonSerializer.Deserialize<QueueSnapshot>(json, TemplateJsonContext.Options)
                    ?? new QueueSnapshot();
                snapshot.Jobs ??= [];
                snapshot.PlansByJobId ??= new Dictionary<string, RenderJobPlan>(StringComparer.Ordinal);
                snapshot.TemplateByIntake ??= new Dictionary<string, Template>(StringComparer.Ordinal);
                snapshot.IntakeOrder ??= [];
            }

            ResetInterrupted(snapshot.Jobs);
            return snapshot;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load queue from {_queueFilePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves jobs to the queue file atomically (via temp file + move).
    /// Thread-safe: only one save operation can run at a time.
    /// </summary>
    public void Save(IEnumerable<RenderJobItem> jobs)
    {
        WriteJson(JsonSerializer.Serialize(jobs, TemplateJsonContext.Options));
    }

    public void SaveSnapshot(QueueSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Jobs ??= [];
        snapshot.PlansByJobId ??= new Dictionary<string, RenderJobPlan>(StringComparer.Ordinal);
        snapshot.TemplateByIntake ??= new Dictionary<string, Template>(StringComparer.Ordinal);
        snapshot.IntakeOrder ??= [];
        snapshot.Version = snapshot.Version < 1 ? 1 : snapshot.Version;
        WriteJson(JsonSerializer.Serialize(snapshot, TemplateJsonContext.Options));
    }

    /// <summary>
    /// Deletes the queue file.
    /// </summary>
    public void Clear()
    {
        if (File.Exists(_queueFilePath))
        {
            File.Delete(_queueFilePath);
        }
    }

    /// <summary>
    /// Gets the path to the queue file.
    /// </summary>
    public string QueueFilePath => _queueFilePath;

    private void WriteJson(string json)
    {
        lock (_lock)
        {
            var directory = Path.GetDirectoryName(_queueFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _queueFilePath + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _queueFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* best effort */ }
                }

                throw new InvalidOperationException($"Failed to save queue to {_queueFilePath}: {ex.Message}", ex);
            }
        }
    }

    private static void ResetInterrupted(List<RenderJobItem> jobs)
    {
        foreach (var job in jobs.Where(j => j.Status is JobStatus.Running or JobStatus.Paused))
        {
            job.Status = JobStatus.Pending;
            job.StartedAt = null;
            job.Progress = 0.0;
        }
    }
}
