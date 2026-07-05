using WindowsShutdownTimer.Core;

namespace WindowsShutdownTimer.App;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\WindowsShutdownTimer-C8D0F6E9-37B1-4101-9A5C-E9E47C4C4522";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Windows 定时关机已经在运行，请从系统托盘打开设置。",
                "Windows 定时关机",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

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
