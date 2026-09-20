using VideoAutoTool.Core.Cache;
using Xunit;

namespace VideoAutoTool.Core.Tests.Cache;

public class CacheKeyTests
{
    [Fact]
    public void ComputeHash_SameInputs_SameHash()
    {
        var key1 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");
        var key2 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");

        Assert.Equal(key1.ComputeHash(), key2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_DifferentPath_DifferentHash()
    {
        var key1 = new CacheKey("test1.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");
        var key2 = new CacheKey("test2.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");

        Assert.NotEqual(key1.ComputeHash(), key2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_DifferentSize_DifferentHash()
    {
        var key1 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");
        var key2 = new CacheKey("test.mp4", 2000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");

        Assert.NotEqual(key1.ComputeHash(), key2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_DifferentMtime_DifferentHash()
    {
        var key1 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");
        var key2 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 2), 1280, 720, 25, 1.0, 1.0, "cover");

        Assert.NotEqual(key1.ComputeHash(), key2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_DifferentCanvas_DifferentHash()
    {
        var key1 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");
        var key2 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1920, 1080, 25, 1.0, 1.0, "cover");

        Assert.NotEqual(key1.ComputeHash(), key2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_DifferentScale_DifferentHash()
    {
        var key1 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");
        var key2 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.5, 1.0, "cover");

        Assert.NotEqual(key1.ComputeHash(), key2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_DifferentOpacity_DifferentHash()
    {
        var key1 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");
        var key2 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 0.5, "cover");

        Assert.NotEqual(key1.ComputeHash(), key2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_DifferentScaleMode_DifferentHash()
    {
        var key1 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover");
        var key2 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "fit");

        Assert.NotEqual(key1.ComputeHash(), key2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_WithExtraParams_IncludedInHash()
    {
        var key1 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover",
            new Dictionary<string, string> { ["type"] = "background" });
        var key2 = new CacheKey("test.mp4", 1000, new DateTime(2024, 1, 1), 1280, 720, 25, 1.0, 1.0, "cover",
            new Dictionary<string, string> { ["type"] = "avatar" });

        Assert.NotEqual(key1.ComputeHash(), key2.ComputeHash());
    }
}
