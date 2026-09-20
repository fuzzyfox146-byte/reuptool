using VideoAutoTool.Core.Ffmpeg;

namespace VideoAutoTool.Core.Tests;

public class FfmpegLocatorTests
{
    [Fact]
    public void Locate_WithFakeDirectory_ReturnsPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vat-ffmpeg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ffmpeg.exe"), string.Empty);
        File.WriteAllText(Path.Combine(dir, "ffprobe.exe"), string.Empty);

        var paths = FfmpegLocator.Locate(dir);
        Assert.Equal(Path.Combine(dir, "ffmpeg.exe"), paths.FfmpegPath);
        Assert.Equal(Path.Combine(dir, "ffprobe.exe"), paths.FfprobePath);
    }

    [Fact]
    public void Locate_FindsToolsFfmpegLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), $"vat-ffmpeg-layout-{Guid.NewGuid():N}");
        var dir = Path.Combine(root, "tools", "ffmpeg");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "ffmpeg.exe"), string.Empty);
        File.WriteAllText(Path.Combine(dir, "ffprobe.exe"), string.Empty);

        var paths = FfmpegLocator.Locate(dir);
        Assert.Equal(Path.Combine(dir, "ffmpeg.exe"), paths.FfmpegPath);
        Directory.Delete(root, recursive: true);
    }
}
