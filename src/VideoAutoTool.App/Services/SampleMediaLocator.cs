namespace VideoAutoTool.App.Services;

/// <summary>
/// Resolves the sample media in <c>mẫu</c> (or copied <c>assets/samples</c>) for the design canvas.
/// </summary>
public static class SampleMediaLocator
{
    public static string? FindAvatarImage() =>
        FindByName("avatar.png") ?? FindFirst("*.png");

    public static string? FindSoundwaveVideo() =>
        FindByName("soundwave.mov")
        ?? FindByName("Sound wave.mov")
        ?? FindFirst("*.mov");

    public static string? FindSampleSrt() => FindFirst("*.srt");

    private static string? FindByName(string fileName)
    {
        foreach (var dir in CandidateDirectories())
        {
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path)) return path;
        }

        return null;
    }

    private static string? FindFirst(string searchPattern)
    {
        foreach (var dir in CandidateDirectories())
        {
            if (!Directory.Exists(dir)) continue;
            var match = Directory.EnumerateFiles(dir, searchPattern).FirstOrDefault();
            if (match != null) return match;
        }

        return null;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        var samples = Path.Combine(AppContext.BaseDirectory, "assets", "samples");
        yield return samples;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir != null; i++)
        {
            yield return Path.Combine(dir.FullName, "mẫu");
            yield return Path.Combine(dir.FullName, "mau");
            dir = dir.Parent;
        }
    }
}
