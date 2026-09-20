using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Fonts;

public sealed record FontEntry(string FamilyName, FontSource Source, string? FilePath);

public interface IFontCatalog
{
    IReadOnlyList<FontEntry> ListAll();

    bool Exists(StylePreset preset);

    string? ResolveFilePath(StylePreset preset);
}
