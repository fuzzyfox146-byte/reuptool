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

    public int QueueBatchSize { get; set; } = 9;

    public int RenderCount { get; set; } = 3;

    public int BackgroundScalePercent { get; set; } = 150;

    public int RenderHeight { get; set; } = 720;

    public string YtDlpPath { get; set; } = "";

    public string CookiesPath { get; set; } = "";

    public int DownloadParallel { get; set; } = 2;

    public string VideoQuality { get; set; } = "144";

    public List<DownloadChannelSession> DownloadChannels { get; set; } = [];
}

public sealed class DownloadChannelSession
{
    public string ParentFolder { get; set; } = "";

    public string ChannelUrl { get; set; } = "";

    public int PlaylistStart { get; set; } = 1;

    public int PlaylistEnd { get; set; } = 20;

    public int NameStart { get; set; } = 1;

    public string Language { get; set; } = "en";
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
        ProductEdition.DataFolder,
        "session.json");

    public static AppSession? TryLoad()
    {
        CopyLegacySessionOnce();
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

    /// <summary>
    /// First launch of a side-by-side edition copies an existing session.
    /// Opt copies VideoAutoTool-Gpu (then the original). GPU copies the original.
    /// Later saves stay in that edition folder and do not overwrite the others.
    /// </summary>
    private static void CopyLegacySessionOnce()
    {
        if (File.Exists(FilePath) || (!ProductEdition.IsGpuEdition && !ProductEdition.IsOptEdition))
        {
            return;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sources = ProductEdition.IsOptEdition
            ? new[] { "VideoAutoTool-Gpu", "VideoAutoTool" }
            : new[] { "VideoAutoTool" };
        foreach (var folder in sources)
        {
            var legacy = Path.Combine(appData, folder, "session.json");
            if (!File.Exists(legacy))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.Copy(legacy, FilePath);
            return;
        }
    }
}
