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
    /// First launch of the GPU edition copies the original session so both apps start from the same setup.
    /// Later saves stay in VideoAutoTool-Gpu and do not overwrite the 1.0.25 session.
    /// </summary>
    private static void CopyLegacySessionOnce()
    {
        if (!ProductEdition.IsGpuEdition || File.Exists(FilePath))
        {
            return;
        }

        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VideoAutoTool",
            "session.json");
        if (!File.Exists(legacy))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.Copy(legacy, FilePath);
    }
}
