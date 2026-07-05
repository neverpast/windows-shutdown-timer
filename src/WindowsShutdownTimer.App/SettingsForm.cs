using WindowsShutdownTimer.Core;

namespace WindowsShutdownTimer.App;

public sealed class SettingsForm : Form
{
    private static readonly Color ShellBack = Color.FromArgb(241, 245, 249);
    private static readonly Color SurfaceBack = Color.FromArgb(255, 255, 255);
    private static readonly Color Primary = Color.FromArgb(37, 99, 235);
    private static readonly Color PrimaryDark = Color.FromArgb(30, 64, 175);
    private static readonly Color Accent = Color.FromArgb(16, 185, 129);
    private static readonly Color Danger = Color.FromArgb(220, 38, 38);
    private static readonly Color Border = Color.FromArgb(203, 213, 225);
    private static readonly Color TextMain = Color.FromArgb(15, 23, 42);
    private static readonly Color TextMuted = Color.FromArgb(71, 85, 105);

    private readonly CheckBox _enabledCheckBox = new() { Text = "启用提醒与关机", AutoSize = true };
    private readonly CheckBox _autoShutdownCheckBox = new() { Text = "到点自动关机", AutoSize = true };
    private readonly CheckBox _forceShutdownCheckBox = new() { Text = "强制关闭应用（可能丢失未保存内容）", AutoSize = true };
    private readonly CheckBox _startWithWindowsCheckBox = new() { Text = "登录 Windows 后自动启动", AutoSize = true };
    private readonly DateTimePicker _shutdownTimePicker = new();
    private readonly DataGridView _remindersGrid = new();
    private readonly Label _shutdownTimeBadge = new();
    private readonly Label _timeUntilShutdownLabel = new();
    private readonly Label _statusLabel = new();
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 1_000 };
    private readonly Action<AppSettings> _saveDefaultSettings;
    private AppSettings _defaultSettings;
    private string _lastValidShutdownTime = "00:00";
    private bool _loadingSettings;
    private bool _syncingShutdownTime;

    public SettingsForm(AppSettings settings, AppSettings defaultSettings, Action<AppSettings> saveDefaultSettings)
    {
        Settings = Clone(settings);
        _defaultSettings = Clone(defaultSettings);
        _saveDefaultSettings = saveDefaultSettings;

        Text = "Windows 定时关机设置";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = true;
        MaximizeBox = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(980, 660);
        MinimumSize = new Size(760, 500);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = ShellBack;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;

        BuildLayout();
        LoadSettings();

        _previewTimer.Tick += (_, _) => UpdateShutdownPreview();
        _previewTimer.Start();
    }

    public AppSettings Settings { get; private set; }

    public void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Minimized)
        {
            ShowInTaskbar = false;
            Hide();
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18, 18, 18, 12),
            BackColor = ShellBack,
            AutoScroll = true
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        root.Controls.Add(CreateHeaderPanel());

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true,
            BackColor = SurfaceBack,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 12, 0, 0)
        };
        StyleCheckBox(_enabledCheckBox);
        StyleCheckBox(_autoShutdownCheckBox);
        StyleCheckBox(_forceShutdownCheckBox);
        StyleCheckBox(_startWithWindowsCheckBox);
        _autoShutdownCheckBox.CheckedChanged += (_, _) => UpdateShutdownPreview();
        options.Controls.Add(_enabledCheckBox);
        options.Controls.Add(_autoShutdownCheckBox);
        options.Controls.Add(_forceShutdownCheckBox);
        options.Controls.Add(_startWithWindowsCheckBox);
        root.Controls.Add(options);

        var shutdownPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            BackColor = SurfaceBack,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 10, 0, 10)
        };
        _shutdownTimePicker.Format = DateTimePickerFormat.Custom;
        _shutdownTimePicker.CustomFormat = "HH:mm";
        _shutdownTimePicker.ShowUpDown = true;
        _shutdownTimePicker.Width = 120;
        _shutdownTimePicker.Font = new Font(Font.FontFamily, 10F, FontStyle.Regular, GraphicsUnit.Point);
        _shutdownTimePicker.ValueChanged += (_, _) => ApplyShutdownPickerChange();
        shutdownPanel.Controls.Add(CreateSectionLabel("每日关机时间"));
        shutdownPanel.Controls.Add(_shutdownTimePicker);
        shutdownPanel.Controls.Add(new Label { Text = "使用上下按钮选择时间", AutoSize = true, ForeColor = TextMuted, Padding = new Padding(8, 5, 0, 0) });
        root.Controls.Add(shutdownPanel);

        ConfigureGrid();
        root.Controls.Add(_remindersGrid);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = ShellBack
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var editButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true,
            BackColor = ShellBack
        };

        var commitButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = true,
            BackColor = ShellBack
        };

        var saveButton = CreateBottomButton("保存", 112, ButtonTone.Primary);
        saveButton.Click += (_, _) =>
        {
            if (TrySave())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        var restoreDefaultsButton = CreateBottomButton("恢复默认", 160, ButtonTone.Secondary);
        restoreDefaultsButton.Click += (_, _) =>
        {
            Settings = Clone(_defaultSettings);
            LoadSettings();
            SetStatus("已恢复默认设置");
        };

        var saveDefaultsButton = CreateBottomButton("设为默认", 160, ButtonTone.Secondary);
        saveDefaultsButton.Click += (_, _) =>
        {
            if (!TryReadCurrentSettings(out var next))
            {
                return;
            }

            _defaultSettings = Clone(next);
            _saveDefaultSettings(Clone(next));
            SetStatus("已保存为默认设置");
        };

        var addButton = CreateBottomButton("添加", 96, ButtonTone.Accent);
        addButton.Click += (_, _) =>
        {
            AddReminderRow(Guid.NewGuid().ToString("N"), true, 5, _shutdownTimePicker.Value.ToString("HH:mm"), true, true);
            SetStatus("已添加提前 5 分钟提醒");
        };

        var deleteButton = CreateBottomButton("删除", 96, ButtonTone.Danger);
        deleteButton.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _remindersGrid.SelectedRows)
            {
                if (!row.IsNewRow)
                {
                    _remindersGrid.Rows.Remove(row);
                }
            }
        };

        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = TextMuted;
        _statusLabel.Padding = new Padding(8, 9, 0, 0);
        _statusLabel.Margin = new Padding(4, 0, 0, 0);

        editButtons.Controls.Add(restoreDefaultsButton);
        editButtons.Controls.Add(saveDefaultsButton);
        editButtons.Controls.Add(addButton);
        editButtons.Controls.Add(deleteButton);
        editButtons.Controls.Add(_statusLabel);
        commitButtons.Controls.Add(saveButton);
        bottom.Controls.Add(editButtons, 0, 0);
        bottom.Controls.Add(commitButtons, 1, 0);
        root.Controls.Add(bottom);

        AcceptButton = saveButton;
    }

    private Control CreateHeaderPanel()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 116),
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Primary,
            Padding = new Padding(24, 14, 24, 14),
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var copy = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Primary,
            Margin = new Padding(0)
        };
        copy.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        copy.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Windows 定时关机",
            Dock = DockStyle.Top,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 17F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };

        var subtitle = new Label
        {
            Text = "每日提醒、语音倒计时、到点自动关机",
            Dock = DockStyle.Top,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(219, 234, 254),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(1, 6, 0, 0)
        };

        var shutdownPreview = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Primary,
            Margin = new Padding(18, 2, 0, 0),
            Anchor = AnchorStyles.Right
        };
        shutdownPreview.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shutdownPreview.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _shutdownTimeBadge.Text = Settings.ShutdownTime;
        _shutdownTimeBadge.AutoSize = false;
        _shutdownTimeBadge.TextAlign = ContentAlignment.MiddleCenter;
        _shutdownTimeBadge.Font = new Font(Font.FontFamily, 14F, FontStyle.Bold, GraphicsUnit.Point);
        _shutdownTimeBadge.ForeColor = Color.White;
        _shutdownTimeBadge.BackColor = PrimaryDark;
        _shutdownTimeBadge.Size = new Size(124, 42);
        _shutdownTimeBadge.MinimumSize = new Size(124, 42);
        _shutdownTimeBadge.Margin = new Padding(0, 0, 0, 5);

        _timeUntilShutdownLabel.AutoSize = false;
        _timeUntilShutdownLabel.TextAlign = ContentAlignment.MiddleCenter;
        _timeUntilShutdownLabel.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        _timeUntilShutdownLabel.ForeColor = Color.FromArgb(219, 234, 254);
        _timeUntilShutdownLabel.BackColor = Primary;
        _timeUntilShutdownLabel.Size = new Size(190, 28);
        _timeUntilShutdownLabel.MinimumSize = new Size(190, 28);
        _timeUntilShutdownLabel.Margin = new Padding(0);

        shutdownPreview.Controls.Add(_shutdownTimeBadge, 0, 0);
        shutdownPreview.Controls.Add(_timeUntilShutdownLabel, 0, 1);

        copy.Controls.Add(title, 0, 0);
        copy.Controls.Add(subtitle, 0, 1);
        header.Controls.Add(copy, 0, 0);
        header.Controls.Add(shutdownPreview, 1, 0);
        return header;
    }

    private static Label CreateSectionLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            ForeColor = TextMain,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 8, 0),
            Margin = new Padding(0)
        };

    private static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.ForeColor = TextMain;
        checkBox.Margin = new Padding(0, 0, 18, 0);
        checkBox.Padding = new Padding(0, 2, 0, 2);
        checkBox.UseVisualStyleBackColor = false;
        checkBox.BackColor = SurfaceBack;
    }

    private Button CreateBottomButton(string text, int width, ButtonTone tone)
    {
        var (backColor, foreColor) = tone switch
        {
            ButtonTone.Primary => (Primary, Color.White),
            ButtonTone.Accent => (Accent, Color.White),
            ButtonTone.Danger => (Danger, Color.White),
            _ => (Color.White, TextMain)
        };

        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 40,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(8, 0, 8, 0),
            AutoEllipsis = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = foreColor,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = tone == ButtonTone.Secondary ? Border : backColor;
        button.FlatAppearance.MouseOverBackColor = tone == ButtonTone.Secondary ? Color.FromArgb(248, 250, 252) : ControlPaint.Light(backColor);
        return button;
    }

    private enum ButtonTone
    {
        Primary,
        Secondary,
        Accent,
        Danger
    }

    private void ConfigureGrid()
    {
        _remindersGrid.Dock = DockStyle.Fill;
        _remindersGrid.MinimumSize = new Size(0, 120);
        _remindersGrid.AllowUserToAddRows = false;
        _remindersGrid.AllowUserToResizeRows = false;
        _remindersGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _remindersGrid.MultiSelect = true;
        _remindersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _remindersGrid.RowHeadersVisible = false;
        _remindersGrid.BackgroundColor = SurfaceBack;
        _remindersGrid.BorderStyle = BorderStyle.None;
        _remindersGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _remindersGrid.GridColor = Border;
        _remindersGrid.EnableHeadersVisualStyles = false;
        _remindersGrid.ColumnHeadersHeight = 38;
        _remindersGrid.RowTemplate.Height = 34;
        _remindersGrid.Margin = new Padding(0);
        _remindersGrid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = TextMain,
            SelectionBackColor = Color.FromArgb(219, 234, 254),
            SelectionForeColor = TextMain,
            Font = Font
        };
        _remindersGrid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 250, 252)
        };
        _remindersGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = PrimaryDark,
            ForeColor = Color.White,
            SelectionBackColor = PrimaryDark,
            SelectionForeColor = Color.White,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold, GraphicsUnit.Point),
            Alignment = DataGridViewContentAlignment.MiddleCenter
        };

        _remindersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Id", Name = "Id", Visible = false });
        _remindersGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "启用", Name = "Enabled", FillWeight = 45, MinimumWidth = 72 });
        _remindersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "提前(分钟)",
            Name = "LeadMinutes",
            FillWeight = 72,
            MinimumWidth = 128,
            ValueType = typeof(int),
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
        _remindersGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "提醒时刻",
            Name = "Time",
            FillWeight = 70,
            MinimumWidth = 116,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
        _remindersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "提醒文案", Name = "Message", FillWeight = 220, MinimumWidth = 260, ReadOnly = true });
        _remindersGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "语音", Name = "Speak", FillWeight = 45, MinimumWidth = 72 });
        _remindersGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "通知", Name = "Toast", FillWeight = 45, MinimumWidth = 72 });
        _remindersGrid.CellEndEdit += (_, e) =>
        {
            if (_loadingSettings || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_remindersGrid.Columns[e.ColumnIndex].Name == "LeadMinutes")
            {
                TryRefreshReminderRow(_remindersGrid.Rows[e.RowIndex], _shutdownTimePicker.Value.ToString("HH:mm"));
            }
        };
    }

    private void LoadSettings()
    {
        _loadingSettings = true;
        _enabledCheckBox.Checked = Settings.Enabled;
        _autoShutdownCheckBox.Checked = Settings.AutoShutdown;
        _forceShutdownCheckBox.Checked = Settings.ForceShutdown;
        _startWithWindowsCheckBox.Checked = Settings.StartWithWindows;
        _shutdownTimeBadge.Text = Settings.ShutdownTime;
        LoadReminderRows();
        SyncShutdownPicker(Settings.ShutdownTime);
        _lastValidShutdownTime = Settings.ShutdownTime;
        UpdateShutdownPreview();
        SetStatus("");
        _loadingSettings = false;
    }

    private void LoadReminderRows()
    {
        _remindersGrid.Rows.Clear();
        foreach (var reminder in Settings.Reminders)
        {
            var leadMinutes = reminder.LeadMinutes ?? ReminderScheduleHelper.MinutesUntil(
                TimeOfDayParser.Parse(reminder.Time),
                TimeOfDayParser.Parse(Settings.ShutdownTime));

            AddReminderRow(reminder.Id, reminder.Enabled, leadMinutes, Settings.ShutdownTime, reminder.Speak, reminder.Toast);
        }
    }

    private void ApplyShutdownPickerChange()
    {
        if (_syncingShutdownTime || _loadingSettings)
        {
            return;
        }

        var selectedTime = _shutdownTimePicker.Value.ToString("HH:mm");
        ApplyShutdownTimeChange(selectedTime, updateReminders: true);
    }

    private void ApplyShutdownTimeChange(string newShutdownTime, bool updateReminders)
    {
        if (newShutdownTime == _lastValidShutdownTime)
        {
            UpdateShutdownPreview();
            return;
        }

        if (updateReminders)
        {
            try
            {
                RefreshReminderRowsForShutdownTime(newShutdownTime);
                SetStatus("已根据关机时间更新提醒");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
        }

        _lastValidShutdownTime = newShutdownTime;
        UpdateShutdownPreview();
    }

    private void SyncShutdownPicker(string shutdownTime)
    {
        var parsed = TimeOfDayParser.Parse(shutdownTime);
        _syncingShutdownTime = true;
        _shutdownTimePicker.Value = DateTime.Today.Add(parsed);
        _syncingShutdownTime = false;
    }

    private void RefreshReminderRowsForShutdownTime(string shutdownTime)
    {
        foreach (DataGridViewRow row in _remindersGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            RefreshReminderRow(row, shutdownTime);
        }
    }

    private void AddReminderRow(string id, bool enabled, int leadMinutes, string shutdownTime, bool speak, bool toast)
    {
        var rowIndex = _remindersGrid.Rows.Add(id, enabled, leadMinutes, "", "", speak, toast);
        RefreshReminderRow(_remindersGrid.Rows[rowIndex], shutdownTime);
    }

    private void TryRefreshReminderRow(DataGridViewRow row, string shutdownTime)
    {
        try
        {
            RefreshReminderRow(row, shutdownTime);
            SetStatus("已更新提醒时刻和文案");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private static void RefreshReminderRow(DataGridViewRow row, string shutdownTime)
    {
        var leadMinutes = ReadLeadMinutes(row);
        row.Cells["Time"].Value = ReminderScheduleHelper.GetReminderTime(shutdownTime, leadMinutes);
        row.Cells["Message"].Value = ReminderScheduleHelper.BuildReminderMessage(leadMinutes);
    }

    private bool TrySave()
    {
        if (!TryReadCurrentSettings(out var next))
        {
            return false;
        }

        Settings = next;
        return true;
    }

    private bool TryReadCurrentSettings(out AppSettings settings)
    {
        settings = AppSettings.CreateDefault();

        try
        {
            _remindersGrid.EndEdit();

            settings = new AppSettings
            {
                Enabled = _enabledCheckBox.Checked,
                ShutdownTime = _shutdownTimePicker.Value.ToString("HH:mm"),
                AutoShutdown = _autoShutdownCheckBox.Checked,
                ForceShutdown = _forceShutdownCheckBox.Checked,
                StartWithWindows = _startWithWindowsCheckBox.Checked,
                Reminders = ReadReminderRows()
            };

            settings.Normalize();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "设置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private List<ReminderSettings> ReadReminderRows()
    {
        var reminders = new List<ReminderSettings>();

        foreach (DataGridViewRow row in _remindersGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var leadMinutes = ReadLeadMinutes(row);
            var reminder = new ReminderSettings
            {
                Id = Convert.ToString(row.Cells["Id"].Value) ?? "",
                LeadMinutes = leadMinutes,
                Time = ReminderScheduleHelper.GetReminderTime(_shutdownTimePicker.Value.ToString("HH:mm"), leadMinutes),
                Message = ReminderScheduleHelper.BuildReminderMessage(leadMinutes),
                Enabled = Convert.ToBoolean(row.Cells["Enabled"].Value ?? true),
                Speak = Convert.ToBoolean(row.Cells["Speak"].Value ?? true),
                Toast = Convert.ToBoolean(row.Cells["Toast"].Value ?? true)
            };

            reminders.Add(reminder);
        }

        if (reminders.Count == 0)
        {
            throw new InvalidOperationException("至少需要保留一条提醒。");
        }

        return reminders;
    }

    private static int ReadLeadMinutes(DataGridViewRow row)
    {
        var raw = Convert.ToString(row.Cells["LeadMinutes"].Value)?.Trim();
        if (!int.TryParse(raw, out var leadMinutes) || !ReminderScheduleHelper.IsValidLeadMinutes(leadMinutes))
        {
            throw new InvalidOperationException("提前提醒时间必须是 1 到 1439 之间的整数分钟。");
        }

        return leadMinutes;
    }

    private static AppSettings Clone(AppSettings settings) => settings.Clone();

    private void UpdateShutdownPreview()
    {
        try
        {
            var shutdownTime = _shutdownTimePicker.Value.ToString("HH:mm");
            _shutdownTimeBadge.Text = shutdownTime;

            if (!_autoShutdownCheckBox.Checked)
            {
                _timeUntilShutdownLabel.Text = "自动关机已关闭";
                return;
            }

            var now = DateTime.Now;
            var next = ScheduleEngine.GetNextOccurrence(now, shutdownTime);
            _timeUntilShutdownLabel.Text = $"还有 {FormatDuration(next - now)}";
        }
        catch
        {
            _timeUntilShutdownLabel.Text = "时间格式待修正";
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (duration < TimeSpan.FromMinutes(1))
        {
            var totalSeconds = Math.Max(0, (int)Math.Ceiling(duration.TotalSeconds));
            return $"{totalSeconds}秒";
        }

        var totalMinutes = Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes));
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        return hours > 0 ? $"{hours}小时{minutes:00}分钟" : $"{minutes}分钟";
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
