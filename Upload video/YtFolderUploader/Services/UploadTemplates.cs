namespace YtFolderUploader;

public readonly record struct TemplateSync(UploadTemplate Template, bool Created);

/// <summary>
/// Mẫu đăng = một bộ cấu hình có tên cố định. Tên không tự đổi khi sửa giờ; dropdown hiện kèm giờ thật.
/// Kênh có TemplateId rỗng là "Riêng kênh này": không bao giờ tự gắn hay tự tạo mẫu.
/// Ngày bắt đầu, ngày ghi hình và playlist là của riêng từng kênh nên không đi theo mẫu.
/// </summary>
public static class UploadTemplates
{
    public static string KeyOf(UploadOptions options)
    {
        var copy = options.Clone();
        copy.Categories = new();
        copy.TimeSlotsText = NormalizeLines(copy.TimeSlotsText);
        copy.DescriptionTemplate = NormalizeLines(copy.DescriptionTemplate);
        copy.Tags = copy.Tags.Trim();
        copy.LocalizationTitle = copy.LocalizationTitle.Trim();
        copy.LocalizationDescription = NormalizeLines(copy.LocalizationDescription);
        // Các mục riêng từng kênh không được làm hai mẫu giống hệt trông như khác nhau.
        copy.PlaylistId = "";
        copy.ScheduleStartDate = default;
        copy.RecordingDate = default;
        return System.Text.Json.JsonSerializer.Serialize(copy, SettingsStore.JsonOptions);
    }

    /// <summary>Bản sao cấu hình mẫu, giữ lại các mục riêng của kênh đích.</summary>
    public static UploadOptions CloneFor(UploadOptions shared, UploadOptions? channelOwn)
    {
        var copy = shared.Clone();
        if (channelOwn != null)
        {
            copy.ScheduleStartDate = channelOwn.ScheduleStartDate;
            copy.RecordingDate = channelOwn.RecordingDate;
            copy.PlaylistId = channelOwn.PlaylistId;
        }

        return copy;
    }

