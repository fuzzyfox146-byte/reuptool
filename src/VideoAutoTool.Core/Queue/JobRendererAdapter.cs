using VideoAutoTool.Core.Planning;
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

    public RenderRequest ActiveRequest { get; set; } = new(RenderMode.Full);

    public async Task RenderAsync(
        RenderJobItem job,
        Template template,
        RenderJobPlan plan,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        var request = job.ClipSeconds is > 0
            ? new RenderRequest(RenderMode.Clip, job.ClipSeconds)
            : new RenderRequest(RenderMode.Full);
        await _renderer.RenderAsync(
            template,
            plan,
            request,
            job.OutputPath,
            useCache: true,  // Use cache by default for queue rendering
            progress,
            cancellationToken).ConfigureAwait(false);
    }
}
