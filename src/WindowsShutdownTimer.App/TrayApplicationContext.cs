using WindowsShutdownTimer.Core;

namespace WindowsShutdownTimer.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan ScheduleCorrectionInterval = TimeSpan.FromMinutes(5);
    private const int MinimumScheduleIntervalMs = 250;

    private readonly SettingsStore _settingsStore;
    private readonly SettingsStore _defaultSettingsStore;
    private readonly ScheduleStateStore _scheduleStateStore;
    private readonly StartupService _startupService;
    private readonly SpeechReminderService _speechService;
    private readonly WindowsNotificationService _notificationService;
    private readonly ShutdownService _shutdownService;
    private readonly AutomaticShutdownMarkerStore _automaticShutdownMarkerStore;
    private readonly PowerHistoryService _powerHistoryService;
    private readonly ScheduleState _scheduleState;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly NotifyIcon _notifyIcon;

    private AppSettings _settings;
    private ToolStripMenuItem _pauseItem = null!;
    private ToolStripMenuItem _resumeItem = null!;
    private SettingsForm? _settingsForm;
    private CancellationTokenSource? _shutdownCountdownCancellation;
    private DateTime? _shutdownCountdownScheduledAt;
    private bool _shutdownCountdownRunning;

    public TrayApplicationContext(
        SettingsStore settingsStore,
        StartupService startupService,
        SpeechReminderService speechService,
        WindowsNotificationService notificationService,
        ShutdownService shutdownService,
        AutomaticShutdownMarkerStore automaticShutdownMarkerStore,
        PowerHistoryService powerHistoryService)
    {
        _settingsStore = settingsStore;
        _startupService = startupService;
        _speechService = speechService;
        _notificationService = notificationService;
        _shutdownService = shutdownService;
        _automaticShutdownMarkerStore = automaticShutdownMarkerStore;
        _powerHistoryService = powerHistoryService;
        _defaultSettingsStore = new SettingsStore(SettingsStore.GetDefaultSettingsFilePath());
        _scheduleStateStore = new ScheduleStateStore();
        _settings = _settingsStore.Load();
        _scheduleState = _scheduleStateStore.Load();

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

        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += (_, _) => CheckSchedule();

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
            defaults => _defaultSettingsStore.Save(defaults),
            _powerHistoryService);
        _settingsForm = form;
        form.FormClosed += (_, _) =>
        {
            if (form.DialogResult == DialogResult.OK)
            {
                _settings = form.Settings;
                _settingsStore.Save(_settings);
                ApplyStartup();
                UpdatePauseMenu();
                ScheduleNextCheck();
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
        var shutdownToPause = _shutdownCountdownScheduledAt ??
            ScheduleEngine.GetNextOccurrence(DateTime.Now, _settings.ShutdownTime);
        _scheduleState.PauseShutdownFor(DateOnly.FromDateTime(shutdownToPause));
        CancelShutdownCountdown();
        SaveScheduleState();
        UpdatePauseMenu();
        ScheduleNextCheck();
        ShowBalloon("定时关机", $"已暂停 {shutdownToPause:yyyy-MM-dd HH:mm} 的自动关机。");
    }

    private void ResumeTonight()
    {
        _scheduleState.ResumeShutdown();
        SaveScheduleState();
        UpdatePauseMenu();
        ScheduleNextCheck();
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
        _timer.Stop();

        IReadOnlyList<DueScheduleEvent> events;
        var previousPausedDate = _scheduleState.PausedShutdownDate;

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
            ScheduleNextCheck();
            return;
        }

        if (previousPausedDate != _scheduleState.PausedShutdownDate)
        {
            SaveScheduleState();
        }

        foreach (var dueEvent in events)
        {
            if (dueEvent.Type == ScheduleEventType.Reminder && dueEvent.Reminder is not null)
            {
                SendReminder(dueEvent.Reminder.Message, dueEvent.Reminder.Speak, dueEvent.Reminder.Toast);
            }
            else if (dueEvent.Type == ScheduleEventType.Shutdown)
            {
                StartShutdownCountdown(dueEvent.ScheduledAt);
            }
        }

        UpdatePauseMenu();

        if (!_shutdownCountdownRunning)
        {
            ScheduleNextCheck();
        }
    }

    private async void StartShutdownCountdown(DateTime scheduledAt)
    {
        if (_shutdownCountdownRunning)
        {
            return;
        }

        _shutdownCountdownRunning = true;
        var countdownCancellation = new CancellationTokenSource();
        _shutdownCountdownCancellation = countdownCancellation;
        _shutdownCountdownScheduledAt = scheduledAt;
        _timer.Stop();
        var shutdownStarted = false;

        try
        {
            var forceShutdown = _settings.ForceShutdown;
            _notificationService.Show("定时关机提醒", $"{scheduledAt:HH:mm} 将按计划自动关机。");
            ShowBalloon("定时关机提醒", $"{scheduledAt:HH:mm} 将按计划自动关机。");
            await _speechService.SpeakShutdownCountdownAsync(scheduledAt, countdownCancellation.Token);

            if (countdownCancellation.IsCancellationRequested)
            {
                return;
            }

            SaveAutomaticShutdownMarker(forceShutdown);
            _shutdownService.Shutdown(forceShutdown);
            shutdownStarted = true;
        }
        catch (Exception ex)
        {
            if (!countdownCancellation.IsCancellationRequested)
            {
                ShowBalloon("定时关机失败", ex.Message);
            }
        }
        finally
        {
            if (ReferenceEquals(_shutdownCountdownCancellation, countdownCancellation))
            {
                _shutdownCountdownCancellation = null;
                _shutdownCountdownScheduledAt = null;
            }

            countdownCancellation.Dispose();
            _shutdownCountdownRunning = false;

            if (!shutdownStarted)
            {
                UpdatePauseMenu();
                ScheduleNextCheck();
            }
        }
    }

    private void ScheduleNextCheck()
    {
        if (_shutdownCountdownRunning)
        {
            return;
        }

        try
        {
            var now = DateTime.Now;
            var nextCheck = ScheduleEngine.GetNextCheckTime(
                now,
                _settings,
                _scheduleState,
                ScheduleEngine.DefaultMaxLateness,
                ScheduleCorrectionInterval);
            var delay = nextCheck - now;

            _timer.Interval = Math.Max(MinimumScheduleIntervalMs, (int)Math.Ceiling(delay.TotalMilliseconds));
            _timer.Start();
        }
        catch
        {
            _timer.Interval = (int)ScheduleCorrectionInterval.TotalMilliseconds;
            _timer.Start();
        }
    }

    private void CancelShutdownCountdown()
    {
        if (_shutdownCountdownRunning)
        {
            _shutdownCountdownCancellation?.Cancel();
        }
    }

    private void SaveAutomaticShutdownMarker(bool forceShutdown)
    {
        try
        {
            _automaticShutdownMarkerStore.Add(new AutomaticShutdownMarker(
                DateTime.Now,
                _settings.ShutdownTime,
                forceShutdown));
        }
        catch
        {
            // Shutdown should continue even when local history metadata cannot be written.
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

    private void SaveScheduleState()
    {
        try
        {
            _scheduleStateStore.Save(_scheduleState);
        }
        catch
        {
            ShowBalloon("定时关机", "保存暂停状态失败，请检查配置目录权限。");
        }
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
