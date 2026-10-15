namespace YtFolderUploader;

public static class ScheduleDefer
{
    public static DateOnly NextPublicDay(DateTimeOffset? publishAt, string? timeZoneId, DateTimeOffset now)
    {
        var zone = TimeZones.Resolve(timeZoneId);
        var basis = publishAt ?? now;
        var local = TimeZoneInfo.ConvertTime(basis, zone);
        return DateOnly.FromDateTime(local.DateTime).AddDays(1);
    }

    public static DateTimeOffset AddOneDay(DateTimeOffset when, string? timeZoneId)
    {
        var zone = TimeZones.Resolve(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(when, zone);
        var shifted = local.DateTime.AddDays(1);
        return new DateTimeOffset(shifted, zone.GetUtcOffset(shifted));
    }

    public static DateTimeOffset StartOfDay(DateOnly day, string? timeZoneId)
    {
        var zone = TimeZones.Resolve(timeZoneId);
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
