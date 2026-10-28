using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace VideoAutoTool.Core.FrameBatch;

/// <summary>
/// How a template layer gets its source files when the job is added to the queue.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LayerSourceMode
{
    /// <summary>Use Folder / FixedFile exactly as designed in the template.</summary>
    FromTemplate,

    /// <summary>A separate folder for this layer.</summary>
    Folder,

    /// <summary>Detect the files inside the shared auto folder (sub-folder or file-name keyword).</summary>
    AutoDetect,

    /// <summary>A path typed or pasted by the user: a single file or a folder.</summary>
    Path
}

/// <summary>
/// Turns the per-layer queue bindings (<see cref="FrameTemplateLayer.QueueSourceMode"/>) into the
/// Folder / FixedFile / NameFilter values that <see cref="FrameTemplatePlanner"/> already understands.
/// </summary>
public static class FrameSourceResolver
{
    private static readonly string[] DriverFolderNames = ["video", "audio", "nguon", "source", "main", "chinh"];

    private static IReadOnlyList<string> DriverExtensions(FrameTemplatePreset preset) =>
        preset.AudioOnlyDriver ? FrameDesign.DriverExtensions : FrameDesign.VideoExtensions;

    public static bool UsesAutoFolder(FrameTemplatePreset preset) =>
        preset.Layers.Any(layer => layer.Enabled && layer.QueueSourceMode == LayerSourceMode.AutoDetect);

    /// <summary>Applies the queue bindings to <paramref name="preset"/> (a clone). Returns one warning per layer that found nothing.</summary>
    public static IReadOnlyList<string> Apply(FrameTemplatePreset preset)
    {
        var warnings = new List<string>();
        var layers = preset.Layers.Where(layer => layer.Enabled && layer.Kind is not (FrameLayerKind.MainVideo or FrameLayerKind.StaticText)).ToList();
        foreach (var layer in layers)
        {
            layer.NameFilter = "";
            var path = (layer.QueueSourcePath ?? "").Trim().Trim('"');
            switch (layer.QueueSourceMode)
            {
                case LayerSourceMode.FromTemplate:
                    break;
                case LayerSourceMode.Folder:
                    if (!Directory.Exists(path))
                    {
                        warnings.Add($"Layer '{layer.Name}': folder không tồn tại: {path}");
                        break;
                    }

                    UseFolder(layer, path);
                    break;
                case LayerSourceMode.Path:
                    if (File.Exists(path))
                    {
                        layer.Pick = FrameLayerPick.Fixed;
                        layer.FixedFile = Path.GetFullPath(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        UseFolder(layer, path);
                    }
                    else
                    {
                        warnings.Add($"Layer '{layer.Name}': đường dẫn không tồn tại: {path}");
                    }

                    break;
                case LayerSourceMode.AutoDetect:
                    if (!Directory.Exists(preset.AutoSourceFolder))
                    {
                        warnings.Add($"Layer '{layer.Name}': chưa chọn folder chung để tự dò.");
                        break;
                    }

                    if (!AutoDetect(layer, layers, preset.AutoSourceFolder))
                    {
                        warnings.Add($"Layer '{layer.Name}': không tự dò được file trong {preset.AutoSourceFolder}.");
                    }

                    break;
            }
        }

        return warnings;
    }

    /// <summary>Driver (main) videos found in the shared auto folder, skipping files that belong to another layer.</summary>
    public static IReadOnlyList<string> FindDrivers(FrameTemplatePreset preset, string autoFolder)
    {
        if (!Directory.Exists(autoFolder))
        {
            return [];
        }

        foreach (var name in DriverFolderNames)
        {
            var sub = FindSubFolder(autoFolder, [name]);
            if (sub is not null)
            {
                var inSub = FramePlan.ListMedia(sub, DriverExtensions(preset));
                if (inSub.Count > 0)
                {
                    return inSub;
                }
            }
        }

        var otherKeys = preset.Layers
            .Where(layer => layer.Enabled && layer.Kind is not (FrameLayerKind.MainVideo or FrameLayerKind.StaticText))
            .SelectMany(layer => KeysFor(layer, preset.Layers))
            .ToList();
        return FramePlan.ListMedia(autoFolder, DriverExtensions(preset))
            .Where(file => !MatchesAny(Normalize(Path.GetFileNameWithoutExtension(file)), otherKeys))
            .ToList();
    }

    /// <summary>True when the file name contains any of the '|' separated keywords of <paramref name="filter"/>.</summary>
    public static bool MatchesFilter(string file, string filter) =>
        MatchesAny(Normalize(Path.GetFileNameWithoutExtension(file)), filter.Split('|', StringSplitOptions.RemoveEmptyEntries));

    private static void UseFolder(FrameTemplateLayer layer, string folder)
    {
        layer.Folder = Path.GetFullPath(folder);
        layer.FixedFile = "";
        if (layer.Pick == FrameLayerPick.Fixed)
        {
            layer.Pick = FrameLayerPick.RoundRobin;
        }
    }

    private static bool AutoDetect(FrameTemplateLayer layer, IReadOnlyList<FrameTemplateLayer> all, string root)
    {
        var keys = KeysFor(layer, all);
        var extensions = FrameTemplatePlanner.ExtensionsFor(layer.Kind);

        var sub = FindSubFolder(root, keys);
        if (sub is not null && FramePlan.ListMedia(sub, extensions).Count > 0)
        {
            UseFolder(layer, sub);
            return true;
        }

        var inRoot = FramePlan.ListMedia(root, extensions);
        var matched = inRoot.Where(file => MatchesAny(Normalize(Path.GetFileNameWithoutExtension(file)), keys)).ToList();
        if (matched.Count > 0)
        {
            UseFolder(layer, root);
            layer.NameFilter = string.Join('|', keys);
            return true;
        }

        // Without a keyword, only images and subtitles may use the whole folder, and only when no sibling layer shares the type.
        var sameKind = all.Count(other => ExtensionsOverlap(other.Kind, layer.Kind));
        if (layer.Kind is FrameLayerKind.FrameOverlay or FrameLayerKind.ImageOverlay or FrameLayerKind.Subtitle
            && sameKind == 1
            && inRoot.Count > 0)
        {
            UseFolder(layer, root);
            return true;
        }

        return false;
    }

    private static bool ExtensionsOverlap(FrameLayerKind a, FrameLayerKind b) =>
        FrameTemplatePlanner.ExtensionsFor(a).Intersect(FrameTemplatePlanner.ExtensionsFor(b), StringComparer.OrdinalIgnoreCase).Any();

    private static string? FindSubFolder(string root, IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Normalize(Path.GetFileName(dir));
                if (name == key || (key.Length >= 3 && name.Contains(key, StringComparison.Ordinal)))
                {
                    return dir;
                }
            }
        }