    public static TemplateSync Sync(AppData data, ChannelProfile channel)
    {
        data.Templates ??= new();
        var key = KeyOf(channel.Options);
        var existing = data.Templates.FirstOrDefault(template => template.Key == key);
        if (existing != null)
        {
            channel.TemplateId = existing.Id;
            return new TemplateSync(existing, false);
        }

        var created = new UploadTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = DefaultName(data),
            Key = key,
            Options = channel.Options.Clone()
        };
        data.Templates.Add(created);
        channel.TemplateId = created.Id;
        return new TemplateSync(created, true);
    }

    /// <summary>Lưu cấu hình đang sửa vào đúng mẫu của kênh và đổ sang các kênh dùng chung mẫu. Không đổi tên mẫu.</summary>
    public static void SaveInto(AppData data, ChannelProfile channel, Func<ChannelProfile, bool>? skip = null)
    {
        data.Templates ??= new();
        if (string.IsNullOrEmpty(channel.TemplateId))
            return;
        var template = data.Templates.FirstOrDefault(item => item.Id == channel.TemplateId);
        if (template == null)
            return;

        template.Options = channel.Options.Clone();
        template.Key = KeyOf(template.Options);

        foreach (var other in data.Channels)
        {
            if (other.TemplateId != template.Id || ReferenceEquals(other, channel))
                continue;
            if (skip != null && skip(other))
                continue;
            other.Options = CloneFor(template.Options, other.Options);
        }
    }

    public static UploadTemplate AddCopy(AppData data, ChannelProfile channel, string? name = null)
    {
        data.Templates ??= new();
        var created = new UploadTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            Options = channel.Options.Clone()
        };
        created.Key = KeyOf(created.Options);
        if (string.IsNullOrWhiteSpace(name))
        {
            created.Name = DefaultName(data);
        }
        else
        {
            created.Name = UniqueName(data, name.Trim());
            created.NameIsCustom = true;
        }

        data.Templates.Add(created);
        channel.TemplateId = created.Id;
        return created;
    }

    /// <summary>Chạy một lần khi mở bản mới: gộp các mẫu trùng nhau và đặt lại tên theo kiểu "Mẫu N".</summary>
    public static bool SeedOnce(AppData data)
    {
        var changed = false;
        if (data.SettingsRevision < 2)
        {
            EnsureMissing(data);
            data.SettingsRevision = 2;
            changed = true;
        }

        if (data.SettingsRevision < 3)
        {
            Consolidate(data);
            data.SettingsRevision = 3;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Gộp các mẫu có cấu hình giống hệt (bỏ qua ngày và playlist riêng từng kênh) thành một.
    /// Mẫu có tên tự đặt được giữ lại. Kênh trỏ vào mẫu bị gộp được nối sang mẫu giữ lại.
    /// </summary>
    public static bool Consolidate(AppData data)
    {
        data.Templates ??= new();
        var changed = false;
        foreach (var template in data.Templates)
            template.Key = KeyOf(template.Options);

        var groups = data.Templates.GroupBy(template => template.Key).Where(group => group.Count() > 1).ToList();
        foreach (var group in groups)
        {
            var keep = group.FirstOrDefault(template => template.NameIsCustom && !IsAutoScheduleName(template.Name))
                ?? group.First();
            foreach (var duplicate in group.Where(template => !ReferenceEquals(template, keep)).ToList())
            {
                foreach (var channel in data.Channels.Where(channel => channel.TemplateId == duplicate.Id))
                    channel.TemplateId = keep.Id;
                data.Templates.Remove(duplicate);
                changed = true;
            }
        }

        foreach (var template in data.Templates.ToList())
        {
            if (!IsAutoScheduleName(template.Name))
                continue;
            template.Name = DefaultName(data);
            template.NameIsCustom = false;
            changed = true;
        }

        return changed;
    }

    public static bool IsAutoScheduleName(string name) => TryReadScheduleName(name, out _, out _);

    public static bool TryReadScheduleName(string name, out int perDay, out string[] times)
    {
        perDay = 0;
        times = [];
        var match = System.Text.RegularExpressions.Regex.Match(
            name.Trim(),
            @"^(\d+) video/ngày · (.+?)(?: \(\d+\))?$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out perDay) || perDay < 1)
            return false;
        times = match.Groups[2].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return times.Length > 0 && times.All(time => System.Text.RegularExpressions.Regex.IsMatch(time, @"^\d{1,2}:\d{2}$"));
    }

    public static bool EnsureMissing(AppData data)
    {
        data.Templates ??= new();
        var changed = false;
        foreach (var channel in data.Channels)
        {
            var previousId = channel.TemplateId;
            var sync = Sync(data, channel);
            if (sync.Created || previousId != channel.TemplateId)
                changed = true;
        }

        return changed;
    }

    /// <summary>Dùng mẫu cho kênh. Giữ ngày bắt đầu, ngày ghi hình và playlist của kênh.</summary>
    public static void Apply(ChannelProfile channel, UploadTemplate template)
    {
        channel.Options = CloneFor(template.Options, channel.Options);
        channel.TemplateId = template.Id;
    }

    /// <summary>Bỏ liên kết mẫu. Kênh giữ nguyên cấu hình hiện tại và không còn đi theo mẫu nào.</summary>
    public static void Detach(ChannelProfile channel) => channel.TemplateId = "";

    static string DefaultName(AppData data)
    {
        var n = data.Templates.Count + 1;
        while (Taken(data, $"Mẫu {n}", null))
            n++;
        return $"Mẫu {n}";
    }

    static string UniqueName(AppData data, string baseName)
    {
        if (!Taken(data, baseName, null))
            return baseName;

        var n = 2;
        while (Taken(data, $"{baseName} ({n})", null))
            n++;
        return $"{baseName} ({n})";
    }

    static bool Taken(AppData data, string name, string? exceptId) =>
        data.Templates.Any(template => template.Id != exceptId && template.Name == name);

    static string NormalizeLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
}
