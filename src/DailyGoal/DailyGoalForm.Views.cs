namespace DailyGoal;

internal sealed partial class DailyGoalForm
{
    private void BuildTodayView()
    {
        todayView.Location = new Point(0, UiTheme.S(144));
        todayView.Size = new Size(ExpandedWidth, appHeight - UiTheme.S(144));
        todayView.BackColor = UiTheme.Canvas;
        content.Controls.Add(todayView);

        inputShell.Location = new Point(UiTheme.S(32), UiTheme.S(14));
        inputShell.Size = new Size(UiTheme.S(412), UiTheme.S(54));
        todayView.Controls.Add(inputShell);

        taskInput.Location = new Point(UiTheme.S(17), UiTheme.S(15));
        taskInput.Size = new Size(UiTheme.S(378), UiTheme.S(30));
        taskInput.Font = UiTheme.BodyFont;
        taskInput.BorderStyle = BorderStyle.None;
        taskInput.BackColor = UiTheme.Surface;
        taskInput.ForeColor = UiTheme.Text;
        taskInput.PlaceholderText = "写下一件想完成的事";
        taskInput.MaxLength = 160;
        taskInput.KeyDown += (_, args) =>
        {
            if (args.Control && args.KeyCode == Keys.V && TryAddTasksFromClipboard())
            {
                args.SuppressKeyPress = true;
                return;
            }

            if (args.KeyCode != Keys.Enter)
            {
                return;
            }

            args.SuppressKeyPress = true;
            AddTask();
        };
        inputShell.Controls.Add(taskInput);

        ConfigureIconButton(templateButton, IconKind.Template, "常用任务模板", new Point(UiTheme.S(452), UiTheme.S(19)), new Size(UiTheme.S(46), UiTheme.S(44)));
        templateButton.BackColor = UiTheme.Canvas;
        templateButton.Click += (_, _) => ShowTemplateMenu();
        todayView.Controls.Add(templateButton);

        ConfigureIconButton(addButton, IconKind.Add, "添加任务", new Point(ExpandedWidth - UiTheme.S(90), UiTheme.S(14)), new Size(UiTheme.S(58), UiTheme.S(54)));
        addButton.Tone = IconButtonTone.Accent;
        addButton.BackColor = UiTheme.Canvas;
        addButton.Click += (_, _) => AddTask();
        todayView.Controls.Add(addButton);

        remainingLabel.Location = new Point(UiTheme.S(35), UiTheme.S(84));
        remainingLabel.Size = new Size(UiTheme.S(450), UiTheme.S(28));
        remainingLabel.Font = UiTheme.SmallBoldFont;
        remainingLabel.ForeColor = UiTheme.TextMuted;
        remainingLabel.BackColor = UiTheme.Canvas;
        todayView.Controls.Add(remainingLabel);

        taskList.Location = new Point(UiTheme.S(22), UiTheme.S(116));
        taskList.Size = new Size(ExpandedWidth - UiTheme.S(44), todayView.Height - UiTheme.S(128));
        taskList.Padding = new Padding(UiTheme.S(7), UiTheme.S(6), UiTheme.S(7), UiTheme.S(8));
        taskList.BackColor = UiTheme.Canvas;
        todayView.Controls.Add(taskList);

        todayEmpty.Location = taskList.Location;
        todayEmpty.Size = taskList.Size;
        todayView.Controls.Add(todayEmpty);
        todayEmpty.BringToFront();
    }

    private void BuildHistoryView()
    {
        historyView.Location = new Point(0, UiTheme.S(144));
        historyView.Size = new Size(ExpandedWidth, appHeight - UiTheme.S(144));
        historyView.BackColor = UiTheme.Canvas;
        historyView.Visible = false;
        content.Controls.Add(historyView);

        historySummary.Location = new Point(UiTheme.S(32), UiTheme.S(12));
        historySummary.Size = new Size(ExpandedWidth - UiTheme.S(64), UiTheme.S(150));
        historyView.Controls.Add(historySummary);

        historyList.Location = new Point(UiTheme.S(22), UiTheme.S(172));
        historyList.Size = new Size(ExpandedWidth - UiTheme.S(44), historyView.Height - UiTheme.S(184));
        historyList.Padding = new Padding(UiTheme.S(7), UiTheme.S(4), UiTheme.S(7), UiTheme.S(8));
        historyList.BackColor = UiTheme.Canvas;
        historyView.Controls.Add(historyList);

        historyEmpty.Title = "还没有完成记录";
        historyEmpty.Subtitle = "完成一件事后，它会出现在这里";
        historyEmpty.Location = historyList.Location;
        historyEmpty.Size = historyList.Size;
        historyView.Controls.Add(historyEmpty);
        historyEmpty.BringToFront();
    }

