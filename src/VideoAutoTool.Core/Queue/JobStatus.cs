namespace VideoAutoTool.Core.Queue;

/// <summary>
/// Status of a render job in the queue.
/// </summary>
public enum JobStatus
{
    /// <summary>Job is waiting to be processed.</summary>
    Pending,
    
    /// <summary>Job is currently being rendered.</summary>
    Running,
    
    /// <summary>Job was paused by user.</summary>
    Paused,
    
    /// <summary>Job completed successfully.</summary>
    Done,
    
    /// <summary>Job failed with an error.</summary>
    Failed,
    
    /// <summary>Job was cancelled by user.</summary>
    Cancelled,
    
    /// <summary>Job was skipped (e.g., output file already exists).</summary>
    Skipped
}
