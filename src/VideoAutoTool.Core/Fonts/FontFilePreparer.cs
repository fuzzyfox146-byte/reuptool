using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Fonts;

public static class FontFilePreparer
{
    public static string PrepareFontsDirectory(string tempDirectory, StylePreset preset, IFontCatalog catalog)
    {
        var fontsDir = Path.Combine(tempDirectory, "fonts");
        Directory.CreateDirectory(fontsDir);

        var source = catalog.ResolveFilePath(preset);
        if (source is not null && File.Exists(source))
        {
            var dest = Path.Combine(fontsDir, Path.GetFileName(source));
            File.Copy(source, dest, overwrite: true);
        }

        return fontsDir;
    }
}
