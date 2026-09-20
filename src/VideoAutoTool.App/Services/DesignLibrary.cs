namespace VideoAutoTool.App.Services;

public sealed record DesignFileInfo(string Path, string DisplayName, DateTime LastWriteUtc);

/// <summary>
/// Saved design JSON files live in %APPDATA%\VideoAutoTool\designs\.
/// Each save creates a new file (never overwrites).
/// </summary>
public static class DesignLibrary
{
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VideoAutoTool",
        "designs");

    public static IReadOnlyList<DesignFileInfo> List()
    {
        Directory.CreateDirectory(DirectoryPath);
        return Directory.EnumerateFiles(DirectoryPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => new DesignFileInfo(
                path,
                Path.GetFileNameWithoutExtension(path),
                File.GetLastWriteTimeUtc(path)))
            .OrderByDescending(f => f.LastWriteUtc)
            .ToList();
    }

    public static string NextSavePath(string? designName)
    {
        Directory.CreateDirectory(DirectoryPath);
        var baseName = Sanitize(string.IsNullOrWhiteSpace(designName) ? "design" : designName);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(DirectoryPath, $"{baseName}_{stamp}.json");
        var i = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(DirectoryPath, $"{baseName}_{stamp}_{i}.json");
            i++;
        }

        return path;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }
}
