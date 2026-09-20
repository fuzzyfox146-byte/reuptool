using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.Planning;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Tests;

public class JobPlannerTests
{
    [Fact]
    public async Task Plan_OneBackgroundPerJob_RoundRobinsAssets_TrimsLongerClips()
    {
        var probe = new FakeMediaProbe(new Dictionary<string, MediaInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["d1"] = new("d1", 14, true, 14, false, null, null, null, null, false, null),
            ["d2"] = new("d2", 9, true, 9, false, null, null, null, null, false, null),
            ["d3"] = new("d3", 21, true, 21, false, null, null, null, null, false, null),
            ["bg1"] = new("bg1", 20, false, null, true, 1920, 1080, 30, "yuv420p", false, null),
            ["bg2"] = new("bg2", 10, false, null, true, 1920, 1080, 30, "yuv420p", false, null),
            ["bg3"] = new("bg3", 30, false, null, true, 1280, 720, 30, "yuv420p", false, null),
            ["a1"] = new("a1", null, false, null, true, 500, 700, null, "rgba", true, null),
            ["a2"] = new("a2", null, false, null, true, 800, 800, null, "rgba", true, null),
            ["a3"] = new("a3", null, false, null, true, 600, 900, null, "rgba", true, null),
            ["w1"] = new("w1", 2.5, false, null, true, 640, 160, 60, "argb", true, null),
            ["w2"] = new("w2", 3.0, false, null, true, 640, 160, 60, "argb", true, null)
        });

        var template = TemplateDefaults.CreateCo139();
        var root = CreateFakeRoot(template);
        var planner = new JobPlanner(probe);
        var plan1 = await planner.PlanAsync(template, root);
        var plan2 = await planner.PlanAsync(template, root);

        Assert.Equal(3, plan1.Jobs.Count);
        Assert.Equal(["bg1"], SegmentNames(plan1.Jobs[0]));
        Assert.Equal(["bg2"], SegmentNames(plan1.Jobs[1]));
        Assert.Equal(["bg3"], SegmentNames(plan1.Jobs[2]));
        Assert.Single(plan1.Jobs[0].Backgrounds);
        Assert.True(plan1.Jobs[0].Backgrounds[0].DurationFull >= plan1.Jobs[0].DurationSeconds);
        Assert.EndsWith("a1.png", plan1.Jobs[0].AvatarPath);
        Assert.EndsWith("a2.png", plan1.Jobs[1].AvatarPath);
        Assert.EndsWith("a3.png", plan1.Jobs[2].AvatarPath);
        Assert.Contains("w1", plan1.Jobs[0].WavePath);
        Assert.Contains("w2", plan1.Jobs[1].WavePath);
        Assert.Contains("w1", plan1.Jobs[2].WavePath);
        Assert.Equal(plan1.Jobs.Select(j => j.OutputPath), plan2.Jobs.Select(j => j.OutputPath));
    }

    [Fact]
    public async Task Plan_SkipsDriver_WhenNoBackgroundIsLongEnough()
    {
        var probe = new FakeMediaProbe(new Dictionary<string, MediaInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["d1"] = new("d1", 14, true, 14, false, null, null, null, null, false, null),
            ["d2"] = new("d2", 9, true, 9, false, null, null, null, null, false, null),
            ["d3"] = new("d3", 21, true, 21, false, null, null, null, null, false, null),
            ["bg1"] = new("bg1", 7, false, null, true, 1920, 1080, 30, "yuv420p", false, null),
            ["bg2"] = new("bg2", 6, false, null, true, 1920, 1080, 30, "yuv420p", false, null),
            ["bg3"] = new("bg3", 8, false, null, true, 1280, 720, 30, "yuv420p", false, null),
            ["a1"] = new("a1", null, false, null, true, 500, 700, null, "rgba", true, null),
            ["a2"] = new("a2", null, false, null, true, 800, 800, null, "rgba", true, null),
            ["a3"] = new("a3", null, false, null, true, 600, 900, null, "rgba", true, null),
            ["w1"] = new("w1", 2.5, false, null, true, 640, 160, 60, "argb", true, null),
            ["w2"] = new("w2", 3.0, false, null, true, 640, 160, 60, "argb", true, null)
        });

        var template = TemplateDefaults.CreateCo139();
        var root = CreateFakeRoot(template);
        var planner = new JobPlanner(probe);
        var plan = await planner.PlanAsync(template, root);

        Assert.Empty(plan.Jobs);
        Assert.Contains(plan.Warnings, w => w.Message.Contains("Skipped", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> SegmentNames(RenderJobPlan job) =>
        job.Backgrounds.Select(b => Path.GetFileNameWithoutExtension(b.File)).ToList();

    private static string CreateFakeRoot(Template template)
    {
        var root = Path.Combine(Path.GetTempPath(), $"vat-root-{Guid.NewGuid():N}");
        WriteFile(root, template.Driver.Folder, "001 d1.mp4");
        WriteFile(root, template.Driver.Folder, "002 d2.mp4");
        WriteFile(root, template.Driver.Folder, "003 d3.mp4");
        WriteFile(root, "background", "bg1.mp4");
        WriteFile(root, "background", "bg2.mp4");
        WriteFile(root, "background", "bg3.mp4");
        WriteFile(root, "avatar", "a1.png");
        WriteFile(root, "avatar", "a2.png");
        WriteFile(root, "avatar", "a3.png");
        WriteFile(root, "soundwave", "w1.mov");
        WriteFile(root, "soundwave", "w2.mov");
        WriteFile(root, @"text\SUB", "001 d1.en.srt");
        WriteFile(root, @"text\SUB", "002 d2.en.srt");
        WriteFile(root, @"text\SUB", "003 d3.en.srt");
        return root;
    }

    private static void WriteFile(string root, string folder, string name)
    {
        var dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), "x");
    }

    private sealed class FakeMediaProbe : IMediaProbe
    {
        private readonly Dictionary<string, MediaInfo> _map;

        public FakeMediaProbe(Dictionary<string, MediaInfo> map) => _map = map;

        public Task<MediaInfo> ProbeAsync(string path, CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(path);
            var match = _map.FirstOrDefault(kvp => fileName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));
            if (match.Value is null)
            {
                throw new KeyNotFoundException(fileName);
            }

            return Task.FromResult(match.Value with { Path = path });
        }
    }
}
