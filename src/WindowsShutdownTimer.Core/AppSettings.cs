namespace WindowsShutdownTimer.Core;

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public string ShutdownTime { get; set; } = "00:00";
    public bool AutoShutdown { get; set; } = true;
    public bool ForceShutdown { get; set; } = false;
    public bool StartWithWindows { get; set; } = true;
    public List<ReminderSettings> Reminders { get; set; } = CreateDefaultReminders();

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Enabled = Enabled,
            ShutdownTime = ShutdownTime,
            AutoShutdown = AutoShutdown,
            ForceShutdown = ForceShutdown,
            StartWithWindows = StartWithWindows,
            Reminders = (Reminders ?? new List<ReminderSettings>())
                .Select(r => new ReminderSettings
                {
                    Id = r.Id,
                    Time = r.Time,
                    Message = r.Message,
                    Speak = r.Speak,
                    Toast = r.Toast,
                    Enabled = r.Enabled
                })
                .ToList()
        };
    }

    public void Normalize()
    {
        ShutdownTime = TimeOfDayParser.Normalize(ShutdownTime);

        if (Reminders is null || Reminders.Count == 0)
        {
            Reminders = CreateDefaultReminders();
        }

        foreach (var reminder in Reminders)
        {
            reminder.Normalize();
        }
    }

    private static List<ReminderSettings> CreateDefaultReminders() =>
    [
        new ReminderSettings
        {
            Id = "reminder-2345",
            Time = "23:45",
            Message = "还有15分钟自动关机",
            Speak = true,
            Toast = true,
            Enabled = true
        },
        new ReminderSettings
        {
            Id = "reminder-2355",
            Time = "23:55",
            Message = "还有5分钟自动关机",
            Speak = true,
            Toast = true,
            Enabled = true
        }
    ];
}
