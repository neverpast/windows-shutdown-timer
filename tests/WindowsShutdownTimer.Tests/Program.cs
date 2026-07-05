using WindowsShutdownTimer.Core;

var tests = new (string Name, Action Test)[]
{
    ("normalizes 24:00 to 00:00", NormalizesMidnight),
    ("calculates next occurrences", CalculatesNextOccurrences),
    ("fires reminders once per day", FiresReminderOncePerDay),
    ("pause tonight skips only selected shutdown date", PauseTonightSkipsOnlySelectedDate),
    ("shutdown args default stays non-force", DefaultForceShutdownIsFalse),
    ("default settings restore midnight shutdown", DefaultSettingsRestoreMidnightShutdown),
    ("settings clone is independent", SettingsCloneIsIndependent),
    ("defaults store path uses defaults json", DefaultsStorePathUsesDefaultsJson),
    ("corrupt settings file backs up and restores defaults", CorruptSettingsFileBacksUpAndRestoresDefaults),
    ("paused shutdown date persists", PausedShutdownDatePersists),
    ("reminders follow lead minutes", RemindersFollowLeadMinutes),
    ("legacy reminder times infer lead minutes", LegacyReminderTimesInferLeadMinutes)
};

var failed = 0;

foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(ex);
    }
}

if (failed > 0)
{
    Environment.ExitCode = 1;
}

static void NormalizesMidnight()
{
    Equal("00:00", TimeOfDayParser.Normalize("24:00"));
    Equal("23:45", TimeOfDayParser.Normalize("23:45"));
    Throws<FormatException>(() => TimeOfDayParser.Normalize("24:01"));
}

static void CalculatesNextOccurrences()
{
    var now = new DateTime(2026, 7, 3, 23, 40, 0);

    Equal(new DateTime(2026, 7, 3, 23, 45, 0), ScheduleEngine.GetNextOccurrence(now, "23:45"));
    Equal(new DateTime(2026, 7, 3, 23, 55, 0), ScheduleEngine.GetNextOccurrence(now, "23:55"));
    Equal(new DateTime(2026, 7, 4, 0, 0, 0), ScheduleEngine.GetNextOccurrence(now, "00:00"));
}

static void FiresReminderOncePerDay()
{
    var settings = AppSettings.CreateDefault();
    var state = new ScheduleState();
    var now = new DateTime(2026, 7, 3, 23, 45, 5);

    var first = ScheduleEngine.GetDueEvents(now, settings, state, TimeSpan.FromMinutes(2));
    Equal(1, first.Count(e => e.Type == ScheduleEventType.Reminder));

    var second = ScheduleEngine.GetDueEvents(now.AddSeconds(15), settings, state, TimeSpan.FromMinutes(2));
    Equal(0, second.Count(e => e.Type == ScheduleEventType.Reminder));
}

static void PauseTonightSkipsOnlySelectedDate()
{
    var settings = AppSettings.CreateDefault();
    var state = new ScheduleState();

    state.PauseShutdownFor(new DateOnly(2026, 7, 4));

    var paused = ScheduleEngine.GetDueEvents(
        new DateTime(2026, 7, 4, 0, 0, 5),
        settings,
        state,
        TimeSpan.FromMinutes(2));

    Equal(0, paused.Count(e => e.Type == ScheduleEventType.Shutdown));

    var nextDay = ScheduleEngine.GetDueEvents(
        new DateTime(2026, 7, 5, 0, 0, 5),
        settings,
        state,
        TimeSpan.FromMinutes(2));

    Equal(1, nextDay.Count(e => e.Type == ScheduleEventType.Shutdown));
}

static void DefaultForceShutdownIsFalse()
{
    False(AppSettings.CreateDefault().ForceShutdown);
    Equal("/s /t 0", ShutdownCommandBuilder.BuildArguments(false));
    Equal("/s /t 0 /f", ShutdownCommandBuilder.BuildArguments(true));
}

