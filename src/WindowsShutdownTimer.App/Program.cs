using WindowsShutdownTimer.Core;

namespace WindowsShutdownTimer.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext(
            new SettingsStore(),
            new StartupService(),
            new SpeechReminderService(),
            new WindowsNotificationService(),
            new ShutdownService());

        Application.Run(context);
    }
}
