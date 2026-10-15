using System.Text.Json;

namespace HdDownloader.Services;

public sealed class PackSettings
{
    public string YtDlpPath { get; set; } = "";

    public string CookiesPath { get; set; } = "";

    public string RootFolder { get; set; } = "";

    public string Quality { get; set; } = "720";

    public int Parallel { get; set; } = 3;

    public bool OrderByViews { get; set; } = PackSettingsStore.DefaultOrderByViews;

    public List<PackChannelSettings> Channels { get; set; } = [];
}

public sealed class PackChannelSettings
{
    public string FolderName { get; set; } = "HD036";

    public string ChannelUrl { get; set; } = "";

    public int PlaylistStart { get; set; } = 1;

    public int PlaylistEnd { get; set; } = 10;

    public int NameStart { get; set; } = 1;
}

public static class PackSettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string AppFolder { get; set; } = "HdDownloader";

    public static bool DefaultOrderByViews { get; set; }

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppFolder,
        "settings.json");

    public static PackSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new PackSettings();
            }

            return JsonSerializer.Deserialize<PackSettings>(File.ReadAllText(FilePath)) ?? new PackSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new PackSettings();
        }
    }

    public static void Save(PackSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Json));
    }
}
