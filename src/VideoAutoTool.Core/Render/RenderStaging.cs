using VideoAutoTool.Core.Templates;
using VideoAutoTool.Core.Validation;

namespace VideoAutoTool.Core.Render;

/// <summary>
/// Writes the ffmpeg .part on the local SSD when the final path is on another volume
/// and the SSD has enough free space. After encode, Promote copies once to the destination.
/// </summary>
public static class RenderStaging
{
    public const long ReserveBytes = 5L * 1024 * 1024 * 1024;

    public static string StagingDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoAutoTool",
            "out-tmp");

    public static string ResolvePartPath(string finalOutputPath, long estimatedBytes)
    {
        var destPart = finalOutputPath + ".part";
        try
        {
            var destRoot = Path.GetPathRoot(Path.GetFullPath(finalOutputPath));
            var localRoot = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
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
            var name = Path.GetFileName(finalOutputPath) + "." + Guid.NewGuid().ToString("N") + ".part";
            return Path.Combine(StagingDirectory, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException)
        {
            return destPart;
        }
    }

    public static long EstimateJobBytes(Template template, double durationSeconds) =>
        DiskSpaceEstimator.EstimateBytes(template, durationSeconds);

    public static void Promote(string partPath, string finalOutputPath)
    {
        var destPart = finalOutputPath + ".part";
        var staged = !string.Equals(
            Path.GetFullPath(partPath),
            Path.GetFullPath(destPart),
            StringComparison.OrdinalIgnoreCase);

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

    public static void TryDeletePair(string? partPath, string finalOutputPath)
    {
        PartFile.TryDelete(partPath);
        PartFile.TryDelete(finalOutputPath + ".part");
    }
}
