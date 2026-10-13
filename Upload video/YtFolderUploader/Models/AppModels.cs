using System.Text.Json.Serialization;

namespace YtFolderUploader;

public sealed class AppData
{
    public int MaxUploadsPerRun { get; set; } = 10;
    public List<ChannelProfile> Channels { get; set; } = new();
}

public sealed class ChannelProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string CredentialText { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string YoutubeChannelId { get; set; } = "";
    public string YoutubeChannelTitle { get; set; } = "";
    public List<VideoSource> Sources { get; set; } = new();
    public UploadOptions Options { get; set; } = UploadOptions.CreateDefault();

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(YoutubeChannelTitle)
            && !string.Equals(YoutubeChannelTitle, Name, StringComparison.Ordinal))
            return $"{Name} — {YoutubeChannelTitle}";
        return string.IsNullOrWhiteSpace(Name) ? "(chưa đặt tên)" : Name;
    }
}

public sealed class VideoSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string VideoFolder { get; set; } = "";
    public string ThumbFolder { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int StartIndex { get; set; }

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? VideoFolder : Name;
}

public sealed class UploadOptions
{
    public int VideosPerDay { get; set; } = 3;
    public string TimeSlotsText { get; set; } = "09:00\r\n15:00\r\n21:00";
    public string TimeZoneId { get; set; } = "SE Asia Standard Time";
    public DateOnly ScheduleStartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string PrivacyMode { get; set; } = "scheduled";
    public string DescriptionTemplate { get; set; } = "{title}";
    public string Tags { get; set; } = "";
    public string CategoryId { get; set; } = "24";
    public string CategoryRegion { get; set; } = "JP";
    public List<CategoryChoice> Categories { get; set; } = new();
    public string DefaultLanguage { get; set; } = "ja";
    public string DefaultAudioLanguage { get; set; } = "";
    public bool MadeForKids { get; set; }
    public bool ContainsSyntheticMedia { get; set; }
    public bool Embeddable { get; set; } = true;
    public string License { get; set; } = "youtube";
    public bool PublicStatsViewable { get; set; } = true;
    public bool NotifySubscribers { get; set; }
    public bool HasPaidProductPlacement { get; set; }
    public bool SendRecordingDate { get; set; }
    public DateOnly RecordingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string LocalizationLanguage { get; set; } = "";
    public string LocalizationTitle { get; set; } = "{title}";
    public string LocalizationDescription { get; set; } = "{title}";
    public bool AutoLevels { get; set; }
    public bool Stabilize { get; set; }
    public string PlaylistId { get; set; } = "";

    [JsonIgnore]
    public bool IsScheduled => string.IsNullOrWhiteSpace(PrivacyMode) || PrivacyMode == "scheduled";

    public static UploadOptions CreateDefault() => new();

    public UploadOptions Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, SettingsStore.JsonOptions);
        return System.Text.Json.JsonSerializer.Deserialize<UploadOptions>(json, SettingsStore.JsonOptions) ?? CreateDefault();
    }
}

public sealed class CategoryChoice
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";

    public override string ToString() => string.IsNullOrWhiteSpace(Title) ? Id : $"{Title} ({Id})";

    public static List<CategoryChoice> Defaults() =>
    [
        new() { Id = "1", Title = "Film & Animation" },
        new() { Id = "2", Title = "Autos & Vehicles" },
        new() { Id = "10", Title = "Music" },
        new() { Id = "15", Title = "Pets & Animals" },
        new() { Id = "17", Title = "Sports" },
        new() { Id = "19", Title = "Travel & Events" },
        new() { Id = "20", Title = "Gaming" },
        new() { Id = "22", Title = "People & Blogs" },
        new() { Id = "23", Title = "Comedy" },
        new() { Id = "24", Title = "Entertainment" },
        new() { Id = "25", Title = "News & Politics" },
        new() { Id = "26", Title = "Howto & Style" },
        new() { Id = "27", Title = "Education" },
        new() { Id = "28", Title = "Science & Technology" },
        new() { Id = "29", Title = "Nonprofits & Activism" }
    ];
}

public sealed class HistoryEntry
{
    public string ChannelProfileId { get; set; } = "";
    public string VideoPath { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime FileModifiedUtc { get; set; }
    public string Index { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string Title { get; set; } = "";
    public string YoutubeVideoId { get; set; } = "";
    public DateTimeOffset? PublishAt { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed class UploadHistory
{
    public List<HistoryEntry> Entries { get; set; } = new();

    public HistoryEntry? FindExact(string channelId, string videoPath, long fileSize) =>
        Entries.LastOrDefault(entry =>
            entry.ChannelProfileId == channelId
            && string.Equals(entry.VideoPath, videoPath, StringComparison.OrdinalIgnoreCase)
            && entry.FileSize == fileSize
            && !string.IsNullOrWhiteSpace(entry.YoutubeVideoId));

    public HistoryEntry? FindPath(string channelId, string videoPath) =>
        Entries.LastOrDefault(entry =>
            entry.ChannelProfileId == channelId
            && string.Equals(entry.VideoPath, videoPath, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(entry.YoutubeVideoId));
}

public sealed class QueueItem
{
    public bool Selected { get; set; }
    public ChannelProfile Channel { get; set; } = null!;
    public string SourceName { get; set; } = "";
    public string Index { get; set; } = "";
    public string VideoPath { get; set; } = "";
    public string? ThumbPath { get; set; }
    public string Title { get; set; } = "";
    public bool TitleTrimmed { get; set; }
    public DateTimeOffset? PublishAt { get; set; }
    public string YoutubeVideoId { get; set; } = "";
    public string Status { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime FileModifiedUtc { get; set; }
}

public sealed class LabeledCode
{
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public override string ToString() => Label;
}

public sealed class ScanResult
{
    public List<ScannedVideo> Videos { get; } = new();
    public List<string> Notes { get; } = new();
}

public sealed class ScannedVideo
{
    public string Index { get; init; } = "";
    public int IndexNumber { get; init; }
    public string VideoPath { get; init; } = "";
    public string? ThumbPath { get; init; }
    public string Title { get; init; } = "";
    public bool TitleTrimmed { get; init; }
    public bool MissingThumb { get; init; }
    public long FileSize { get; init; }
    public DateTime FileModifiedUtc { get; init; }
}

public sealed class SchedulePlan
{
    public List<DateTimeOffset> Times { get; } = new();
    public string? Warning { get; set; }
}

public sealed class ParsedCredential
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RefreshToken { get; set; }
    public string? ChannelId { get; set; }
    public string? ChannelTitle { get; set; }
}

public sealed class UploadOneResult
{
    public bool VideoUploaded { get; init; }
    public string? VideoId { get; init; }
    public bool StopBatch { get; init; }
    public string Status { get; init; } = "";
    public string? Warning { get; init; }
}

public readonly record struct UploadProgress(int Percent, string Text);

public readonly record struct ChannelIdentity(string Id, string Title);
