using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace YtFolderUploader;

public sealed class SettingsStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public string DirectoryPath { get; }
    public string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
    public string HistoryPath => Path.Combine(DirectoryPath, "history.json");
    public string? RecoveryNote { get; private set; }

    public SettingsStore()
    {
        DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YtFolderUploader");
        Directory.CreateDirectory(DirectoryPath);
    }

    public AppData Load()
    {
        var data = Read<AppData>(SettingsPath, "cấu hình");
        Normalize(data);
        var changed = EnableSavedSources(data);
        changed |= UploadTemplates.RepairNamedSchedules(data);
        changed |= UploadTemplates.SeedOnce(data);
        if (changed)
            Save(data);
        return data;
    }

    public static bool EnableSavedSources(AppData data)
    {
        if (data.SettingsRevision >= 1)
            return false;

        foreach (var channel in data.Channels)
        {
            foreach (var source in channel.Sources)
            {
                if (!string.IsNullOrWhiteSpace(source.VideoFolder))
                    source.Enabled = true;
            }
        }

        data.SettingsRevision = 1;
        return true;
    }

    public UploadHistory LoadHistory()
    {
        var history = Read<UploadHistory>(HistoryPath, "lịch sử đăng");
        history.Entries ??= new();
        return history;
    }

    public void Save(AppData data)
    {
        Normalize(data);
        Write(SettingsPath, data);
    }

    public void SaveHistory(UploadHistory history)
    {
        history.Entries ??= new();
        Write(HistoryPath, history);
    }

    public static void Normalize(AppData data)
    {
        data.Channels ??= new();
        data.Templates ??= new();
        if (data.MaxUploadsPerRun < 0)
            data.MaxUploadsPerRun = 0;

        foreach (var template in data.Templates)
        {
            if (string.IsNullOrWhiteSpace(template.Id))
                template.Id = Guid.NewGuid().ToString("N");
            template.Options ??= UploadOptions.CreateDefault();
            template.Options.Categories ??= new();
            template.Key = UploadTemplates.KeyOf(template.Options);
        }

        foreach (var channel in data.Channels)
        {
            if (string.IsNullOrWhiteSpace(channel.Id))
                channel.Id = Guid.NewGuid().ToString("N");
            channel.Enabled = true;
            channel.Sources ??= new();
            channel.Options ??= UploadOptions.CreateDefault();
            channel.Options.Categories ??= new();
            if (channel.Options.VideosPerDay < 1)
                channel.Options.VideosPerDay = 1;
            if (string.IsNullOrWhiteSpace(channel.Options.TimeZoneId))
                channel.Options.TimeZoneId = "SE Asia Standard Time";
            if (string.IsNullOrWhiteSpace(channel.Options.PrivacyMode))
                channel.Options.PrivacyMode = "scheduled";
            if (string.IsNullOrWhiteSpace(channel.Options.CategoryId))
                channel.Options.CategoryId = "24";
            if (channel.Options.ScheduleStartDate == default)
                channel.Options.ScheduleStartDate = DateOnly.FromDateTime(DateTime.Today);

            foreach (var source in channel.Sources)
            {
                if (string.IsNullOrWhiteSpace(source.Id))
                    source.Id = Guid.NewGuid().ToString("N");
                if (source.StartIndex < 0)
                    source.StartIndex = 0;
            }
        }
    }

    T Read<T>(string path, string label) where T : new()
    {
        if (!File.Exists(path))
            return new T();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
        }
        catch (Exception ex)
        {
            var badPath = path + ".bad";
            try
            {
                File.Copy(path, badPath, overwrite: true);
            }
            catch (IOException)
            {
                // The broken file stays in place; a new file is written on the next save.
            }

            RecoveryNote = $"Không đọc được {label}. Bản hỏng được chép sang {badPath}. Chi tiết: {ex.Message}";
            return new T();
        }
    }

    static void Write<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json, Utf8);
        File.Move(temp, path, overwrite: true);
    }
}
