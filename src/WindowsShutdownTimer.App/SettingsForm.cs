using WindowsShutdownTimer.Core;

namespace WindowsShutdownTimer.App;

public sealed class SettingsForm : Form
{
    private readonly CheckBox _enabledCheckBox = new() { Text = "启用提醒与关机", AutoSize = true };
    private readonly CheckBox _autoShutdownCheckBox = new() { Text = "到点自动关机", AutoSize = true };
    private readonly CheckBox _forceShutdownCheckBox = new() { Text = "强制关闭应用（可能丢失未保存内容）", AutoSize = true };
    private readonly CheckBox _startWithWindowsCheckBox = new() { Text = "登录 Windows 后自动启动", AutoSize = true };
    private readonly TextBox _shutdownTimeTextBox = new() { Width = 80 };
    private readonly DataGridView _remindersGrid = new();

    public SettingsForm(AppSettings settings)
    {
        Settings = Clone(settings);

        Text = "Windows 定时关机设置";
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(720, 460);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        BuildLayout();
        LoadSettings();
    }

    public AppSettings Settings { get; private set; }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true
        };
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
            Padding = new Padding(0, 10, 0, 10)
        };
        shutdownPanel.Controls.Add(new Label { Text = "每日关机时间", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 5, 6, 0) });
        shutdownPanel.Controls.Add(_shutdownTimeTextBox);
        shutdownPanel.Controls.Add(new Label { Text = "格式 HH:mm；24:00 会保存为 00:00", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        root.Controls.Add(shutdownPanel);

        ConfigureGrid();
        root.Controls.Add(_remindersGrid);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var saveButton = new Button { Text = "保存", Width = 90 };
        saveButton.Click += (_, _) =>
        {
            if (TrySave())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 90 };
        var defaultsButton = new Button { Text = "恢复默认提醒", Width = 120 };
        defaultsButton.Click += (_, _) =>
        {
            Settings.Reminders = AppSettings.CreateDefault().Reminders;
            LoadReminderRows();
        };

        var addButton = new Button { Text = "添加提醒", Width = 90 };
        addButton.Click += (_, _) => _remindersGrid.Rows.Add(Guid.NewGuid().ToString("N"), true, "23:45", "还有15分钟自动关机", true, true);

        var deleteButton = new Button { Text = "删除选中", Width = 90 };
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

        bottom.Controls.Add(saveButton);
        bottom.Controls.Add(cancelButton);
        bottom.Controls.Add(deleteButton);
        bottom.Controls.Add(addButton);
        bottom.Controls.Add(defaultsButton);
        root.Controls.Add(bottom);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void ConfigureGrid()
    {
        _remindersGrid.Dock = DockStyle.Fill;
        _remindersGrid.AllowUserToAddRows = false;
        _remindersGrid.AllowUserToResizeRows = false;
        _remindersGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _remindersGrid.MultiSelect = true;
        _remindersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _remindersGrid.RowHeadersVisible = false;

        _remindersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Id", Name = "Id", Visible = false });
        _remindersGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "启用", Name = "Enabled", FillWeight = 45 });
        _remindersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "提醒时间", Name = "Time", FillWeight = 70 });
        _remindersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "提醒文案", Name = "Message", FillWeight = 260 });
        _remindersGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "语音", Name = "Speak", FillWeight = 45 });
        _remindersGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "通知", Name = "Toast", FillWeight = 45 });
    }

    private void LoadSettings()
    {
        _enabledCheckBox.Checked = Settings.Enabled;
        _autoShutdownCheckBox.Checked = Settings.AutoShutdown;
        _forceShutdownCheckBox.Checked = Settings.ForceShutdown;
        _startWithWindowsCheckBox.Checked = Settings.StartWithWindows;
        _shutdownTimeTextBox.Text = Settings.ShutdownTime;
        LoadReminderRows();
    }

    private void LoadReminderRows()
    {
        _remindersGrid.Rows.Clear();
        foreach (var reminder in Settings.Reminders)
        {
            _remindersGrid.Rows.Add(reminder.Id, reminder.Enabled, reminder.Time, reminder.Message, reminder.Speak, reminder.Toast);
        }
    }

    private bool TrySave()
    {
        try
        {
            _remindersGrid.EndEdit();

            var next = new AppSettings
            {
                Enabled = _enabledCheckBox.Checked,
                ShutdownTime = TimeOfDayParser.Normalize(_shutdownTimeTextBox.Text),
                AutoShutdown = _autoShutdownCheckBox.Checked,
                ForceShutdown = _forceShutdownCheckBox.Checked,
                StartWithWindows = _startWithWindowsCheckBox.Checked,
                Reminders = ReadReminderRows()
            };

            next.Normalize();
            Settings = next;
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

            var time = Convert.ToString(row.Cells["Time"].Value) ?? "";
            var message = Convert.ToString(row.Cells["Message"].Value) ?? "";
            var reminder = new ReminderSettings
            {
                Id = Convert.ToString(row.Cells["Id"].Value) ?? "",
                Time = TimeOfDayParser.Normalize(time),
                Message = message.Trim(),
                Enabled = Convert.ToBoolean(row.Cells["Enabled"].Value ?? true),
                Speak = Convert.ToBoolean(row.Cells["Speak"].Value ?? true),
                Toast = Convert.ToBoolean(row.Cells["Toast"].Value ?? true)
            };

            if (string.IsNullOrWhiteSpace(reminder.Message))
            {
                throw new InvalidOperationException("提醒文案不能为空。");
            }

            reminders.Add(reminder);
        }

        if (reminders.Count == 0)
        {
            throw new InvalidOperationException("至少需要保留一条提醒。");
        }

        return reminders;
    }

    private static AppSettings Clone(AppSettings settings)
    {
        return new AppSettings
        {
            Enabled = settings.Enabled,
            ShutdownTime = settings.ShutdownTime,
            AutoShutdown = settings.AutoShutdown,
            ForceShutdown = settings.ForceShutdown,
            StartWithWindows = settings.StartWithWindows,
            Reminders = settings.Reminders
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
}
