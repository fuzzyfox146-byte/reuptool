using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Fonts;

public sealed class FontCatalog : IFontCatalog
{
    private readonly string _bundledDirectory;
    private readonly string _importedDirectory;

    public FontCatalog(string? bundledDirectory = null, string? importedDirectory = null)
    {
        _bundledDirectory = bundledDirectory ?? Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
        _importedDirectory = importedDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VideoAutoTool",
            "fonts");
    }

    public IReadOnlyList<FontEntry> ListAll()
    {
        var entries = new List<FontEntry>();
        entries.AddRange(ReadDirectory(_bundledDirectory, FontSource.Bundled));
        entries.AddRange(ReadDirectory(_importedDirectory, FontSource.Imported));
        entries.AddRange(ListSystemFonts());
        return entries
            .GroupBy(e => e.FamilyName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool Exists(StylePreset preset) => ResolveFilePath(preset) is not null || HasSystemFont(preset.Font);

    public string? ResolveFilePath(StylePreset preset)
    {
        return preset.FontSource switch
        {
            FontSource.Bundled => FindInDirectory(_bundledDirectory, preset.Font),
            FontSource.Imported => FindInDirectory(_importedDirectory, preset.Font),
            FontSource.System => null,
            _ => null
        };
    }

    public static string ImportedDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VideoAutoTool",
        "fonts");

    public static string ImportFont(string sourceFilePath)
    {
        Directory.CreateDirectory(ImportedDirectory);
        var dest = Path.Combine(ImportedDirectory, Path.GetFileName(sourceFilePath));
        File.Copy(sourceFilePath, dest, overwrite: true);
        return dest;
    }

    public static void DeleteImportedFont(string fileName)
    {
        var path = Path.Combine(ImportedDirectory, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static IEnumerable<FontEntry> ReadDirectory(string directory, FontSource source)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                     .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                 f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
        {
            var family = Path.GetFileNameWithoutExtension(file);
            yield return new FontEntry(family, source, file);
        }
    }

    private static IEnumerable<FontEntry> ListSystemFonts()
    {
        if (OperatingSystem.IsWindows())
        {
            var fontsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
            if (Directory.Exists(fontsFolder))
            {
                foreach (var file in Directory.EnumerateFiles(fontsFolder, "*.*", SearchOption.TopDirectoryOnly)
                             .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                         f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
                {
                    yield return new FontEntry(Path.GetFileNameWithoutExtension(file), FontSource.System, file);
                }
            }
        }
    }

    private static string? FindInDirectory(string directory, string familyName)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var exact = Directory.EnumerateFiles(directory)
            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
                .Equals(familyName, StringComparison.OrdinalIgnoreCase));
        return exact;
    }

    private static bool HasSystemFont(string familyName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var fontsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
        return Directory.Exists(fontsFolder) && Directory.EnumerateFiles(fontsFolder)
            .Any(f => Path.GetFileNameWithoutExtension(f).Contains(familyName, StringComparison.OrdinalIgnoreCase));
    }
}
