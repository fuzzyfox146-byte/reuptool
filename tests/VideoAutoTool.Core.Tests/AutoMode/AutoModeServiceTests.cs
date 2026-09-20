using VideoAutoTool.Core.AutoMode;

namespace VideoAutoTool.Core.Tests.AutoMode;

public sealed class AutoModeServiceTests : IDisposable
{
    private readonly string _testDir;

    public AutoModeServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"vat-automode-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void Start_EnablesWatching()
    {
        var log = new TestLog();
        using var service = new AutoModeService(_testDir, log);

        service.Start();

        Assert.True(service.IsRunning);
    }

    [Fact]
    public void Stop_DisablesWatching()
    {
        var log = new TestLog();
        using var service = new AutoModeService(_testDir, log);

        service.Start();
        service.Stop();

        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task VideoReady_RaisedWhenVideoAndSrtStable()
    {
        var log = new TestLog();
        using var service = new AutoModeService(_testDir, log);

        var tcs = new TaskCompletionSource<VideoReadyEventArgs>();
        service.VideoReady += (s, e) => tcs.TrySetResult(e);

        service.Start();

        // Create video file
        var videoPath = Path.Combine(_testDir, "test.mp4");
        await File.WriteAllTextAsync(videoPath, "fake video content");

        // Create SRT file
        var srtPath = Path.ChangeExtension(videoPath, ".srt");
        await File.WriteAllTextAsync(srtPath, "1\n00:00:00,000 --> 00:00:01,000\nTest");

        // Wait for stability (10s) + some buffer
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await tcs.Task.WaitAsync(cts.Token);

        Assert.Equal(videoPath, result.VideoPath);
        Assert.Equal(srtPath, result.SrtPath);
    }

    [Fact]
    public async Task VideoReady_NotRaisedWhenOnlyVideoExists()
    {
        var log = new TestLog();
        using var service = new AutoModeService(_testDir, log);

        var raised = false;
        service.VideoReady += (s, e) => raised = true;

        service.Start();

        // Create only video, no SRT
        var videoPath = Path.Combine(_testDir, "test2.mp4");
        await File.WriteAllTextAsync(videoPath, "fake video");

        await Task.Delay(TimeSpan.FromSeconds(12));

        Assert.False(raised);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }
}
