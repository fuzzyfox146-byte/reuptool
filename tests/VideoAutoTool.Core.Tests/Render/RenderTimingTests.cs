using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Render;

public class RenderTimingTests
{
    [Fact]
    public void FormatElapsed_UsesMinutesAndSeconds()
    {
        Assert.Equal("8 phút 12 giây", RenderTiming.FormatElapsed(TimeSpan.FromSeconds(492)));
        Assert.Equal("5 giây", RenderTiming.FormatElapsed(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Describe_IncludesRealtimeFactor()
    {
        var text = RenderTiming.Describe(TimeSpan.FromSeconds(100), 320);
        Assert.Contains("1 phút 40 giây", text);
        Assert.Contains("3.2×", text);
    }
}

public class TemplateRenderOptionsParallelLockTests
{
    [Fact]
    public async Task AssetPrepareGate_AllowsDifferentKeysTogether()
    {
        var aStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var a = AssetPrepareGate.RunAsync(
            [Path.Combine(Path.GetTempPath(), "vat-a.mp4")],
            async () =>
            {
                aStarted.SetResult();
                await release.Task;
            },
            CancellationToken.None);

        var b = AssetPrepareGate.RunAsync(
            [Path.Combine(Path.GetTempPath(), "vat-b.mp4")],
            async () =>
            {
                bStarted.SetResult();
                await release.Task;
            },
            CancellationToken.None);

        var both = Task.WhenAll(aStarted.Task, bStarted.Task);
        var finished = await Task.WhenAny(both, Task.Delay(2000));
        Assert.Same(both, finished);

        release.SetResult();
        await Task.WhenAll(a, b);
    }
}
