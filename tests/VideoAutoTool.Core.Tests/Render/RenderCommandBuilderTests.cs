using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Render;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests.Render;

public class RenderCommandBuilderTests
{
    [Fact]
    public void BuildArguments_UsesNvencAndHwAccelOnVideoInputs()
    {
        var template = TemplateDefaults.CreateCo139();
        var bg = Path.GetFullPath(@"D:\bg\clip.mp4");
        var driver = Path.GetFullPath(@"D:\src\a.mp4");
        var output = Path.GetFullPath(@"D:\out\a.mp4.part");
        var job = new RenderJobPlan(
            1,
            driver,
            "a",
            null,
            8,
            [new BackgroundSegment(bg, 10)],
            null,
            null,
            "right",
            template.StylePresets[0],
            output);
        var graph = new FilterGraphPlan([bg], "[vout]copy[vout]", "[vout]");
        var encode = new EncodeSettings(VideoEncoderKind.H264Nvenc, "cuda");

        var args = RenderCommandBuilder.BuildArguments(
            template, job, graph, driver, output, encode, new RenderRequest(RenderMode.Clip, 8));

        Assert.Contains("h264_nvenc", args);
        Assert.Contains("p1", args);
        Assert.Contains("cuda", args);
        Assert.DoesNotContain("libx264", args);
        Assert.DoesNotContain("1280x720", args);
        var firstInput = args.IndexOf("-i");
        Assert.True(firstInput > 0);
        Assert.NotEqual("-hwaccel", args[firstInput - 2]);
        var extraInput = args.IndexOf("-i", firstInput + 1);
        Assert.True(extraInput > 0);
        Assert.Equal("-hwaccel", args[extraInput - 2]);
        Assert.Equal("cuda", args[extraInput - 1]);
    }
}
