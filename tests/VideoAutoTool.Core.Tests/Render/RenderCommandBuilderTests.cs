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

        Assert.Contains("-vn", args);
        Assert.Contains("-sn", args);
        Assert.Contains("-dn", args);
        Assert.Contains("[aout]", args);
        Assert.Contains("atrim=duration=", string.Join(" ", args));
        Assert.DoesNotContain("0:a:0", args);
        Assert.Contains("-max_muxing_queue_size", args);
        Assert.Equal("200000", args[args.IndexOf("-max_muxing_queue_size") + 1]);
        Assert.Contains("-muxing_queue_data_threshold", args);
        Assert.Contains("h264_nvenc", args);
        Assert.Contains("p1", args);
        Assert.Contains("cuda", args);
        Assert.DoesNotContain("libx264", args);
        Assert.DoesNotContain("1280x720", args);
        var firstInput = args.IndexOf("-i");
        Assert.True(firstInput > 0);
        Assert.True(args.IndexOf("-vn") < firstInput);
        Assert.NotEqual("-hwaccel", args[firstInput - 2]);
        var extraInput = args.IndexOf("-i", firstInput + 1);
        Assert.True(extraInput > 0);
        Assert.Equal("-hwaccel", args[extraInput - 2]);
        Assert.Equal("cuda", args[extraInput - 1]);
    }

    [Fact]
    public void BuildCachedArguments_KeepsCudaFramesAndDisablesBFrames()
    {
        var template = TemplateDefaults.CreateCo139();
        var driver = Path.GetFullPath(@"D:\src\a.mp4");
        var concat = Path.GetFullPath(@"D:\tmp\list.txt");
        var wave = Path.GetFullPath(@"D:\a\wave.mp4");
        var output = Path.GetFullPath(@"D:\out\a.mp4.part");
        var job = new RenderJobPlan(
            1, driver, "a", null, 8,
            [new BackgroundSegment(Path.GetFullPath(@"D:\bg\clip.mp4"), 10)],
            null, wave, "right", template.StylePresets[0], output);
        var graph = new FilterGraphPlan([concat, wave], "[vout]copy[vout]", "[vout]");
        var encode = new EncodeSettings(VideoEncoderKind.H264Nvenc, "cuda", KeepFramesOnGpu: true);

        var args = RenderCommandBuilder.BuildCachedArguments(
            template, job, graph, driver, concat, output, encode, new RenderRequest(RenderMode.Full));

        Assert.Contains("-vn", args);
        Assert.True(args.IndexOf("-vn") < args.IndexOf("-i"));
        Assert.Contains("[aout]", args);
        Assert.Contains("atrim=duration=", string.Join(" ", args));
        Assert.DoesNotContain("0:a:0", args);
        Assert.Contains("-max_muxing_queue_size", args);
        Assert.Equal("200000", args[args.IndexOf("-max_muxing_queue_size") + 1]);
        Assert.Contains("-muxing_queue_data_threshold", args);
        Assert.Contains("-hwaccel_output_format", args);
        Assert.Contains("-bf", args);
        Assert.Equal("0", args[args.IndexOf("-bf") + 1]);
    }

    [Fact]
    public void BuildArguments_DoesNotRewriteYuv420pWhenDurationIsFour()
    {
        var template = TemplateDefaults.CreateCo139();
        var driver = Path.GetFullPath(@"D:\src\a.mp4");
        var output = Path.GetFullPath(@"D:\out\a.mp4.part");
        var job = new RenderJobPlan(
            1, driver, "a", null, 4,
            [new BackgroundSegment(Path.GetFullPath(@"D:\bg\clip.mp4"), 10)],
            null, null, "right", template.StylePresets[0], output);
        var graph = new FilterGraphPlan([], "format=yuv420p[vout]", "[vout]");
        var encode = new EncodeSettings(VideoEncoderKind.LibX264, null);

        var args = RenderCommandBuilder.BuildArguments(
            template, job, graph, driver, output, encode, new RenderRequest(RenderMode.Clip, 3));

        var filter = args[args.IndexOf("-filter_complex") + 1];
        Assert.Contains("yuv420p", filter);
        Assert.DoesNotContain("yuv320p", filter);
        Assert.Contains("atrim=duration=3", filter);
    }
}
