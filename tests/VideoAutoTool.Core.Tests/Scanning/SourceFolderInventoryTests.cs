using VideoAutoTool.Core.Scanning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Scanning;

public sealed class SourceFolderInventoryTests : IDisposable
{
    private readonly string _root;

    public SourceFolderInventoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"vat-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void CountMatchingFiles_OnlyTopLevel_DoesNotWalkSubfolders()
    {
        var driver = Path.Combine(_root, "source", "NGUON");
        Directory.CreateDirectory(driver);
        Directory.CreateDirectory(Path.Combine(driver, "nested"));
        File.WriteAllText(Path.Combine(driver, "001.mp4"), "x");
        File.WriteAllText(Path.Combine(driver, "nested", "hidden.mp4"), "x");
        File.WriteAllText(Path.Combine(driver, "note.txt"), "x");

        var count = FileScanner.CountMatchingFiles(driver, [".mp4"]);
        Assert.Equal(1, count);
    }

    [Fact]
    public void FromTemplate_IncludesDriverLayersAndOutput()
    {
        var template = TemplateDefaults.CreateCo139();
        var slots = SourceFolderInventory.FromTemplate(template);

        Assert.Contains(slots, s => s.Kind == SourceFolderKind.Driver && s.UsedForRender);
        Assert.Contains(slots, s => s.Kind == SourceFolderKind.Background && s.UsedForRender);
        Assert.Contains(slots, s => s.Kind == SourceFolderKind.Avatar && s.UsedForRender);
        Assert.Contains(slots, s => s.Kind == SourceFolderKind.Soundwave && s.UsedForRender);
        Assert.Contains(slots, s => s.Kind == SourceFolderKind.Subtitle && s.UsedForRender);
        Assert.Contains(slots, s => s.Kind == SourceFolderKind.Output && s.UsedForRender);
    }

    [Fact]
    public void Inspect_MarksReadyWhenFolderHasMatchingFiles()
    {
        var driver = Path.Combine(_root, "source", "NGUON");
        Directory.CreateDirectory(driver);
        File.WriteAllText(Path.Combine(driver, "001.mp4"), "x");

        var template = TemplateDefaults.CreateCo139();
        var slot = SourceFolderInventory.FromTemplate(template).First(s => s.Kind == SourceFolderKind.Driver);
        var status = SourceFolderInventory.Inspect(_root, slot);

        Assert.True(status.FolderExists);
        Assert.Equal(1, status.FileCount);
        Assert.True(status.IsReady);
    }

    [Fact]
    public void Inspect_NotReadyWhenFolderMissing()
    {
        var template = TemplateDefaults.CreateCo139();
        var slot = SourceFolderInventory.FromTemplate(template).First(s => s.Kind == SourceFolderKind.Driver);
        var status = SourceFolderInventory.Inspect(_root, slot);

        Assert.False(status.FolderExists);
        Assert.Equal(0, status.FileCount);
        Assert.False(status.IsReady);
    }
}
