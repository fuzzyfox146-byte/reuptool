namespace VideoAutoTool.Core.Download;

public static class DownloadArchive
{
    public const string VideosFileName = "downloaded_videos.txt";
    public const string SubsFileName = "downloaded_subs.txt";

    public static IReadOnlyList<string> Clear(string parentFolder)
    {
        var removed = new List<string>();
        foreach (var name in new[] { VideosFileName, SubsFileName })
        {
            var path = Path.Combine(parentFolder, name);
            if (!File.Exists(path))
            {
                continue;
            }

            File.Delete(path);
            removed.Add(path);
        }

        return removed;
    }
}
