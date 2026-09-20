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
        Assert.EndsWith("ffmpeg.exe", paths.FfmpegPath);
        Assert.EndsWith("ffprobe.exe", paths.FfprobePath);
    }
}
