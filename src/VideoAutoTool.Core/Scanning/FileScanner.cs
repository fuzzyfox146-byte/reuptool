namespace VideoAutoTool.Core.Scanning;

public static class FileScanner
{
    private static readonly HashSet<string> IgnoredNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Thumbs.db"
    };

    public static IReadOnlyList<ScannedFile> ScanFolder(
        string root,
        string folder,
        IEnumerable<string> extensions,
        string numberPattern)
    {
        var absoluteFolder = ResolveFolder(root, folder);
        if (!Directory.Exists(absoluteFolder))
        {
            return [];
        }

        var extSet = new HashSet<string>(extensions.Select(NormalizeExtension), StringComparer.OrdinalIgnoreCase);
        var files = Directory.EnumerateFiles(absoluteFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => ShouldInclude(path, extSet))
            .Select(path =>
            {
                var relative = Path.GetRelativePath(root, path);
                var number = NumberExtractor.Extract(Path.GetFileName(path), numberPattern);
                return new ScannedFile(path, relative, number, Path.GetFileName(path));
            })
            .OrderBy(f => f, Comparer<ScannedFile>.Create((a, b) =>
            {
                var numCmp = Nullable.Compare(a.Number, b.Number);
                return numCmp != 0 ? numCmp : NaturalSort.Compare(a.FileName, b.FileName);
            }))
            .ToList();

        return files;
    }

    /// <summary>
    /// Counts matching files in a folder without reading file contents
    /// and without walking subfolders (TopDirectoryOnly).
    /// </summary>
    public static int CountMatchingFiles(string folder, IEnumerable<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return 0;
        }

        var extSet = new HashSet<string>(extensions.Select(NormalizeExtension), StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Count(path => ShouldInclude(path, extSet));
    }

    public static string ResolveFolder(string root, string folder)
    {
        if (Path.IsPathRooted(folder))
        {
            return folder;
        }

        return Path.Combine(root, folder);
    }

    private static bool ShouldInclude(string path, HashSet<string> extensions)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith('.') || name.StartsWith("~$", StringComparison.Ordinal))
        {
            return false;
        }

        if (IgnoredNames.Contains(name))
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        if (ext.Equals(".part", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".tmp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return extensions.Contains(NormalizeExtension(ext));
    }

    private static string NormalizeExtension(string extension) =>
        extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
}
