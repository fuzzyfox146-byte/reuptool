using VideoAutoTool.Core.Cache;
using Xunit;

namespace VideoAutoTool.Core.Tests.Cache;

public class ConcatListBuilderTests
{
    [Fact]
    public void Create_SimplesPaths_GeneratesCorrectFormat()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var paths = new List<string> { "D:\\video1.mp4", "D:\\video2.mp4" };

        var listPath = ConcatListBuilder.Create(paths, tempDir);

        Assert.True(File.Exists(listPath));
        var content = File.ReadAllText(listPath);
        Assert.Contains("file 'D:\\video1.mp4'", content);
        Assert.Contains("file 'D:\\video2.mp4'", content);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void Create_PathsWithSpaces_EscapesCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var paths = new List<string> { "D:\\my video.mp4" };

        var listPath = ConcatListBuilder.Create(paths, tempDir);

        var content = File.ReadAllText(listPath);
        Assert.Contains("file 'D:\\my video.mp4'", content);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void Create_PathsWithSingleQuote_EscapesCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var paths = new List<string> { "D:\\video's.mp4" };

        var listPath = ConcatListBuilder.Create(paths, tempDir);

        var content = File.ReadAllText(listPath);
        Assert.Contains("file 'D:\\video'\\''s.mp4'", content);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void Create_RelativePaths_ConvertsToAbsolute()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var paths = new List<string> { "video.mp4" };

        var listPath = ConcatListBuilder.Create(paths, tempDir);

        var content = File.ReadAllText(listPath);
        var fullPath = Path.GetFullPath("video.mp4");
        Assert.Contains($"file '{fullPath}'", content);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void Create_DoesNotWriteUtf8Bom()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var listPath = ConcatListBuilder.Create(new List<string> { @"D:\video1.mp4" }, tempDir);
        var bytes = File.ReadAllBytes(listPath);
        Assert.True(bytes.Length >= 4);
        Assert.Equal((byte)'f', bytes[0]);
        Assert.False(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Directory.Delete(tempDir, true);
    }
}