static void DefaultSettingsRestoreMidnightShutdown()
{
    var defaults = AppSettings.CreateDefault();
    defaults.Normalize();

    Equal("00:00", defaults.ShutdownTime);
    Equal(3, defaults.Reminders.Count);
    Equal(15, defaults.Reminders[0].LeadMinutes.GetValueOrDefault());
    Equal("23:45", defaults.Reminders[0].Time);
    Equal(5, defaults.Reminders[1].LeadMinutes.GetValueOrDefault());
    Equal("23:55", defaults.Reminders[1].Time);
    Equal(1, defaults.Reminders[2].LeadMinutes.GetValueOrDefault());
    Equal("23:59", defaults.Reminders[2].Time);
}

static void SettingsCloneIsIndependent()
{
    var settings = AppSettings.CreateDefault();
    var clone = settings.Clone();

    clone.ShutdownTime = "21:30";
    clone.Reminders[0].Message = "changed";

    Equal("00:00", settings.ShutdownTime);
    Equal("还有15分钟自动关机", settings.Reminders[0].Message);
}

static void DefaultsStorePathUsesDefaultsJson()
{
    True(SettingsStore.GetDefaultSettingsFilePath().EndsWith("defaults.json", StringComparison.OrdinalIgnoreCase));
}

static void CorruptSettingsFileBacksUpAndRestoresDefaults()
{
    var directory = CreateTempDirectory();
    try
    {
        var filePath = Path.Combine(directory, "settings.json");
        File.WriteAllText(filePath, "{ broken json");

        var store = new SettingsStore(filePath);
        var settings = store.Load();

        Equal("00:00", settings.ShutdownTime);
        True(File.Exists(Path.Combine(directory, "settings.bad.json")));
        True(File.ReadAllText(filePath).Contains("\"shutdownTime\": \"00:00\"", StringComparison.OrdinalIgnoreCase));
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void PausedShutdownDatePersists()
{
    var directory = CreateTempDirectory();
    try
    {
        var filePath = Path.Combine(directory, "state.json");
        var store = new ScheduleStateStore(filePath);
        var state = new ScheduleState();

        state.PauseShutdownFor(new DateOnly(2026, 7, 5));
        store.Save(state);

        var loaded = store.Load();
        True(loaded.IsShutdownPaused(new DateOnly(2026, 7, 5)));

        loaded.ResumeShutdown();
        store.Save(loaded);

        var resumed = store.Load();
        False(resumed.IsShutdownPaused(new DateOnly(2026, 7, 5)));
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void RemindersFollowLeadMinutes()
{
    var reminders = new List<ReminderSettings>
    {
        new() { LeadMinutes = 5 },
        new() { LeadMinutes = 2 }
    };

    ReminderScheduleHelper.ShiftRemindersForShutdownChange("01:00", "02:30", reminders);

    Equal(5, reminders[0].LeadMinutes.GetValueOrDefault());
    Equal("02:25", reminders[0].Time);
    Equal("还有5分钟自动关机", reminders[0].Message);
    Equal(2, reminders[1].LeadMinutes.GetValueOrDefault());
    Equal("02:28", reminders[1].Time);
    Equal("还有2分钟自动关机", reminders[1].Message);
    Equal("00:55", ReminderScheduleHelper.GetReminderTime("01:00", 5));
}

static void LegacyReminderTimesInferLeadMinutes()
{
    var settings = new AppSettings
    {
        ShutdownTime = "00:00",
        Reminders =
        [
            new ReminderSettings { Time = "23:45", Message = "旧文案" },
            new ReminderSettings { Time = "23:55", Message = "旧文案" }
        ]
    };

    settings.Normalize();

    Equal(15, settings.Reminders[0].LeadMinutes.GetValueOrDefault());
    Equal("23:45", settings.Reminders[0].Time);
    Equal("还有15分钟自动关机", settings.Reminders[0].Message);
    Equal(5, settings.Reminders[1].LeadMinutes.GetValueOrDefault());
    Equal("23:55", settings.Reminders[1].Time);
    Equal("还有5分钟自动关机", settings.Reminders[1].Message);
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void False(bool actual)
{
    if (actual)
    {
        throw new InvalidOperationException("Expected false.");
    }
}

static void True(bool actual)
{
    if (!actual)
    {
        throw new InvalidOperationException("Expected true.");
    }
}

static void Throws<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static string CreateTempDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), $"WindowsShutdownTimerTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
}
