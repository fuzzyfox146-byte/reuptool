using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Render;

namespace VideoAutoTool.Core.Tests.Render;

public class BackgroundCacheRulesTests
{
    [Fact]
    public void EncodeDuration_UsesFullClipNotJobRemainder()
    {
        var segment = new BackgroundSegment(@"D:\bg\long.mp4", 4800);
        Assert.Equal(4800, BackgroundCacheRules.EncodeDurationSeconds(segment));
        Assert.NotEqual(4530, BackgroundCacheRules.EncodeDurationSeconds(segment));
    }
}
