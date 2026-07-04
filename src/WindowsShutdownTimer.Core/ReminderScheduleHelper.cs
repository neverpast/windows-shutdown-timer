namespace WindowsShutdownTimer.Core;

public static class ReminderScheduleHelper
{
    private const int MinutesPerDay = 24 * 60;

    public static void ShiftRemindersForShutdownChange(
        string oldShutdownTime,
        string newShutdownTime,
        IEnumerable<ReminderSettings> reminders)
    {
        var oldShutdown = TimeOfDayParser.Parse(oldShutdownTime);
        var newShutdown = TimeOfDayParser.Parse(newShutdownTime);

        foreach (var reminder in reminders)
        {
            var offsetMinutes = reminder.LeadMinutes
                ?? MinutesUntil(TimeOfDayParser.Parse(reminder.Time), oldShutdown);

            reminder.LeadMinutes = offsetMinutes;
            reminder.Time = GetReminderTime(newShutdown, offsetMinutes);
            reminder.Message = BuildReminderMessage(offsetMinutes);
        }
    }

    public static string GetReminderTime(string shutdownTime, int leadMinutes)
    {
        return GetReminderTime(TimeOfDayParser.Parse(shutdownTime), leadMinutes);
    }

    public static string BuildReminderMessage(int leadMinutes)
    {
        return $"还有{FormatOffset(leadMinutes)}自动关机";
    }

    public static int MinutesUntil(TimeSpan from, TimeSpan to)
    {
        return NormalizeMinutes((int)to.TotalMinutes - (int)from.TotalMinutes);
    }

    public static bool IsValidLeadMinutes(int leadMinutes)
    {
        return leadMinutes > 0 && leadMinutes < MinutesPerDay;
    }

    private static string GetReminderTime(TimeSpan shutdownTime, int leadMinutes)
    {
        if (!IsValidLeadMinutes(leadMinutes))
        {
            throw new ArgumentOutOfRangeException(nameof(leadMinutes), "提前提醒时间必须在 1 到 1439 分钟之间。");
        }

        var reminderMinutes = NormalizeMinutes((int)shutdownTime.TotalMinutes - leadMinutes);
        return FormatMinutes(reminderMinutes);
    }

    private static int NormalizeMinutes(int minutes)
    {
        var normalized = minutes % MinutesPerDay;
        return normalized < 0 ? normalized + MinutesPerDay : normalized;
    }

    private static string FormatMinutes(int minutes)
    {
        var hour = minutes / 60;
        var minute = minutes % 60;
        return $"{hour:00}:{minute:00}";
    }

    public static string FormatOffset(int minutes)
    {
        if (minutes <= 0)
        {
            return "0分钟";
        }

        var hours = minutes / 60;
        var remainingMinutes = minutes % 60;

        if (hours == 0)
        {
            return $"{remainingMinutes}分钟";
        }

        return remainingMinutes == 0
            ? $"{hours}小时"
            : $"{hours}小时{remainingMinutes}分钟";
    }
}
