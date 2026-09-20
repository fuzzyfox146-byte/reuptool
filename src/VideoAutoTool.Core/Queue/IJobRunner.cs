using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Queue;

/// <summary>
/// Interface for job execution (allows mocking in tests).
/// </summary>
public interface IJobRunner
{
    /// <summary>
    /// Renders a single job.
    /// </summary>
    Task RenderAsync(
        RenderJobItem job,
        Template template,
        RenderJobPlan plan,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null);
}
