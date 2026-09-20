using System.Text.Json;

namespace VideoAutoTool.Core.Queue;

/// <summary>
/// Stores and loads the render queue from a JSON file with atomic writes.
/// </summary>
public sealed class JobStore
{
    private readonly string _queueFilePath;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
    public List<RenderJobItem> Load()
    {
        if (!File.Exists(_queueFilePath))
        {
            return new List<RenderJobItem>();
        }

        try
        {
            var json = File.ReadAllText(_queueFilePath);
            var jobs = JsonSerializer.Deserialize<List<RenderJobItem>>(json, JsonOptions)
                ?? new List<RenderJobItem>();

            // Reset Running jobs to Pending on load (crashed/interrupted)
            foreach (var job in jobs.Where(j => j.Status == JobStatus.Running))
            {
                job.Status = JobStatus.Pending;
                job.StartedAt = null;
                job.Progress = 0.0;
            }

            return jobs;
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
                var json = JsonSerializer.Serialize(jobs, JsonOptions);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _queueFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                // Clean up temp file if it exists
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* best effort */ }
                }
                throw new InvalidOperationException($"Failed to save queue to {_queueFilePath}: {ex.Message}", ex);
            }
        }
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
}
