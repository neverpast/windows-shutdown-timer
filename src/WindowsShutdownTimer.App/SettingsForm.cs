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
        ClientSize = new Size(820, 520);
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = ShellBack;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;

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
            RowCount = 5,
            Padding = new Padding(18),
            BackColor = ShellBack
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
        _shutdownTimeTextBox.BorderStyle = BorderStyle.FixedSingle;
        _shutdownTimeTextBox.BackColor = Color.White;
        _shutdownTimeTextBox.ForeColor = TextMain;
        shutdownPanel.Controls.Add(new Label { Text = "每日关机时间", AutoSize = true, ForeColor = TextMain, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 5, 6, 0) });
        shutdownPanel.Controls.Add(_shutdownTimeTextBox);
        shutdownPanel.Controls.Add(new Label { Text = "格式 HH:mm；24:00 会保存为 00:00", AutoSize = true, ForeColor = TextMuted, Padding = new Padding(8, 5, 0, 0) });
        root.Controls.Add(shutdownPanel);

        ConfigureGrid();
        root.Controls.Add(_remindersGrid);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = ShellBack
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var editButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            BackColor = ShellBack
        };

        var commitButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            BackColor = ShellBack
        };

        var saveButton = CreateBottomButton("保存", 96, ButtonTone.Primary);
        saveButton.Click += (_, _) =>
        {
            if (TrySave())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        var cancelButton = CreateBottomButton("取消", 96, ButtonTone.Secondary);
        cancelButton.DialogResult = DialogResult.Cancel;
        var defaultsButton = CreateBottomButton("恢复默认提醒", 136, ButtonTone.Secondary);
        defaultsButton.Click += (_, _) =>
        {
            Settings.Reminders = AppSettings.CreateDefault().Reminders;
            LoadReminderRows();
        };

        var addButton = CreateBottomButton("添加提醒", 104, ButtonTone.Accent);
        addButton.Click += (_, _) => _remindersGrid.Rows.Add(Guid.NewGuid().ToString("N"), true, "23:45", "还有15分钟自动关机", true, true);

        var deleteButton = CreateBottomButton("删除选中", 104, ButtonTone.Danger);
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

        editButtons.Controls.Add(defaultsButton);
        editButtons.Controls.Add(addButton);
        editButtons.Controls.Add(deleteButton);
        commitButtons.Controls.Add(saveButton);
        commitButtons.Controls.Add(cancelButton);
        bottom.Controls.Add(editButtons, 0, 0);
        bottom.Controls.Add(commitButtons, 1, 0);
        root.Controls.Add(bottom);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private Panel CreateHeaderPanel()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 82,
            BackColor = Primary,
            Margin = new Padding(0)
        };

        var title = new Label
        {
            Text = "Windows 定时关机",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.White,
            Location = new Point(18, 16)
        };

        var subtitle = new Label
        {
            Text = "每日提醒、语音倒计时、到点自动关机",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(219, 234, 254),
            Location = new Point(20, 50)
        };

        var timeBadge = new Label
        {
            Text = "00:00",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.White,
            BackColor = PrimaryDark,
            Size = new Size(94, 42),
            Location = new Point(ClientSize.Width - 130, 20)
        };

        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(timeBadge);
        return header;
    }

    private static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.ForeColor = TextMain;
        checkBox.Margin = new Padding(0, 0, 18, 0);
        checkBox.Padding = new Padding(0, 2, 0, 2);
        checkBox.UseVisualStyleBackColor = false;
        checkBox.BackColor = SurfaceBack;
    }

    private static Button CreateBottomButton(string text, int width, ButtonTone tone)
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
            Height = 36,
            Margin = new Padding(4, 0, 4, 0),
            AutoEllipsis = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = foreColor,
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
