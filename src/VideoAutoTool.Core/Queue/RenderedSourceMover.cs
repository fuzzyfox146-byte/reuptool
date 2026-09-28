namespace VideoAutoTool.Core.Queue;

/// <summary>
/// Moves finished source videos into a <c>rendered</c> folder next to the first video.
/// </summary>
public static class RenderedSourceMover
{
    public const string FolderName = "rendered";

    public static string FolderFor(string firstDriverPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstDriverPath);
        var parent = Path.GetDirectoryName(Path.GetFullPath(firstDriverPath));
        if (string.IsNullOrWhiteSpace(parent))
        {
            throw new ArgumentException("Driver path has no parent folder.", nameof(firstDriverPath));
        }

        return Path.Combine(parent, FolderName);
    }

    public static IReadOnlyList<string> Move(IEnumerable<string> driverPaths, string renderedFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(renderedFolder);
        Directory.CreateDirectory(renderedFolder);
        var moved = new List<string>();
        foreach (var path in driverPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            var dest = UniquePath(Path.Combine(renderedFolder, Path.GetFileName(path)));
            File.Move(path, dest);
            moved.Add(dest);
        }

        return moved;
    }

    private static string UniquePath(string dest)
    {
        if (!File.Exists(dest))
        {
            return dest;
        }

        var dir = Path.GetDirectoryName(dest) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(dest);
        var ext = Path.GetExtension(dest);
        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(dir, $"{stem}-{Guid.NewGuid():N}{ext}");
    }
}