    private int settingsCursor;

    private void AddSettingText(string title, string subtitle, int textWidth = 300)
    {
        var titleLabel = CreateLabel(title, new Point(UiTheme.S(34), settingsCursor), new Size(UiTheme.S(textWidth), UiTheme.S(31)), UiTheme.BodyBoldFont, UiTheme.Text);
        var subtitleLabel = CreateLabel(subtitle, new Point(UiTheme.S(34), settingsCursor + UiTheme.S(32)), new Size(UiTheme.S(textWidth), UiTheme.S(28)), UiTheme.SmallFont, UiTheme.TextMuted);
        settingsContent.Controls.Add(titleLabel);
        settingsContent.Controls.Add(subtitleLabel);
    }

    private void AddSettingSeparator()
    {
        var separator = new Panel
        {
            Location = new Point(UiTheme.S(34), settingsCursor),
            Size = new Size(ExpandedWidth - UiTheme.S(68), 1),
            BackColor = UiTheme.Border
        };
        settingsContent.Controls.Add(separator);
        settingsCursor += UiTheme.S(16);
    }

    /// <summary>右侧放开关的标准行：标题 + 副标题 + 右上角控件。</summary>
    private void AddToggleRow(string title, string subtitle, Control trailing)
    {
        AddSettingText(title, subtitle);
        trailing.Location = new Point(ExpandedWidth - UiTheme.S(90), settingsCursor + UiTheme.S(7));
        settingsContent.Controls.Add(trailing);
        settingsCursor += UiTheme.S(68);
        AddSettingSeparator();
    }

