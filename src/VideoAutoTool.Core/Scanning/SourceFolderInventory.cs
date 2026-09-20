using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.Core.Scanning;

public enum SourceFolderKind
{
    Driver,
    Background,
    Avatar,
    Soundwave,
    Subtitle,
    Output,
    Other
}

/// <summary>
/// A template source slot (role + relative/absolute folder) used by the Source tab.
/// Does not inspect file contents.
/// </summary>
public sealed record SourceFolderSlot(
    SourceFolderKind Kind,
    string RoleId,
    string Folder,
    IReadOnlyList<string> Extensions,
    bool UsedForRender);

/// <summary>
/// Lightweight folder status: existence + top-level file count only.
/// </summary>
public sealed record SourceFolderStatus(
    SourceFolderKind Kind,
    string RoleId,
    string Folder,
    string AbsolutePath,
    IReadOnlyList<string> Extensions,
    bool UsedForRender,
    bool FolderExists,
    int FileCount,
    bool IsReady);

public static class SourceFolderInventory
{
    public static IReadOnlyList<SourceFolderSlot> FromTemplate(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var slots = new List<SourceFolderSlot>
        {
            new(
                SourceFolderKind.Driver,
                "driver",
                template.Driver.Folder,
                template.Driver.Extensions,
                UsedForRender: true)
        };

        foreach (var layer in template.Layers)
        {
            if (layer.Source is null) continue;

            var kind = layer.Type switch
            {
                LayerType.BackgroundChain => SourceFolderKind.Background,
                LayerType.Image => SourceFolderKind.Avatar,
                LayerType.LoopVideo => SourceFolderKind.Soundwave,
                LayerType.Subtitle => SourceFolderKind.Subtitle,
                _ => SourceFolderKind.Other
            };

            slots.Add(new(
                kind,
                layer.Id,
                layer.Source.Folder,
                layer.Source.Extensions,
                UsedForRender: kind != SourceFolderKind.Other && layer.Visible));
        }

        slots.Add(new(
            SourceFolderKind.Output,
            "output",
            template.Output.Folder,
            [],
            UsedForRender: true));

        return slots;
    }

    public static SourceFolderStatus Inspect(string root, SourceFolderSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        var absolute = string.IsNullOrWhiteSpace(root)
            ? FileScanner.ResolveFolder(Environment.CurrentDirectory, slot.Folder)
            : FileScanner.ResolveFolder(root, slot.Folder);

        var exists = Directory.Exists(absolute);
        var count = slot.Kind == SourceFolderKind.Output
            ? 0
            : FileScanner.CountMatchingFiles(absolute, slot.Extensions);

        var ready = slot.Kind == SourceFolderKind.Output
            ? exists
            : exists && count > 0;

        return new SourceFolderStatus(
            slot.Kind,
            slot.RoleId,
            slot.Folder,
            absolute,
            slot.Extensions,
            slot.UsedForRender,
            exists,
            count,
            ready);
    }
}
