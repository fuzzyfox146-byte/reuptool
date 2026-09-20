using System.Text.Json;

namespace VideoAutoTool.Core.Templates;

public static class TemplateStore
{
    public const int CurrentSchemaVersion = 1;

    public static Template Load(string path)
    {
        var json = File.ReadAllText(path);
        return Deserialize(json);
    }

    public static Template Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("schemaVersion", out var versionElement) &&
            versionElement.TryGetInt32(out var version) &&
            version > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Template schemaVersion {version} is newer than supported version {CurrentSchemaVersion}.");
        }

        var template = JsonSerializer.Deserialize<Template>(json, TemplateJsonContext.Options)
            ?? throw new InvalidOperationException("Template JSON is empty.");
        MigrateLegacySubBox(template);
        return template;
    }

    public static void Save(string path, Template template)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        template.SchemaVersion = CurrentSchemaVersion;
        var json = JsonSerializer.Serialize(template, TemplateJsonContext.Options);
        File.WriteAllText(path, json);
    }

    public static Template Clone(Template template)
    {
        var json = JsonSerializer.Serialize(template, TemplateJsonContext.Options);
        return Deserialize(json);
    }

    private static void MigrateLegacySubBox(Template template)
    {
        var subLayer = template.Layers.FirstOrDefault(l => l.Type == LayerType.Subtitle);
        if (subLayer is null)
        {
            return;
        }

        // Legacy templates may still carry box on layer via extension data ignored by serializer.
        // Ensure every preset has a box; copy from first preset defaults if missing.
        foreach (var preset in template.StylePresets)
        {
            if (preset.Box.Width <= 0)
            {
                preset.Box = new SubtitleBox { X = 175, Y = 470, Width = 480, Height = 220, Anchor = Anchor.TopLeft };
            }
        }
    }
}
