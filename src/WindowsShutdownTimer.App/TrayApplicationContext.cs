using WindowsShutdownTimer.Core;

namespace WindowsShutdownTimer.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore;
    private readonly SettingsStore _defaultSettingsStore;
    private readonly StartupService _startupService;
    private readonly SpeechReminderService _speechService;
    private readonly WindowsNotificationService _notificationService;
    private readonly ShutdownService _shutdownService;
    private readonly ScheduleState _scheduleState = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly NotifyIcon _notifyIcon;

    private AppSettings _settings;
    private ToolStripMenuItem _pauseItem = null!;
    private ToolStripMenuItem _resumeItem = null!;
    private SettingsForm? _settingsForm;
    private bool _shutdownCountdownRunning;

    public TrayApplicationContext(
        SettingsStore settingsStore,
        StartupService startupService,
        SpeechReminderService speechService,
        WindowsNotificationService notificationService,
        ShutdownService shutdownService)
    {
        _settingsStore = settingsStore;
        _startupService = startupService;
        _speechService = speechService;
        _notificationService = notificationService;
        _shutdownService = shutdownService;
        _defaultSettingsStore = new SettingsStore(SettingsStore.GetDefaultSettingsFilePath());
        _settings = _settingsStore.Load();

        _notifyIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "Windows 定时关机",
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
        _notifyIcon.ContextMenuStrip = BuildMenu();

        _notificationService.Register();
        ApplyStartup();

        _timer = new System.Windows.Forms.Timer { Interval = 15_000 };
        _timer.Tick += (_, _) => CheckSchedule();
        _timer.Start();

        CheckSchedule();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("设置", null, (_, _) => OpenSettings());
        _pauseItem = new ToolStripMenuItem("暂停今晚关机", null, (_, _) => PauseTonight());
        _resumeItem = new ToolStripMenuItem("恢复今晚关机", null, (_, _) => ResumeTonight());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_resumeItem);
        menu.Items.Add("测试提醒", null, (_, _) => SendReminder("测试提醒：还有15分钟自动关机", speak: true, toast: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Exit());
        UpdatePauseMenu();
        return menu;
    }

    private void OpenSettings()
    {
        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.RestoreFromTray();
            return;
        }

        var form = new SettingsForm(
            _settings,
            _defaultSettingsStore.Load(),
            defaults => _defaultSettingsStore.Save(defaults));
        _settingsForm = form;
        form.FormClosed += (_, _) =>
        {
            if (form.DialogResult == DialogResult.OK)
            {
                _settings = form.Settings;
                _settingsStore.Save(_settings);
                ApplyStartup();
                UpdatePauseMenu();
            }

            if (ReferenceEquals(_settingsForm, form))
            {
                _settingsForm = null;
            }

            form.Dispose();
        };
        form.Show();
        form.Activate();
    }

    private void PauseTonight()
    {
        var nextShutdown = ScheduleEngine.GetNextOccurrence(DateTime.Now, _settings.ShutdownTime);
        _scheduleState.PauseShutdownFor(DateOnly.FromDateTime(nextShutdown));
        UpdatePauseMenu();
        ShowBalloon("定时关机", $"已暂停 {nextShutdown:yyyy-MM-dd HH:mm} 的自动关机。");
    }

    private void ResumeTonight()
    {
        _scheduleState.ResumeShutdown();
        UpdatePauseMenu();
        ShowBalloon("定时关机", "已恢复自动关机。");
    }

    private void UpdatePauseMenu()
    {
        var nextShutdown = ScheduleEngine.GetNextOccurrence(DateTime.Now, _settings.ShutdownTime);
        var paused = _scheduleState.IsShutdownPaused(DateOnly.FromDateTime(nextShutdown));
        _pauseItem.Enabled = !paused;
        _resumeItem.Enabled = paused;
    }

    private void ApplyStartup()
    {
        try
        {
            _startupService.Apply(_settings.StartWithWindows);
        }
        catch
        {
            ShowBalloon("定时关机", "设置开机自启动失败，请检查权限。");
        }
    }

    private void CheckSchedule()
    {
        IReadOnlyList<DueScheduleEvent> events;
        try
        {
            events = ScheduleEngine.GetDueEvents(
                DateTime.Now,
                _settings,
                _scheduleState,
                ScheduleEngine.DefaultMaxLateness);
        }
        catch (Exception ex)
        {
            ShowBalloon("定时关机配置错误", ex.Message);
            return;
        }

        foreach (var dueEvent in events)
        {
            if (dueEvent.Type == ScheduleEventType.Reminder && dueEvent.Reminder is not null)
            {
                SendReminder(dueEvent.Reminder.Message, dueEvent.Reminder.Speak, dueEvent.Reminder.Toast);
            }
            else if (dueEvent.Type == ScheduleEventType.Shutdown)
            {
                StartShutdownCountdown();
            }
        }

        UpdatePauseMenu();
    }

    private async void StartShutdownCountdown()
    {
        if (_shutdownCountdownRunning)
        {
            return;
        }

        _shutdownCountdownRunning = true;
        _timer.Stop();

        try
        {
            var forceShutdown = _settings.ForceShutdown;
            _notificationService.Show("定时关机提醒", "到点了，10秒后自动关机。");
            ShowBalloon("定时关机提醒", "到点了，10秒后自动关机。");
            await _speechService.SpeakShutdownCountdownAsync();
            _shutdownService.Shutdown(forceShutdown);
        }
        catch (Exception ex)
        {
            ShowBalloon("定时关机失败", ex.Message);
            _shutdownCountdownRunning = false;
            _timer.Start();
        }
    }

    private void SendReminder(string message, bool speak, bool toast)
    {
        if (speak)
        {
            _speechService.Speak(message);
        }

        if (toast && !_notificationService.Show("定时关机提醒", message))
        {
            ShowBalloon("定时关机提醒", message);
        }
    }

    private void ShowBalloon(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
    }

    private void Exit()
    {
        _timer.Stop();
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _settingsForm?.Dispose();
            _notifyIcon.Dispose();
            _notificationService.Dispose();
            _speechService.Dispose();
        }

        base.Dispose(disposing);
    }
}
