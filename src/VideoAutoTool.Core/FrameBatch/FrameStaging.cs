using VideoAutoTool.Core.Render;

namespace VideoAutoTool.Core.FrameBatch;

/// <summary>
/// Writes the frame-render .part.mp4 on the local SSD when the export folder is on another disk
/// and the SSD still has room. FFmpeg needs a real .mp4 extension, so this does not reuse the
/// Opt ".part" suffix.
/// </summary>
public static class FrameStaging
{
    public const long ReserveBytes = 5L * 1024 * 1024 * 1024;

    public static string AppFolder { get; set; } = "FrameRender";

    public static string StagingDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolder,
            "out-tmp");

    public static long EstimateBytes(double durationSeconds, int crf)
    {
        var videoKbps = Math.Max(800, 9000 - Math.Clamp(crf, 0, 51) * 250);
        const int audioKbps = 192;
        return (long)(Math.Max(0, durationSeconds) * (videoKbps + audioKbps) * 1000 / 8 * 1.3);
    }

    public static string ResolvePartPath(string finalOutputPath, long estimatedBytes)
    {
        var destPart = SiblingPart(finalOutputPath);
        try
        {
            var destRoot = Path.GetPathRoot(Path.GetFullPath(finalOutputPath));
            var localRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            if (string.IsNullOrWhiteSpace(destRoot) ||
                string.IsNullOrWhiteSpace(localRoot) ||
                destRoot.Equals(localRoot, StringComparison.OrdinalIgnoreCase))
            {
                return destPart;
            }

            var drive = new DriveInfo(localRoot);
            var need = checked(estimatedBytes * 2 + ReserveBytes);
            if (drive.AvailableFreeSpace < need)
            {
                return destPart;
            }

            Directory.CreateDirectory(StagingDirectory);
            return Path.Combine(StagingDirectory, Guid.NewGuid().ToString("N") + ".part.mp4");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException)
        {
            return destPart;
        }
    }

    public static void Promote(string partPath, string finalOutputPath)
    {
        var destPart = SiblingPart(finalOutputPath);
        var staged = !string.Equals(Path.GetFullPath(partPath), Path.GetFullPath(destPart), StringComparison.OrdinalIgnoreCase);
        var directory = Path.GetDirectoryName(finalOutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(finalOutputPath))
        {
            File.Delete(finalOutputPath);
        }

        if (!staged)
        {
            File.Move(partPath, finalOutputPath);
            return;
        }

        PartFile.TryDelete(destPart);
        File.Copy(partPath, destPart, overwrite: true);
        File.Move(destPart, finalOutputPath);
        PartFile.TryDelete(partPath);
    }

    public static string SiblingPart(string finalOutputPath)
    {
        var directory = Path.GetDirectoryName(finalOutputPath) ?? "";
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(finalOutputPath) + ".part.mp4");
    }
}
