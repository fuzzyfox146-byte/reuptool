namespace YtFolderUploader;

public readonly record struct TemplateSync(UploadTemplate Template, bool Created);

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
        copy.PlaylistId = copy.PlaylistId.Trim();
        return System.Text.Json.JsonSerializer.Serialize(copy, SettingsStore.JsonOptions);
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
            Name = NextName(data, channel.Options),
            Key = key,
            Options = channel.Options.Clone()
        };
        data.Templates.Add(created);
        channel.TemplateId = created.Id;
        return new TemplateSync(created, true);
    }

    public static void SaveInto(AppData data, ChannelProfile channel, Func<ChannelProfile, bool>? skip = null)
    {
        data.Templates ??= new();
        var template = data.Templates.FirstOrDefault(item => item.Id == channel.TemplateId);
        if (template == null || template.NameIsCustom)
            return;

        template.Options = channel.Options.Clone();
        template.Key = KeyOf(template.Options);
        template.Name = UniqueName(data, template.Options, template.Id);

        foreach (var other in data.Channels)
        {
            if (other.TemplateId != template.Id || ReferenceEquals(other, channel))
                continue;
            if (skip != null && skip(other))
                continue;
            other.Options = template.Options.Clone();
        }
    }

    public static UploadTemplate AddCopy(AppData data, ChannelProfile channel)
    {
        data.Templates ??= new();
        var created = new UploadTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            Options = channel.Options.Clone()
        };
        created.Key = KeyOf(created.Options);
        created.Name = UniqueName(data, created.Options, created.Id);
        data.Templates.Add(created);
        channel.TemplateId = created.Id;
        return created;
    }

    public static bool SeedOnce(AppData data)
    {
        if (data.SettingsRevision >= 2)
            return LinkExisting(data);

        EnsureMissing(data);
        data.SettingsRevision = 2;
        return true;
    }

    public static bool LinkExisting(AppData data)
    {
        data.Templates ??= new();
        var changed = false;
        foreach (var channel in data.Channels)
        {
            if (data.Templates.Any(template => template.Id == channel.TemplateId))
                continue;
            var match = data.Templates.FirstOrDefault(template => template.Key == KeyOf(channel.Options));
            if (match == null)
                continue;
            channel.TemplateId = match.Id;
            changed = true;
        }

        return changed;
    }

    public static bool RepairNamedSchedules(AppData data)
    {
        data.Templates ??= new();
        var changed = false;
        foreach (var template in data.Templates)
        {
            if (!TryReadScheduleName(template.Name, out var perDay, out var times))
                continue;
            var wanted = string.Join("\n", times);
            var stored = NormalizeLines(template.Options.TimeSlotsText);
            if (template.Options.VideosPerDay == perDay && stored == wanted)
                continue;
            template.Options.VideosPerDay = perDay;
            template.Options.TimeSlotsText = string.Join("\r\n", times);
            template.Key = KeyOf(template.Options);
            changed = true;
        }

        return changed;
    }

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

    public static void Apply(ChannelProfile channel, UploadTemplate template)
    {
        channel.Options = template.Options.Clone();
        channel.TemplateId = template.Id;
    }

    static string NextName(AppData data, UploadOptions options)
    {
        return UniqueName(data, options, null);
    }

    static string UniqueName(AppData data, UploadOptions options, string? exceptId)
    {
        var times = options.TimeSlotsText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var clock = times.Length == 0 ? "không giờ" : string.Join(", ", times);
        var baseName = $"{options.VideosPerDay} video/ngày · {clock}";
        if (baseName.Length > 80)
            baseName = baseName[..77] + "...";
        if (!Taken(data, baseName, exceptId))
            return baseName;

        var n = 2;
        while (Taken(data, $"{baseName} ({n})", exceptId))
            n++;
        return $"{baseName} ({n})";
    }

    static bool Taken(AppData data, string name, string? exceptId) =>
        data.Templates.Any(template => template.Id != exceptId && template.Name == name);

    static string NormalizeLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
}
