using WindowsShutdownTimer.Core;

var tests = new (string Name, Action Test)[]
{
    ("normalizes 24:00 to 00:00", NormalizesMidnight),
    ("calculates next occurrences", CalculatesNextOccurrences),
    ("fires reminders once per day", FiresReminderOncePerDay),
    ("pause tonight skips only selected shutdown date", PauseTonightSkipsOnlySelectedDate),
    ("shutdown args default stays non-force", DefaultForceShutdownIsFalse)
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
