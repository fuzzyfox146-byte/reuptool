using System.Globalization;

namespace YtFolderUploader;

public static class SchedulePlanner
{
    public static readonly TimeSpan MinimumLead = TimeSpan.FromMinutes(20);

    public static SchedulePlan Plan(
        int count,
        UploadOptions options,
        IEnumerable<DateTimeOffset> used,
        DateTimeOffset now)
    {
        var plan = new SchedulePlan();
        if (count <= 0)
            return plan;

        var zone = TimeZones.Resolve(options.TimeZoneId);
        var slots = ResolveSlots(options.TimeSlotsText, options.VideosPerDay, out var warning);
        plan.Warning = warning;

        var usedKeys = new HashSet<long>(used.Select(Key));
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var date = options.ScheduleStartDate < today ? today : options.ScheduleStartDate;

        var guard = 0;
        while (plan.Times.Count < count && guard++ < 4000)
        {
            var placedOnThisDay = 0;
            foreach (var slot in slots)
            {
                if (plan.Times.Count >= count || placedOnThisDay >= Math.Max(1, options.VideosPerDay))
                    break;

                var local = date.ToDateTime(slot);
                var when = new DateTimeOffset(local, zone.GetUtcOffset(local));
                if (when < now + MinimumLead)
                    continue;
                if (!usedKeys.Add(Key(when)))
                    continue;

                plan.Times.Add(when);
                placedOnThisDay++;
            }

            date = date.AddDays(1);
        }

        if (plan.Times.Count < count)
            plan.Warning = "Không xếp đủ giờ hẹn trong 4000 ngày. Hãy kiểm tra danh sách giờ.";

        return plan;
    }

    public static List<TimeOnly> ResolveSlots(string? text, int videosPerDay, out string? warning)
    {
        warning = null;
        if (videosPerDay < 1)
            videosPerDay = 1;

        var slots = Parse(text);
        if (slots.Count == 0)
            slots = [new TimeOnly(9, 0), new TimeOnly(15, 0), new TimeOnly(21, 0)];

        slots = slots.Distinct().OrderBy(slot => slot).ToList();
        if (slots.Count > videosPerDay)
            return slots.Take(videosPerDay).ToList();
        if (slots.Count == videosPerDay)
            return slots;

        var gap = slots.Count >= 2 ? slots[^1] - slots[^2] : TimeSpan.FromHours(3);
        if (gap <= TimeSpan.Zero)
            gap = TimeSpan.FromHours(1);

        while (slots.Count < videosPerDay)
        {
            var next = slots[^1].Add(gap);
            if (next <= slots[^1])
                break;
            slots.Add(next);
        }

        if (slots.Count < videosPerDay)
            warning = $"Trong một ngày chỉ xếp được {slots.Count} khung giờ, ít hơn mức {videosPerDay} video/ngày. Phần còn lại chuyển sang ngày sau.";

        return slots;
    }

    static List<TimeOnly> Parse(string? text)
    {
        var list = new List<TimeOnly>();
        if (string.IsNullOrWhiteSpace(text))
            return list;

        foreach (var part in text.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part.Trim();
            if (TimeOnly.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                || TimeOnly.TryParse(token, new CultureInfo("vi-VN"), DateTimeStyles.None, out time))
            {
                list.Add(new TimeOnly(time.Hour, time.Minute));
            }
        }

        return list;
    }

    static long Key(DateTimeOffset when) => when.ToUnixTimeSeconds() / 60;
}

public static class TimeZones
{
    public static readonly (string Id, string Label)[] Choices =
    [
        ("SE Asia Standard Time", "Việt Nam / Bangkok (UTC+7)"),
        ("Tokyo Standard Time", "Nhật Bản (UTC+9)"),
        ("Korea Standard Time", "Hàn Quốc (UTC+9)"),
        ("Singapore Standard Time", "Singapore (UTC+8)"),
        ("China Standard Time", "Trung Quốc (UTC+8)"),
        ("UTC", "UTC"),
        ("Eastern Standard Time", "Mỹ miền Đông"),
        ("Pacific Standard Time", "Mỹ miền Tây")
    ];

    public static TimeZoneInfo Resolve(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }
}
