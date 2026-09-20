using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests;

public class TemplateStoreTests
{
    [Fact]
    public void RoundTrip_PreservesPresetBox()
    {
        var template = TemplateDefaults.CreateCo139();
        var path = Path.Combine(Path.GetTempPath(), $"vat-template-{Guid.NewGuid():N}.json");
        TemplateStore.Save(path, template);
        var loaded = TemplateStore.Load(path);
        File.Delete(path);

        Assert.Equal(2, loaded.StylePresets.Count);
        Assert.Equal(465, loaded.StylePresets[0].Box.Y);
        Assert.Equal(330, loaded.StylePresets[1].Box.Y);
        Assert.Equal("gold-serif", loaded.Layers.First(l => l.Type == LayerType.Subtitle).StyleAssignment?.Presets?.First());
    }
}
