using System.Globalization;

namespace YtFolderUploader;

public static class SchedulePlanner
{
    public static readonly TimeSpan MinimumLead = TimeSpan.FromMinutes(20);

    public static SchedulePlan Plan(
        int count,
        UploadOptions options,
        IEnumerable<DateTimeOffset> used,
        DateTimeOffset now,
        DateOnly? notBefore = null)
    {
        var plan = new SchedulePlan();
        var zone = TimeZones.Resolve(options.TimeZoneId);
        if (options.ExactDailyTimes)
            return PlanOneDayAhead(count, options, used, now, zone, notBefore);

        if (count <= 0)
            return plan;

        var slots = ResolveSlots(options.TimeSlotsText, options.VideosPerDay, out var warning);
        plan.Warning = warning;

        var usedKeys = new HashSet<long>(used.Select(Key));
        var date = TargetDay(options, now, notBefore);

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

    public static DateOnly TargetDay(UploadOptions options, DateTimeOffset now, DateOnly? notBefore = null)
    {
        var zone = TimeZones.Resolve(options.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var tomorrow = DateOnly.FromDateTime(localNow.DateTime).AddDays(1);
        var day = options.ScheduleStartDate > tomorrow ? options.ScheduleStartDate : tomorrow;
        if (notBefore != null && notBefore > day)
            day = notBefore.Value;
        return day;
    }

    public static SchedulePlan PlanOneDayAhead(
        int count,
        UploadOptions options,
        IEnumerable<DateTimeOffset> used,
        DateTimeOffset now,
        TimeZoneInfo? zone = null,
        DateOnly? notBefore = null)
    {
        var plan = new SchedulePlan();
        zone ??= TimeZones.Resolve(options.TimeZoneId);
        var slots = Parse(options.TimeSlotsText).Distinct().OrderBy(slot => slot).ToList();
        var day = TargetDay(options, now, notBefore);
        plan.TargetDay = day;
        if (slots.Count == 0)
        {
            plan.ShortSource = count > 0;
            plan.Warning = "Chưa có giờ đăng. Không tự thêm giờ.";
            return plan;
        }

        var daySize = Math.Clamp(options.VideosPerDay, 1, slots.Count);
        var daySlots = slots.Take(daySize).ToList();
        if (options.VideosPerDay > slots.Count)
            plan.Warning = $"Đã nhập {slots.Count} giờ, nên một ngày đăng {slots.Count} video.";

        if (count < daySlots.Count)
        {
            plan.ShortSource = true;
            plan.Warning = $"Ngày {day:dd/MM} cần {daySlots.Count} video, chỉ còn {count}. Không đăng ngày thiếu.";
            return plan;
        }

        var usedKeys = new HashSet<long>(used.Select(Key));
        var times = new List<DateTimeOffset>();
        foreach (var slot in daySlots)
        {
            var local = day.ToDateTime(slot);
            var when = new DateTimeOffset(local, zone.GetUtcOffset(local));
            if (usedKeys.Contains(Key(when)))
            {
                plan.DayAlreadySet = true;
                plan.Times.Clear();
                plan.Warning = $"Ngày {day:dd/MM} đã có lịch. Không đăng thêm.";
                return plan;
            }

            if (when < now + MinimumLead)
            {
                plan.Warning = $"Giờ {slot:HH:mm} ngày {day:dd/MM} đã quá gần. Không đăng ngày này.";
                return plan;
            }

            times.Add(when);
        }

        plan.Times.AddRange(times);
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