    private void BuildSettingsView()
    {
        settingsView.Location = Point.Empty;
        settingsView.Size = new Size(ExpandedWidth, appHeight);
        settingsView.BackColor = UiTheme.WindowTop;
        settingsView.Visible = false;
        content.Controls.Add(settingsView);

        ConfigureIconButton(settingsBackButton, IconKind.Back, "返回", new Point(UiTheme.S(24), UiTheme.S(23)), new Size(UiTheme.S(42), UiTheme.S(42)));
        settingsBackButton.Click += (_, _) => HideSettings();
        settingsView.Controls.Add(settingsBackButton);

        var settingsTitle = CreateLabel("设置", new Point(UiTheme.S(76), UiTheme.S(22)), new Size(UiTheme.S(280), UiTheme.S(40)), UiTheme.TitleFont, UiTheme.Text);
        settingsView.Controls.Add(settingsTitle);

        ConfigureIconButton(settingsCloseButton, IconKind.Close, "关闭", new Point(ExpandedWidth - UiTheme.S(48), UiTheme.S(24)), new Size(UiTheme.S(40), UiTheme.S(40)));
        settingsCloseButton.Tone = IconButtonTone.Danger;
        settingsCloseButton.Click += (_, _) => Close();
        settingsView.Controls.Add(settingsCloseButton);

        settingsScroll.Location = new Point(0, UiTheme.S(76));
        settingsScroll.Size = new Size(ExpandedWidth, appHeight - UiTheme.S(80));
        settingsScroll.BackColor = UiTheme.WindowTop;
        settingsView.Controls.Add(settingsScroll);

        settingsContent.Width = ExpandedWidth;
        settingsContent.BackColor = UiTheme.WindowTop;
        settingsContent.Margin = Padding.Empty;
        settingsScroll.Controls.Add(settingsContent);

        settingsCursor = UiTheme.S(10);

        // 主题：标题下方放三张色卡。
        AddSettingText("主题", "三套配色随时切换", 390);
        themeChoice.Location = new Point(UiTheme.S(34), settingsCursor + UiTheme.S(64));
        themeChoice.Size = new Size(ExpandedWidth - UiTheme.S(68), UiTheme.S(92));
        themeChoice.SelectedChanged += (_, _) =>
        {
            if (!suppressSettingsEvents)
            {
                SelectTheme(themeChoice.SelectedTheme);
            }
        };
        settingsContent.Controls.Add(themeChoice);
        settingsCursor += UiTheme.S(168);
        AddSettingSeparator();

        autoHideToggle.CheckedChanged += (_, _) => SaveSettingsFromControls();
        AddToggleRow("自动隐藏", "鼠标离开后收回屏幕边缘", autoHideToggle);

        AddSettingText("隐藏延迟", "离开窗口后等待多久收回", 230);
        hideDelaySlider.Location = new Point(UiTheme.S(280), settingsCursor + UiTheme.S(12));
        hideDelaySlider.Size = new Size(UiTheme.S(216), UiTheme.S(36));
        hideDelaySlider.ValueChanged += (_, _) =>
        {
            hideDelayValue.Text = $"{hideDelaySlider.Value / 1000d:0.##}s";
            if (!suppressSettingsEvents)
            {
                state.Settings.AutoHideDelayMs = hideDelaySlider.Value;
            }
        };
        hideDelaySlider.ValueCommitted += (_, _) => SaveSettingsFromControls();
        settingsContent.Controls.Add(hideDelaySlider);

        hideDelayValue.Location = new Point(UiTheme.S(504), settingsCursor + UiTheme.S(12));
        hideDelayValue.Size = new Size(UiTheme.S(64), UiTheme.S(36));
        hideDelayValue.Font = UiTheme.SmallBoldFont;
        hideDelayValue.ForeColor = UiTheme.TextMuted;
        hideDelayValue.BackColor = UiTheme.WindowTop;
        hideDelayValue.TextAlign = ContentAlignment.MiddleRight;
        settingsContent.Controls.Add(hideDelayValue);
        settingsCursor += UiTheme.S(68);
        AddSettingSeparator();

        AddSettingText("停靠位置", "贴在屏幕右缘或左缘", 260);
        dockChips.Labels = ["右侧", "左侧"];
        dockChips.Location = new Point(ExpandedWidth - UiTheme.S(216), settingsCursor + UiTheme.S(9));
        dockChips.Size = new Size(UiTheme.S(182), UiTheme.S(42));
        dockChips.SelectedChanged += (_, _) => SaveSettingsFromControls();
        settingsContent.Controls.Add(dockChips);
        settingsCursor += UiTheme.S(68);
        AddSettingSeparator();

        availableScreens = Screen.AllScreens;
        if (availableScreens.Length > 1)
        {
            AddSettingText("所在显示器", "选择贴边停靠的屏幕", 240);
            var chips = new ChoiceChips
            {
                Labels = availableScreens
                    .Select((screen, index) => screen.Primary ? $"主屏" : $"屏幕 {index + 1}")
                    .ToArray()
            };
            var chipsWidth = Math.Min(ExpandedWidth - UiTheme.S(300), availableScreens.Length * UiTheme.S(92));
            chips.Location = new Point(ExpandedWidth - UiTheme.S(34) - chipsWidth, settingsCursor + UiTheme.S(9));
            chips.Size = new Size(chipsWidth, UiTheme.S(42));
            chips.SelectedChanged += (_, _) => SaveSettingsFromControls();
            settingsContent.Controls.Add(chips);
            screenChips = chips;
            settingsCursor += UiTheme.S(68);
            AddSettingSeparator();
        }

        AddSettingText("界面大小", "按所在屏幕安全缩放", 220);
        scaleChips.Labels = ["自适应", "100%", "125%", "150%"];
        scaleChips.Location = new Point(ExpandedWidth - UiTheme.S(328), settingsCursor + UiTheme.S(9));
        scaleChips.Size = new Size(UiTheme.S(294), UiTheme.S(42));
        scaleChips.SelectedChanged += (_, _) => ScaleChipsOnChanged();
        settingsContent.Controls.Add(scaleChips);
        settingsCursor += UiTheme.S(68);
        AddSettingSeparator();

        AddSettingText("全局快捷键", "在任意界面按下即可快速记一条", 390);
        hotkeyToggle.Location = new Point(ExpandedWidth - UiTheme.S(90), settingsCursor + UiTheme.S(7));
        hotkeyToggle.CheckedChanged += (_, _) => SaveSettingsFromControls();
        settingsContent.Controls.Add(hotkeyToggle);
        hotkeyChips.Labels = ["Ctrl+Alt+D", "Ctrl+Alt+空格", "Ctrl+Shift+D"];
        hotkeyChips.Location = new Point(UiTheme.S(150), settingsCursor + UiTheme.S(64));
        hotkeyChips.Size = new Size(UiTheme.S(418), UiTheme.S(44));
        hotkeyChips.SelectedChanged += (_, _) => SaveSettingsFromControls();
        settingsContent.Controls.Add(hotkeyChips);
        settingsCursor += UiTheme.S(124);
        AddSettingSeparator();

        AddSettingText("傍晚提醒", "到点后若还有待办，托盘气泡提醒一次", 390);
        reminderToggle.Location = new Point(ExpandedWidth - UiTheme.S(90), settingsCursor + UiTheme.S(7));
        reminderToggle.CheckedChanged += (_, _) => SaveSettingsFromControls();
        settingsContent.Controls.Add(reminderToggle);
        reminderChips.Labels = ["17:00", "18:00", "19:00", "20:00", "21:00"];
        reminderChips.Location = new Point(UiTheme.S(150), settingsCursor + UiTheme.S(64));
        reminderChips.Size = new Size(UiTheme.S(418), UiTheme.S(44));
        reminderChips.SelectedChanged += (_, _) => SaveSettingsFromControls();
        settingsContent.Controls.Add(reminderChips);
        settingsCursor += UiTheme.S(124);
        AddSettingSeparator();

        AddSettingText("完成粒子", "选择庆祝效果的丰富程度", 390);
        particleChoice.Location = new Point(UiTheme.S(150), settingsCursor + UiTheme.S(64));
        particleChoice.Size = new Size(UiTheme.S(418), UiTheme.S(48));
        particleChoice.SelectedChanged += (_, _) => SaveSettingsFromControls();
        settingsContent.Controls.Add(particleChoice);
        settingsCursor += UiTheme.S(124);
        AddSettingSeparator();

        reducedMotionToggle.CheckedChanged += (_, _) => SaveSettingsFromControls();
        AddToggleRow("减少动态效果", "关闭粒子并缩短界面动画", reducedMotionToggle);

        startupToggle.CheckedChanged += StartupToggleOnChanged;
        AddToggleRow("开机启动", "登录 Windows 后自动驻留托盘", startupToggle);

        AddSettingText("本地数据", "自动保留 30 天快照，可手动迁移任务", 300);
        exportBackupButton.Text = "导出备份";
        exportBackupButton.Location = new Point(UiTheme.S(350), settingsCursor + UiTheme.S(10));
        exportBackupButton.Size = new Size(UiTheme.S(100), UiTheme.S(44));
        exportBackupButton.Click += (_, _) => ExportBackup();
        settingsContent.Controls.Add(exportBackupButton);

        importBackupButton.Text = "导入备份";
        importBackupButton.Location = new Point(UiTheme.S(462), settingsCursor + UiTheme.S(10));
        importBackupButton.Size = new Size(UiTheme.S(106), UiTheme.S(44));
        importBackupButton.Click += (_, _) => ImportBackup();
        settingsContent.Controls.Add(importBackupButton);
        settingsCursor += UiTheme.S(68);
        AddSettingSeparator();

        AddSettingText("数据位置", "放到云盘同步文件夹即可多机同步（重启生效）", 420);
        dataPathLabel.Location = new Point(UiTheme.S(34), settingsCursor + UiTheme.S(62));
        dataPathLabel.Size = new Size(ExpandedWidth - UiTheme.S(68), UiTheme.S(26));
        dataPathLabel.Font = UiTheme.SmallFont;
        dataPathLabel.ForeColor = UiTheme.TextFaint;
        dataPathLabel.BackColor = UiTheme.WindowTop;
        dataPathLabel.AutoEllipsis = true;
        settingsContent.Controls.Add(dataPathLabel);

        openDataFolderButton.Text = "打开文件夹";
        openDataFolderButton.Location = new Point(UiTheme.S(34), settingsCursor + UiTheme.S(94));
        openDataFolderButton.Size = new Size(UiTheme.S(128), UiTheme.S(44));
        openDataFolderButton.Click += (_, _) => OpenDataFolder();
        settingsContent.Controls.Add(openDataFolderButton);

        changeDataFolderButton.Text = "更改位置…";
        changeDataFolderButton.Location = new Point(UiTheme.S(174), settingsCursor + UiTheme.S(94));
        changeDataFolderButton.Size = new Size(UiTheme.S(128), UiTheme.S(44));
        changeDataFolderButton.Click += (_, _) => ChangeDataDirectory();
        settingsContent.Controls.Add(changeDataFolderButton);

        resetDataFolderButton.Text = "恢复默认";
        resetDataFolderButton.Location = new Point(UiTheme.S(314), settingsCursor + UiTheme.S(94));
        resetDataFolderButton.Size = new Size(UiTheme.S(118), UiTheme.S(44));
        resetDataFolderButton.Visible = false;
        resetDataFolderButton.Click += (_, _) => ResetDataDirectory();
        settingsContent.Controls.Add(resetDataFolderButton);
        settingsCursor += UiTheme.S(152);

        var note = CreateLabel(
            "设置会立即保存到本机。",
            new Point(UiTheme.S(34), settingsCursor),
            new Size(ExpandedWidth - UiTheme.S(68), UiTheme.S(30)),
            UiTheme.SmallFont,
            UiTheme.TextFaint);
        settingsContent.Controls.Add(note);
        settingsCursor += UiTheme.S(40);

        settingsContent.Height = settingsCursor;
        settingsScroll.Relayout();
    }
}
