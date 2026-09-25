using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Render;

public class RenderStagingTests
{
    [Fact]
    public void ResolvePartPath_SameVolumeStaysBesideFinal()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var final = Path.Combine(local, "VideoAutoTool-Test", $"out-{Guid.NewGuid():N}.mp4");
        var part = RenderStaging.ResolvePartPath(final, estimatedBytes: 10_000_000);
        Assert.Equal(final + ".part", part);
    }

    [Fact]
    public void EstimateJobBytes_IsPositive()
    {
        var template = TemplateDefaults.CreateCo139();
        Assert.True(RenderStaging.EstimateJobBytes(template, 60) > 0);
    }

    [Fact]
    public void Promote_MovesLocalPartToFinal()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vat-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var final = Path.Combine(dir, "out.mp4");
        var part = final + ".part";
        File.WriteAllText(part, "ok");

        RenderStaging.Promote(part, final);

        Assert.True(File.Exists(final));
        Assert.False(File.Exists(part));
        Assert.Equal("ok", File.ReadAllText(final));
        Directory.Delete(dir, recursive: true);
    }
}
