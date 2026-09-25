using VideoAutoTool.Core.Render;

namespace VideoAutoTool.Core.Tests.Render;

public class PartFileTests
{
    [Fact]
    public void TryDelete_RemovesExistingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vat-part-{Guid.NewGuid():N}.part");
        File.WriteAllText(path, "x");

        PartFile.TryDelete(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void TryDelete_MissingFileDoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vat-part-{Guid.NewGuid():N}.part");
        PartFile.TryDelete(path);
    }

    [Fact]
    public async Task TryDelete_WaitsUntilTheHandleIsReleased()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vat-part-{Guid.NewGuid():N}.part");
        File.WriteAllText(path, "x");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var delete = Task.Run(() => PartFile.TryDelete(path));
        await Task.Delay(250);
        await stream.DisposeAsync();
        var finished = await Task.WhenAny(delete, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(delete, finished);
        Assert.False(File.Exists(path));
    }
}
