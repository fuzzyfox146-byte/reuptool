using VideoAutoTool.Core.Planning;

namespace VideoAutoTool.Core.Queue;

/// <summary>
/// Represents a single job in the render queue.
/// </summary>
public sealed class RenderJobItem
{
    /// <summary>Unique identifier for this job.</summary>
    public required string Id { get; init; }
    
    /// <summary>Index of the job in the original plan (for display).</summary>
    public required int JobIndex { get; init; }
    
    /// <summary>Path to the driver video file.</summary>
    public required string DriverPath { get; init; }
    
    /// <summary>Output file path.</summary>
    public required string OutputPath { get; init; }
    
    /// <summary>Duration of the video in seconds.</summary>
    public required double DurationSeconds { get; init; }
    
    /// <summary>Current status of the job.</summary>
    public required JobStatus Status { get; set; }
    
    /// <summary>Progress from 0.0 to 1.0.</summary>
    public double Progress { get; set; }
    
    /// <summary>When the job started rendering (UTC).</summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>When the job finished (UTC).</summary>
    public DateTime? FinishedAt { get; set; }
    
    /// <summary>Last few lines of log output (for error display).</summary>
    public string? LogTail { get; set; }
    
    /// <summary>Error message if job failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a RenderJobItem from a RenderJobPlan.
    /// </summary>
    public static RenderJobItem FromPlan(RenderJobPlan plan)
    {
        return new RenderJobItem
        {
            Id = Guid.NewGuid().ToString("N"),
            JobIndex = plan.Index,
            DriverPath = plan.DriverPath,
            OutputPath = plan.OutputPath,
            DurationSeconds = plan.DurationSeconds,
            Status = JobStatus.Pending,
            Progress = 0.0
        };
    }
}
