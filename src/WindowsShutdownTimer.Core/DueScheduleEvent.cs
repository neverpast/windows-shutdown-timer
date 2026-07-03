namespace WindowsShutdownTimer.Core;

public sealed record DueScheduleEvent(
    ScheduleEventType Type,
    DateTime ScheduledAt,
    ReminderSettings? Reminder);
