using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Queue;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Queue;

/// <summary>
/// Adapter that wraps JobRenderer to implement IJobRunner interface.
/// </summary>
public sealed class JobRendererAdapter : IJobRunner
{
    private readonly JobRenderer _renderer;

    public JobRendererAdapter(JobRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public async Task RenderAsync(
        RenderJobItem job,
        Template template,
        RenderJobPlan plan,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        await _renderer.RenderAsync(
            template,
            plan,
            new RenderRequest(RenderMode.Full),
            job.OutputPath,
            useCache: true,  // Use cache by default for queue rendering
            progress,
            cancellationToken).ConfigureAwait(false);
    }
}
