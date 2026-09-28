using VideoAutoTool.Core.Queue;

namespace VideoAutoTool.Core.Tests.Queue;

public class RenderedSourceMoverTests
{
    [Fact]
    public void FolderFor_UsesParentOfFirstVideo()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vat-src-" + Guid.NewGuid().ToString("N"));
        var video = Path.Combine(folder, "001 title.mp4");
        Assert.Equal(Path.Combine(folder, "rendered"), RenderedSourceMover.FolderFor(video));
    }

    [Fact]
    public void Move_SendsFinishedVideosToRenderedAndLeavesMissing()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vat-src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var first = Path.Combine(folder, "001 a.mp4");
        var second = Path.Combine(folder, "002 b.mp4");
        File.WriteAllText(first, "1");
        File.WriteAllText(second, "2");
        var rendered = RenderedSourceMover.FolderFor(first);

        try
        {
            var moved = RenderedSourceMover.Move([first, second, Path.Combine(folder, "missing.mp4")], rendered);

            Assert.Equal(2, moved.Count);
            Assert.False(File.Exists(first));
            Assert.False(File.Exists(second));
            Assert.True(File.Exists(Path.Combine(rendered, "001 a.mp4")));
            Assert.True(File.Exists(Path.Combine(rendered, "002 b.mp4")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Move_RenamesWhenRenderedNameAlreadyExists()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vat-src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var video = Path.Combine(folder, "001 a.mp4");
        File.WriteAllText(video, "new");
        var rendered = RenderedSourceMover.FolderFor(video);
        Directory.CreateDirectory(rendered);
        File.WriteAllText(Path.Combine(rendered, "001 a.mp4"), "old");

        try
        {
            var dest = Assert.Single(RenderedSourceMover.Move([video], rendered));
            Assert.Equal(Path.Combine(rendered, "001 a (2).mp4"), dest);
            Assert.Equal("old", File.ReadAllText(Path.Combine(rendered, "001 a.mp4")));
            Assert.Equal("new", File.ReadAllText(dest));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
