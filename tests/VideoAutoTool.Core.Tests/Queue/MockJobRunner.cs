using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Queue;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Queue;

/// <summary>
/// Mock IJobRunner for testing queue logic without actual rendering.
/// </summary>
internal sealed class MockJobRunner : IJobRunner
{
    public List<string> RenderedJobs { get; } = new();
    public TimeSpan RenderDelay { get; set; } = TimeSpan.FromMilliseconds(50);
    public Func<RenderJobItem, Exception?>? FailCondition { get; set; }

    public async Task RenderAsync(
        RenderJobItem job,
        Template template,
        RenderJobPlan plan,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        RenderedJobs.Add(job.Id);

        // Check if we should fail this job
        var exception = FailCondition?.Invoke(job);
        if (exception != null)
        {
            throw exception;
        }

        // Simulate rendering with progress updates
        for (int i = 0; i <= 10; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(i / 10.0);
            await Task.Delay(RenderDelay / 10, cancellationToken).ConfigureAwait(false);
        }

        // Create output file if it doesn't exist
        if (!File.Exists(job.OutputPath))
        {
            await File.WriteAllTextAsync(job.OutputPath, "mock output", cancellationToken).ConfigureAwait(false);
        }
    }
}
