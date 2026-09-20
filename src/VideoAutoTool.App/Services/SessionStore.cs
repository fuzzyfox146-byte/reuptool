using System.Text.Json;
using VideoAutoTool.Core.Templates;

namespace VideoAutoTool.App.Services;

public sealed class FolderOverride
{
    public string RoleId { get; set; } = "";

    public string Folder { get; set; } = "";
}

public sealed class AppSession
{
    public int Version { get; set; } = 1;

    public Template Template { get; set; } = new();

    public string? DesignPath { get; set; }

    public string? SelectedDesignPath { get; set; }

    public string RootFolder { get; set; } = "";

    public List<FolderOverride> FolderOverrides { get; set; } = [];

    public string FfmpegPath { get; set; } = "";

    public int ParallelCount { get; set; } = 2;

    public int BackgroundScalePercent { get; set; } = 150;
}

public enum UnsavedCloseChoice
{
    Save,
    Discard,
    Cancel
}

/// <summary>
/// Last Ctrl+S snapshot: design + source folders + settings.
/// </summary>
public static class SessionStore
{
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VideoAutoTool",
        "session.json");

    public static AppSession? TryLoad()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSession>(json, TemplateJsonContext.Options);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(AppSession session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(session, TemplateJsonContext.Options);
        File.WriteAllText(FilePath, json);
    }

    public static string Fingerprint(AppSession session) =>
        JsonSerializer.Serialize(session, TemplateJsonContext.Options);
}
