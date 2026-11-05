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

    public static DateOnly Today(string? timeZoneId, DateTimeOffset now)
    {
        var zone = TimeZones.Resolve(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }

    /// <summary>
    /// Lùi lịch của kênh sang ngày sau. Mỗi ngày thật chỉ lùi một lần: bấm đăng nhiều lần
    /// hoặc nhiều video cùng lỗi trong một lượt cũng không làm lịch nhảy thêm ngày.
    /// Trả về true nếu lần gọi này thật sự lùi lịch.
    /// </summary>
    public static bool TryDefer(ChannelProfile channel, DateTimeOffset? publishAt, DateTimeOffset now)
    {
        var today = Today(channel.Options.TimeZoneId, now);
        if (channel.UploadDeferredOn == today && channel.UploadDeferredTo != null)
            return false;

        var day = NextPublicDay(publishAt, channel.Options.TimeZoneId, now);
        if (channel.UploadDeferredTo == null || channel.UploadDeferredTo < day)
            channel.UploadDeferredTo = day;
        channel.UploadDeferredOn = today;
        return true;
    }

    public static void Clear(ChannelProfile channel)
    {
        channel.UploadDeferredTo = null;
        channel.UploadDeferredOn = null;
    }
}
