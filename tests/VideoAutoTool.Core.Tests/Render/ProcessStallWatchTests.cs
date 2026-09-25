using VideoAutoTool.Core.Ffmpeg;

namespace VideoAutoTool.Core.Tests.Render;

public class ProcessStallWatchTests
{
    [Fact]
    public void IsStalled_WhenSizeUnchangedFor15Seconds()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(ProcessStallWatch.IsStalled(100, start, 200, start.AddSeconds(20)));
        Assert.False(ProcessStallWatch.IsStalled(100, start, 100, start.AddSeconds(14)));
        Assert.True(ProcessStallWatch.IsStalled(100, start, 100, start.AddSeconds(15)));
    }

    [Fact]
    public void TryReadLength_MissingPathIsZero()
    {
        Assert.Equal(0, ProcessStallWatch.TryReadLength(null));
        Assert.Equal(0, ProcessStallWatch.TryReadLength(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.part")));
    }
}