        return null;
    }

    private static bool MatchesAny(string normalizedName, IEnumerable<string> keys) =>
        keys.Any(key => key.Length >= 2 && normalizedName.Contains(key, StringComparison.Ordinal));

    /// <summary>Keywords for a layer: its own Id and name words first, then generic words for its kind.</summary>
    private static List<string> KeysFor(FrameTemplateLayer layer, IReadOnlyList<FrameTemplateLayer> all)
    {
        var keys = new List<string>();
        void Add(string? value)
        {
            var key = Normalize(value ?? "");
            if (key.Length >= 2 && !keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        Add(layer.Id);
        foreach (var word in SplitWords(layer.Name))
        {
            Add(word);
        }

        // Generic kind words are shared by every layer of that kind, so skip them when two layers would collide.
        var sameKind = all.Count(other => other.Enabled && other.Kind == layer.Kind);
        if (sameKind <= 1)
        {
            foreach (var alias in AliasesFor(layer.Kind))
            {
                Add(alias);
            }
        }

        return keys;
    }

    private static IEnumerable<string> AliasesFor(FrameLayerKind kind) => kind switch
    {
        FrameLayerKind.BackgroundVideo => ["bg", "background", "nen"],
        FrameLayerKind.FrameOverlay => ["frame", "khung"],
        FrameLayerKind.ImageOverlay => ["avatar", "anh", "image", "logo"],
        FrameLayerKind.LoopVideo => ["wave", "song", "loop"],
        FrameLayerKind.Subtitle => ["sub", "srt", "phude"],
        _ => []
    };

    private static IEnumerable<string> SplitWords(string text) =>
        text.Split([' ', '/', '(', ')', '-', '_', '.', ','], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= 3);

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch is 'đ' or 'Đ')
            {
                builder.Append('d');
            }
            else if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }
}
