using System.Diagnostics;
using System.Globalization;

namespace DailyGoal;

internal enum MainView
{
    Today,
    Tomorrow,
    History
}

internal abstract record UndoEntry;

internal sealed record UndoCompletion(string TaskId, int PreviousSortOrder, DateTimeOffset CompletedAt) : UndoEntry;

/// <summary>删除类操作的撤销：保存被删任务快照；停止重复系列时记下需要重新启用的系列 Id。</summary>
internal sealed record UndoDeletion(TaskRecord Snapshot, string? ReenableRecurrenceId) : UndoEntry;

internal sealed partial class DailyGoalForm : Form
{
    private static int ExpandedWidth => UiTheme.S(600);
    private static int CollapsedWidth => UiTheme.S(42);
    private const int CompletionDelayMs = 1250;
    private const int UndoDurationMs = 6000;
    private const int MaxActiveParticles = 120;
    /// <summary>收起态悬停多久才展开，避免鼠标扫过屏幕角落时误触。</summary>
    private const int PeekExpandIntentMs = 240;
    private const int HotkeyId = 0xDA11;
    private static readonly int TaskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");

    private Rectangle workArea;
    private readonly int appHeight;
    private readonly StateStore stateStore;
    private readonly BackupService backupService;
    private readonly IStartupRegistration startupRegistration;
    private readonly IAppClock clock;
    private readonly ScheduleService scheduleService = new();
    private readonly RecurrenceService recurrenceService = new();
    private AppState state;
    private DateOnly currentDate;
    private string? pendingStorageWarning;
    private bool dwmCornersApplied;

    private readonly HealingPanel content = new();
    private readonly PeekRail peek = new();
    private readonly WindowGrip dragGrip = new();
    private readonly Label titleLabel = new();
    private readonly Label dateLabel = new();
    private readonly ProgressRing progressRing = new();
    private readonly CharacterBadgeControl themeCharacter = new();
    private readonly IconButton settingsButton = new();
    private readonly IconButton moreButton = new();
    private readonly IconButton closeButton = new();
    private readonly SegmentButton todayTab = new();
    private readonly SegmentButton tomorrowTab = new();
    private readonly SegmentButton historyTab = new();
    private readonly Panel todayView = new();
    private readonly Panel historyView = new();
    private readonly HealingPanel settingsView = new();
    private readonly HealingPanel templateView = new();
    private readonly InputShell inputShell = new();
    private readonly TextBox taskInput = new();
    private readonly IconButton addButton = new();
    private readonly IconButton templateButton = new();
    private readonly Label remainingLabel = new();
    private readonly TaskFlowPanel taskList = new();
    private readonly EmptyStateControl todayEmpty = new();
    private readonly HistorySummaryControl historySummary = new();
    private readonly StackScrollPanel historyList = new();
    private readonly EmptyStateControl historyEmpty = new();
    private readonly UndoBar undoBar = new();
    private readonly IconButton settingsBackButton = new();
    private readonly IconButton settingsCloseButton = new();
    private readonly StackScrollPanel settingsScroll = new();
    private readonly Panel settingsContent = new();
    private readonly ThemeChoiceControl themeChoice = new();
    private readonly ToggleSwitch autoHideToggle = new();
    private readonly DelaySlider hideDelaySlider = new();
    private readonly Label hideDelayValue = new();
    private readonly ChoiceChips dockChips = new();
    private ChoiceChips? screenChips;
    private Screen[] availableScreens = [];
    private readonly ChoiceChips scaleChips = new();
    private readonly ToggleSwitch hotkeyToggle = new();
    private readonly ChoiceChips hotkeyChips = new();
    private readonly ToggleSwitch reminderToggle = new();
    private readonly ChoiceChips reminderChips = new();
    private readonly ParticleChoiceControl particleChoice = new();
    private readonly ToggleSwitch reducedMotionToggle = new();
    private readonly ToggleSwitch startupToggle = new();
    private readonly SegmentButton exportBackupButton = new();
    private readonly SegmentButton importBackupButton = new();
    private readonly Label dataPathLabel = new();
    private readonly SegmentButton openDataFolderButton = new();
    private readonly SegmentButton changeDataFolderButton = new();
    private readonly SegmentButton resetDataFolderButton = new();
    private readonly IconButton templateBackButton = new();
    private readonly IconButton templateCloseButton = new();
    private readonly ListBox templateList = new();
    private readonly TextBox templateEditor = new();
    private readonly SegmentButton renameTemplateButton = new();
    private readonly SegmentButton deleteTemplateButton = new();
    private readonly HealingPanel recurringView = new();
    private readonly IconButton recurringBackButton = new();
    private readonly IconButton recurringCloseButton = new();
    private readonly ListBox recurringList = new();
    private readonly ChoiceChips recurringRuleChips = new();
    private readonly WeekdayPicker recurringWeekdays = new();
    private readonly Label recurringEnableLabel = new();
    private readonly ToggleSwitch recurringEnableToggle = new();
    private readonly SegmentButton deleteRecurringButton = new();
    private readonly StatusToast statusToast = new();
    private readonly ToolTip toolTip = new();
    private readonly ContextMenuStrip moreMenu = new();
    private readonly ContextMenuStrip taskMenu = new();
    private readonly ContextMenuStrip templateMenu = new();
    private readonly bool trayEnabled;
    private readonly bool suppressDialogs;
    private NotifyIcon? trayIcon;
    private ContextMenuStrip? trayMenu;
    private ToolStripMenuItem? trayRemainingItem;

    private readonly System.Windows.Forms.Timer slideTimer = new();
    private readonly System.Windows.Forms.Timer hoverTimer = new();
    private readonly System.Windows.Forms.Timer particleTimer = new();
    private readonly System.Windows.Forms.Timer undoTimer = new();
    private readonly System.Windows.Forms.Timer dateTimer = new();
    private readonly System.Windows.Forms.Timer statusTimer = new();
    private readonly HashSet<System.Windows.Forms.Timer> rowAnimationTimers = [];
    private readonly Dictionary<string, System.Windows.Forms.Timer> completionTimers = [];
    private readonly Dictionary<string, TaskRowControl> taskRows = [];

    private readonly List<Particle> particles = [];
    private readonly Random random = new();
    private readonly ParticleLayer particleLayer;
    private readonly Stopwatch particleClock = new();
    private double lastParticleElapsedMs;

    private int targetWidth;
    private bool slideUsesPosition;
    private DateTime lastInside = DateTime.Now;
    private DateTime? peekHoverStart;
    private MainView currentView = MainView.Today;
    private bool settingsVisible;
    private bool dragInProgress;
    private bool suppressSettingsEvents;
    private bool suppressRecurringEvents;
    private bool resourcesDisposed;
    private bool backupWarningShown;
    private bool hotkeyRegistered;
    private bool railDragging;
    private bool suppressActivationOnShow;
    private List<string>? dragOriginalOrder;
    private TaskRowControl? activeDragRow;
    private TaskDragGhost? taskDragGhost;
    private Point dragGrabOffset;
    private UndoEntry? undoEntry;
    private int templateDragIndex = -1;
    private Image? xuanlingAvatar;
    private Image? xuanlingScene;

    public string DataDirectory => stateStore.DataDirectory;
    public bool DataDirectoryExists => Directory.Exists(DataDirectory);

    /// <summary>更改数据位置/界面缩放后请求重启：主循环退出后由 Program 拉起新进程。</summary>
    public bool RestartRequested { get; private set; }

    protected override CreateParams CreateParams
    {
        get
        {
            const int csDropShadow = 0x00020000;
            var createParams = base.CreateParams;
            createParams.ClassStyle |= csDropShadow;
            return createParams;
        }
    }

    protected override bool ShowWithoutActivation => suppressActivationOnShow || base.ShowWithoutActivation;

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == TaskbarCreatedMessage)
        {
            HandleTaskbarCreated();
        }
        else if (message.Msg == NativeMethods.WmHotkey && message.WParam.ToInt32() == HotkeyId)
        {
            ShowFromTray(true);
        }

        base.WndProc(ref message);
    }

    public DailyGoalForm(
        string? dataDirectoryOverride = null,
        int? appHeightOverride = null,
        bool enableTrayIcon = true,
        IStartupRegistration? startupRegistrationOverride = null,
        bool suppressDialogsForTests = false,
        IAppClock? clockOverride = null)
    {
        trayEnabled = enableTrayIcon;
        suppressDialogs = suppressDialogsForTests;
        startupRegistration = startupRegistrationOverride ?? new StartupRegistration();
        clock = clockOverride ?? new SystemAppClock();
        currentDate = clock.Today;
        stateStore = new StateStore(dataDirectoryOverride);
        backupService = new BackupService(stateStore.DataDirectory);
        var loadResult = stateStore.Load(currentDate);
        state = loadResult.State;
        pendingStorageWarning = loadResult.Warning;
        particleLayer = new ParticleLayer(particles);

        workArea = ResolveWorkArea();
        appHeight = appHeightOverride is null
            ? Math.Min(UiTheme.S(840), Math.Max(UiTheme.S(500), workArea.Height - UiTheme.S(28)))
            : Math.Clamp(appHeightOverride.Value, 500, 840);

        var scheduleChanged = scheduleService.ApplyRollover(state, currentDate);
        scheduleChanged |= recurrenceService.EnsureTasksForDate(state, currentDate, clock.Now);
        scheduleChanged |= state.Normalize(currentDate);

        if ((loadResult.NeedsSave || scheduleChanged) && !stateStore.Save(state, out var saveError))
        {
            AppendStorageWarning(saveError);
        }

        if (File.Exists(stateStore.DataFile)
            && !backupService.EnsureDailySnapshot(state, currentDate, out var backupError))
        {
            AppendStorageWarning(backupError);
            backupWarningShown = true;
        }

        ReconcileStartupRegistration();

        BuildWindow();
        xuanlingAvatar = LoadThemeImage("xuanling-avatar.png");
        xuanlingScene = LoadThemeImage("xuanling-scene.png");
        BuildContent();
        WireTimers();
        ApplySettingsToControls();
        RenderAll();
        SwitchPalette(state.Settings.Theme);

        Shown += (_, _) => ShowPendingStorageWarning();
    }

    private Rectangle ResolveWorkArea()
    {
        var screens = Screen.AllScreens;
        var preferred = state.Settings.PreferredScreenDevice;
        var screen = string.IsNullOrWhiteSpace(preferred)
            ? Screen.PrimaryScreen
            : screens.FirstOrDefault(candidate =>
                string.Equals(candidate.DeviceName, preferred, StringComparison.OrdinalIgnoreCase))
                ?? Screen.PrimaryScreen;
        return screen?.WorkingArea ?? Screen.GetWorkingArea(Point.Empty);
    }

    /// <summary>窗口的目标纵向位置：工作区顶部 + 用户拖动记忆的偏移，越界时收紧。</summary>
    private int WindowTop
    {
        get
        {
            var maxOffset = Math.Max(0, workArea.Height - appHeight - 4);
            return workArea.Top + Math.Clamp(state.Settings.WindowTopOffset, 0, maxOffset);
        }
    }

    /// <summary>按实际窗口宽度贴边；用于无法把固定宽窗体滑出虚拟桌面的兼容路径。</summary>
    private int DockedLeft(int width)
    {
        return state.Settings.DockSide == DockSide.Left
            ? workArea.Left + 2
            : workArea.Right - width - 2;
    }

    /// <summary>固定 600px 窗体按可见宽度滑出屏幕，只留下收起条。</summary>
    private int SlidingDockedLeft(int visibleWidth)
    {
        return state.Settings.DockSide == DockSide.Left
            ? workArea.Left - (ExpandedWidth - visibleWidth) + 2
            : workArea.Right - visibleWidth - 2;
    }

    private bool CanSlideOffscreen()
    {
        var screen = Screen.FromRectangle(workArea);
        var virtualScreen = SystemInformation.VirtualScreen;
        return state.Settings.DockSide == DockSide.Left
            ? workArea.Left == screen.Bounds.Left && screen.Bounds.Left == virtualScreen.Left
            : workArea.Right == screen.Bounds.Right && screen.Bounds.Right == virtualScreen.Right;
    }

    private void UpdatePeekRailPlacement()
    {
        peek.Dock = DockStyle.None;
        peek.Size = new Size(CollapsedWidth, appHeight);
        peek.Location = slideUsesPosition && state.Settings.DockSide == DockSide.Left
            ? new Point(ExpandedWidth - CollapsedWidth, 0)
            : Point.Empty;
    }

    /// <summary>
    /// 切换调色板并把整棵控件树上引用旧色板的 BackColor/ForeColor 映射到新色板。
    /// 控件构造时拿到的是当时的主题色，owner-draw 的部分在重绘时会自动读新值。
    /// </summary>
    private void SwitchPalette(AppThemeMode mode)
    {
        if (UiTheme.Mode == mode)
        {
            ApplyThemeArtwork();
            return;
        }

        var oldPalette = UiTheme.PaletteSnapshot();
        UiTheme.Apply(mode);
        var newPalette = UiTheme.PaletteSnapshot();
        var map = new Dictionary<int, Color>(oldPalette.Length);
        for (var index = 0; index < oldPalette.Length; index += 1)
        {
            map[oldPalette[index].ToArgb()] = newPalette[index];
        }

        RemapThemeColors(this, map);
        foreach (var menu in new[] { moreMenu, taskMenu, templateMenu, trayMenu })
        {
            if (menu is not null)
            {
                menu.BackColor = UiTheme.Surface;
                menu.ForeColor = UiTheme.Text;
            }
        }

        ApplyThemeArtwork();
        ApplyListScrollTheme();
        Invalidate(true);
    }

    /// <summary>ListBox（模板/重复系列）保留原生滚动条，按主题深浅切换系统滚动条外观。</summary>
    private void ApplyListScrollTheme()
    {
        var dark = UiTheme.Mode == AppThemeMode.Dark;
        if (templateList.IsHandleCreated)
        {
            NativeMethods.ApplyScrollBarTheme(templateList.Handle, dark);
        }

        if (recurringList.IsHandleCreated)
        {
            NativeMethods.ApplyScrollBarTheme(recurringList.Handle, dark);
        }
    }

    private static void RemapThemeColors(Control root, Dictionary<int, Color> map)
    {
        if (map.TryGetValue(root.BackColor.ToArgb(), out var back))
        {
            root.BackColor = back;
        }

        if (map.TryGetValue(root.ForeColor.ToArgb(), out var fore))
        {
            root.ForeColor = fore;
        }

        foreach (Control child in root.Controls)
        {
            RemapThemeColors(child, map);
        }

        root.Invalidate();
    }

    private void SelectTheme(AppThemeMode mode)
    {
        if (state.Settings.Theme == mode && UiTheme.Mode == mode)
        {
            return;
        }

        var previous = state.Settings.Theme;
        state.Settings.Theme = mode;
        if (!SaveState())
        {
            state.Settings.Theme = previous;
            return;
        }

        SwitchPalette(mode);
        suppressSettingsEvents = true;
        themeChoice.SelectedTheme = mode;
        suppressSettingsEvents = false;
    }

    private static Image? LoadThemeImage(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var source = Image.FromStream(stream);
            return new Bitmap(source);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private void ApplyThemeArtwork()
    {
        var visible = UiTheme.Mode == AppThemeMode.Xuanling && xuanlingAvatar is not null;
        themeCharacter.CharacterImage = visible ? xuanlingAvatar : null;
        themeCharacter.Visible = visible;
        var scene = visible ? xuanlingScene ?? xuanlingAvatar : null;
        todayEmpty.CharacterImage = scene;
        historyEmpty.CharacterImage = scene;
        taskList.ThemeWatermark = scene;
        themeCharacter.Invalidate();
        todayEmpty.Invalidate();
        historyEmpty.Invalidate();
        taskList.Invalidate();
    }

    private void BuildWindow()
    {
        AutoScaleMode = AutoScaleMode.None;
        Font = UiTheme.BodyFont;
        Text = "Daily Goal";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = UiTheme.WindowTop;
        targetWidth = state.Settings.AutoHideEnabled ? CollapsedWidth : ExpandedWidth;
        slideUsesPosition = CanSlideOffscreen();
        var initialWindowWidth = slideUsesPosition ? ExpandedWidth : targetWidth;
        Size = new Size(initialWindowWidth, appHeight);
        Location = new Point(
            slideUsesPosition ? SlidingDockedLeft(targetWidth) : DockedLeft(initialWindowWidth),
            WindowTop);
        MinimumSize = new Size(CollapsedWidth, 360);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        if (trayEnabled)
        {
            BuildTrayIcon();
        }

        content.Size = new Size(ExpandedWidth, appHeight);
        content.Location = Point.Empty;
        content.BackColor = UiTheme.WindowTop;
        content.Visible = targetWidth == ExpandedWidth;
        Controls.Add(content);

        UpdatePeekRailPlacement();
        peek.BackColor = UiTheme.WindowTop;
        peek.Visible = targetWidth == CollapsedWidth;
        peek.PointRight = state.Settings.DockSide == DockSide.Left;
        peek.RailDragStarted += PeekRailOnDragStarted;
        peek.RailDragMoved += PeekRailOnDragMoved;
        peek.RailDragEnded += PeekRailOnDragEnded;
        Controls.Add(peek);
        if (peek.Visible)
        {
            peek.BringToFront();
        }

        Resize += (_, _) => UpdateWindowRegion();
        LocationChanged += (_, _) => SyncParticleLayerPosition();
        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                SyncParticleLayerPosition();
            }
            else
            {
                particleLayer.Hide();
            }
        };
        UpdateWindowRegion();
    }

    private void PeekRailOnDragStarted(object? sender, Point screenLocation)
    {
        railDragging = true;
        windowDragSource = null;
        BeginWindowDrag(screenLocation);
    }

    private void PeekRailOnDragMoved(object? sender, Point screenLocation)
    {
        if (!railDragging)
        {
            return;
        }

        UpdateWindowDrag(screenLocation);
    }

    private void PeekRailOnDragEnded(object? sender, EventArgs e)
    {
        railDragging = false;
        peekHoverStart = null;
        EndWindowDrag();
    }

    private void BuildTrayIcon()
    {
        trayMenu = new ContextMenuStrip
        {
            Font = UiTheme.BodyFont
        };
        UiTheme.StyleMenu(trayMenu);
        var quickAddItem = new ToolStripMenuItem("快速添加任务");
        quickAddItem.Click += (_, _) => ShowFromTray(true);
        trayRemainingItem = new ToolStripMenuItem("今日剩余 0 项") { Enabled = false };
        var showItem = new ToolStripMenuItem("显示窗口");
        showItem.Click += (_, _) => ShowFromTray();
        var exitItem = new ToolStripMenuItem("退出 Daily Goal");
        exitItem.Click += (_, _) => Close();
        trayMenu.Items.Add(quickAddItem);
        trayMenu.Items.Add(trayRemainingItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(showItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(exitItem);

        trayIcon = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Application,
            Text = "Daily Goal",
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                ShowFromTray();
            }
        };
        trayIcon.BalloonTipClicked += (_, _) => ShowFromTray(true);
    }

    private void ShowFromTray(bool focusInput = false)
    {
        if (!Visible)
        {
            Show();
        }

        WindowState = FormWindowState.Normal;
        lastInside = DateTime.Now.AddMilliseconds(1800);
        if (focusInput)
        {
            if (settingsVisible)
            {
                HideSettings();
            }
            if (templateView.Visible)
            {
                HideTemplateManager();
            }
            if (recurringView.Visible)
            {
                HideRecurringManager();
            }
            ShowMainView(MainView.Today);
        }
        SetExpandedState(true);
        BringToFront();
        Activate();
        if (focusInput)
        {
            BeginInvoke(() =>
            {
                taskInput.Focus();
                taskInput.Select(taskInput.TextLength, 0);
            });
        }
    }

    internal void HandleExternalActivation()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(HandleExternalActivation);
            return;
        }

        ShowFromTray(true);
    }

    internal void HandleTaskbarCreated()
    {
        if (trayIcon is null)
        {
            return;
        }

        trayIcon.Visible = false;
        trayIcon.Visible = true;
    }

    internal static (uint Modifiers, uint Key) HotkeyFor(HotkeyChoice choice)
    {
        return choice switch
        {
            HotkeyChoice.CtrlAltSpace => (NativeMethods.ModControl | NativeMethods.ModAlt, 0x20u),
            HotkeyChoice.CtrlShiftD => (NativeMethods.ModControl | NativeMethods.ModShift, 0x44u),
            _ => (NativeMethods.ModControl | NativeMethods.ModAlt, 0x44u)
        };
    }

    internal static string HotkeyText(HotkeyChoice choice)
    {
        return choice switch
        {
            HotkeyChoice.CtrlAltSpace => "Ctrl+Alt+空格",
            HotkeyChoice.CtrlShiftD => "Ctrl+Shift+D",
            _ => "Ctrl+Alt+D"
        };
    }

    /// <summary>按当前设置注册/注销全局快捷键；测试模式（无托盘）不触碰系统级热键。</summary>
    private void ApplyHotkeyRegistration()
    {
        if (!trayEnabled || !IsHandleCreated)
        {
            return;
        }

        if (hotkeyRegistered)
        {
            _ = NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            hotkeyRegistered = false;
        }

        if (!state.Settings.HotkeyEnabled)
        {
            return;
        }

        var (modifiers, key) = HotkeyFor(state.Settings.Hotkey);
        hotkeyRegistered = NativeMethods.RegisterHotKey(Handle, HotkeyId, modifiers | NativeMethods.ModNoRepeat, key);
        if (!hotkeyRegistered)
        {
            ShowTransientStatus($"快捷键 {HotkeyText(state.Settings.Hotkey)} 注册失败，可能已被其他程序占用", true);
        }
    }

    /// <summary>提醒条件（纯判断，便于自检）：已启用、到点、今天还没提醒过、且今天还有待办。</summary>
    internal bool ShouldRemindNow(DateTimeOffset now)
    {
        return state.Settings.ReminderEnabled
            && now.LocalDateTime.Hour >= state.Settings.ReminderHour
            && state.LastReminderDate != currentDate
            && state.TasksForDate(currentDate).Any();
    }

    private void CheckEveningReminder()
    {
        if (trayIcon is null || !ShouldRemindNow(clock.Now))
        {
            return;
        }

        var remaining = state.TasksForDate(currentDate).Count();
        state.LastReminderDate = currentDate;
        _ = stateStore.Save(state, out _);
        trayIcon.BalloonTipTitle = "Daily Goal";
        trayIcon.BalloonTipText = $"今天还剩 {remaining} 项任务，回来看看吧";
        trayIcon.ShowBalloonTip(8000);
    }

    private string ExecutablePath => Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "DailyGoal.exe");

    private void ReconcileStartupRegistration()
    {
        if (!trayEnabled)
        {
            return;
        }

        var desired = state.Settings.StartWithWindows;
        if (startupRegistration.SetEnabled(desired, ExecutablePath, out var error))
        {
            return;
        }

        AppendStorageWarning(error);
        if (!desired)
        {
            return;
        }

        state.Settings.StartWithWindows = false;
        if (!stateStore.Save(state, out var saveError))
        {
            AppendStorageWarning(saveError);
        }
    }

    private void StartupToggleOnChanged(object? sender, EventArgs e)
    {
        if (suppressSettingsEvents)
        {
            return;
        }

        var desired = startupToggle.Checked;
        var previous = state.Settings.StartWithWindows;
        if (!startupRegistration.SetEnabled(desired, ExecutablePath, out var error))
        {
            suppressSettingsEvents = true;
            startupToggle.Checked = previous;
            suppressSettingsEvents = false;
            ShowStorageWarning(error ?? "无法更新开机启动。");
            return;
        }

        state.Settings.StartWithWindows = desired;
        if (SaveState())
        {
            return;
        }

        _ = startupRegistration.SetEnabled(previous, ExecutablePath, out _);
        state.Settings.StartWithWindows = previous;
        ApplySettingsToControls();
    }

    private void ExportBackup()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "导出 Daily Goal 备份",
            Filter = "Daily Goal 备份 (*.dailygoal.json)|*.dailygoal.json|JSON 文件 (*.json)|*.json",
            DefaultExt = "dailygoal.json",
            AddExtension = true,
            RestoreDirectory = true,
            FileName = $"daily-goal-{DateTime.Now:yyyyMMdd-HHmm}.dailygoal.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!backupService.ExportPortable(state, dialog.FileName, out var error))
        {
            ShowStorageWarning(error ?? "备份导出失败。");
            return;
        }

        ShowTransientStatus("任务备份已导出");
    }

    private void ImportBackup()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入 Daily Goal 备份",
            Filter = "Daily Goal 备份 (*.dailygoal.json;*.json)|*.dailygoal.json;*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var import = backupService.ReadPortable(dialog.FileName, currentDate);
        if (!import.Success)
        {
            ShowStorageWarning(import.Error ?? "备份文件无效。");
            return;
        }

        var activeCount = import.Tasks.Count(task => task.IsActive);
        var historyCount = import.Tasks.Count - activeCount;
        var confirmed = suppressDialogs || AppDialog.Confirm(
            this,
            "导入 Daily Goal 备份",
            $"导入将替换当前计划数据。\n\n待办：{activeCount} 项\n历史：{historyCount} 项\n模板：{import.Templates.Count} 项\n重复系列：{import.RecurringTasks.Count} 项\n\n当前设备设置会保留。",
            "导入并替换",
            danger: true);
        if (!confirmed)
        {
            return;
        }

        var snapshot = state.Clone();
        if (!backupService.CreatePreImportBackup(state, out _, out var backupError))
        {
            ShowStorageWarning(backupError ?? "无法创建导入前保护副本，已取消导入。");
            return;
        }

        state.Tasks = import.Tasks.Select(task => task.Clone()).ToList();
        state.Templates = import.Templates.Select(template => template.Clone()).ToList();
        state.RecurringTasks = import.RecurringTasks.Select(recurring => recurring.Clone()).ToList();
        state.Settings = snapshot.Settings.Clone();
        state.LastOpenedDate = currentDate;
        state.Normalize(currentDate);
        state.ApplyDailyRollover(currentDate);
        recurrenceService.EnsureTasksForDate(state, currentDate, clock.Now);
        state.PruneHistory(currentDate);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        HideUndoBar();
        RenderAll();
        ShowTransientStatus("任务与历史记录已导入");
    }

    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DataDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException)
        {
            ShowTransientStatus("无法打开数据文件夹", true);
        }
    }

    private void ChangeDataDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择新的数据文件夹（放入云盘同步目录即可多机同步）",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var target = dialog.SelectedPath;
        if (string.Equals(Path.GetFullPath(target), Path.GetFullPath(DataDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var targetFile = Path.Combine(target, "tasks.json");
        if (File.Exists(targetFile))
        {
            if (!AppDialog.Confirm(
                    this,
                    "使用已有数据",
                    "所选文件夹已包含 Daily Goal 数据。重启后将改用该文件夹中的数据，当前数据保持原位不变。",
                    "使用该数据"))
            {
                return;
            }
        }
        else if (!TryCopyDataTo(target, out var copyError))
        {
            ShowStorageWarning(copyError ?? "迁移数据失败，未更改数据位置。");
            return;
        }

        if (!StateStore.TrySetCustomDataDirectory(target, out var pointerError))
        {
            ShowStorageWarning(pointerError ?? "无法更新数据位置。");
            return;
        }

        UpdateDataPathControls();
        PromptRestart("数据位置已更新，重启应用后生效。");
    }

    private bool TryCopyDataTo(string target, out string? error)
    {
        error = null;
        try
        {
            Directory.CreateDirectory(target);
            if (File.Exists(stateStore.DataFile))
            {
                File.Copy(stateStore.DataFile, Path.Combine(target, "tasks.json"), false);
            }

            var backupFile = $"{stateStore.DataFile}.bak";
            if (File.Exists(backupFile))
            {
                File.Copy(backupFile, Path.Combine(target, "tasks.json.bak"), true);
            }

            var backupsSource = Path.Combine(DataDirectory, "backups");
            if (Directory.Exists(backupsSource))
            {
                var backupsTarget = Path.Combine(target, "backups");
                Directory.CreateDirectory(backupsTarget);
                foreach (var file in Directory.GetFiles(backupsSource, "*.json"))
                {
                    File.Copy(file, Path.Combine(backupsTarget, Path.GetFileName(file)), true);
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            error = $"迁移数据失败：{exception.Message}";
            return false;
        }
    }

    private void ResetDataDirectory()
    {
        if (!AppDialog.Confirm(
                this,
                "恢复默认数据位置",
                $"重启后将改回默认位置：\n{StateStore.DefaultRootDirectory}\n\n当前文件夹中的数据不会被删除。",
                "恢复默认"))
        {
            return;
        }

        if (!StateStore.TrySetCustomDataDirectory(null, out var error))
        {
            ShowStorageWarning(error ?? "无法更新数据位置。");
            return;
        }

        PromptRestart("已恢复默认数据位置，重启应用后生效。");
    }

    private void PromptRestart(string message)
    {
        if (suppressDialogs)
        {
            return;
        }

        if (AppDialog.Choose(this, "需要重启", message, ("立即重启", DialogTone.Primary), ("稍后", DialogTone.Neutral)) == 0)
        {
            RestartRequested = true;
            Close();
        }
    }

    private void BuildContent()
    {
        // 标题区是展开态的窗口拖动面：按住任意空白处即可拖动整窗，松手后自动吸附到最近的屏幕边。
        dragGrip.Location = new Point(UiTheme.S(11), UiTheme.S(27));
        dragGrip.Size = new Size(UiTheme.S(14), UiTheme.S(26));
        dragGrip.AccessibleName = "拖动窗口";
        content.Controls.Add(dragGrip);
        toolTip.SetToolTip(dragGrip, "按住拖动窗口，松手后自动吸附到最近的屏幕边");

        titleLabel.Text = "今日计划";
        titleLabel.Location = new Point(UiTheme.S(32), UiTheme.S(20));
        titleLabel.Size = new Size(UiTheme.S(350), UiTheme.S(40));
        titleLabel.Font = UiTheme.TitleFont;
        titleLabel.ForeColor = UiTheme.Text;
        titleLabel.BackColor = UiTheme.WindowTop;
        content.Controls.Add(titleLabel);

        dateLabel.Location = new Point(UiTheme.S(33), UiTheme.S(59));
        dateLabel.Size = new Size(UiTheme.S(340), UiTheme.S(26));
        dateLabel.Font = UiTheme.SmallFont;
        dateLabel.ForeColor = UiTheme.TextMuted;
        dateLabel.BackColor = UiTheme.WindowTop;
        content.Controls.Add(dateLabel);

        progressRing.Location = new Point(ExpandedWidth - UiTheme.S(170), UiTheme.S(18));
        progressRing.Size = new Size(UiTheme.S(60), UiTheme.S(60));
        content.Controls.Add(progressRing);

        themeCharacter.Location = new Point(UiTheme.S(382), UiTheme.S(82));
        themeCharacter.Size = new Size(UiTheme.S(110), UiTheme.S(61));
        themeCharacter.BackColor = UiTheme.WindowTop;
        themeCharacter.AccessibleName = "玄翎角色插画";
        themeCharacter.Visible = false;
        content.Controls.Add(themeCharacter);

        AttachWindowDrag(dragGrip);
        AttachWindowDrag(titleLabel);
        AttachWindowDrag(dateLabel);
        AttachWindowDrag(progressRing);
        AttachWindowDrag(themeCharacter);
        // 容器本身不换游标：Cursor 会被子控件继承，任务行等内部控件要保持自己的光标。
        AttachWindowDrag(content, showDragCursor: false);

        ConfigureIconButton(settingsButton, IconKind.Settings, "设置", new Point(ExpandedWidth - UiTheme.S(94), UiTheme.S(24)), new Size(UiTheme.S(40), UiTheme.S(40)));
        settingsButton.Click += (_, _) => ShowSettings();
        content.Controls.Add(settingsButton);

        ConfigureIconButton(moreButton, IconKind.More, "更多操作", new Point(ExpandedWidth - UiTheme.S(68), UiTheme.S(98)), new Size(UiTheme.S(40), UiTheme.S(40)));
        moreButton.Click += (_, _) => moreMenu.Show(moreButton, new Point(0, moreButton.Height + 2));
        content.Controls.Add(moreButton);

        ConfigureIconButton(closeButton, IconKind.Close, "关闭", new Point(ExpandedWidth - UiTheme.S(48), UiTheme.S(24)), new Size(UiTheme.S(40), UiTheme.S(40)));
        closeButton.Tone = IconButtonTone.Danger;
        closeButton.Click += (_, _) => Close();
        content.Controls.Add(closeButton);

        todayTab.Text = "今日";
        todayTab.Location = new Point(UiTheme.S(32), UiTheme.S(94));
        todayTab.Size = new Size(UiTheme.S(102), UiTheme.S(44));
        todayTab.Active = true;
        todayTab.Click += (_, _) => ShowMainView(MainView.Today);
        content.Controls.Add(todayTab);

        tomorrowTab.Text = "明日";
        tomorrowTab.Location = new Point(UiTheme.S(140), UiTheme.S(94));
        tomorrowTab.Size = new Size(UiTheme.S(102), UiTheme.S(44));
        tomorrowTab.Click += (_, _) => ShowMainView(MainView.Tomorrow);
        content.Controls.Add(tomorrowTab);

        historyTab.Text = "记录";
        historyTab.Location = new Point(UiTheme.S(248), UiTheme.S(94));
        historyTab.Size = new Size(UiTheme.S(102), UiTheme.S(44));
        historyTab.Click += (_, _) => ShowMainView(MainView.History);
        content.Controls.Add(historyTab);

        BuildTodayView();
        BuildHistoryView();
        BuildSettingsView();
        BuildTemplateView();
        BuildRecurringView();
        BuildUndoBar();
        BuildMoreMenu();
        UpdateDateText();
    }


    private void BuildUndoBar()
    {
        undoBar.Location = new Point((ExpandedWidth - UiTheme.S(400)) / 2, appHeight - UiTheme.S(80));
        undoBar.Size = new Size(UiTheme.S(400), UiTheme.S(56));
        undoBar.Visible = false;
        undoBar.UndoClicked += (_, _) => UndoLast();
        content.Controls.Add(undoBar);
        undoBar.BringToFront();

        statusToast.Location = new Point(UiTheme.S(70), appHeight - UiTheme.S(76));
        statusToast.Size = new Size(ExpandedWidth - UiTheme.S(140), UiTheme.S(50));
        statusToast.Visible = false;
        content.Controls.Add(statusToast);
    }

    private void BuildMoreMenu()
    {
        var clearToday = new ToolStripMenuItem("清空今日任务");
        clearToday.Click += (_, _) => ClearTodayTasks();
        var clearHistory = new ToolStripMenuItem("清空 30 天记录");
        clearHistory.Click += (_, _) => ClearHistory();
        var recurringManager = new ToolStripMenuItem("重复任务管理");
        recurringManager.Click += (_, _) => ShowRecurringManager(null);
        var themeItem = new ToolStripMenuItem("主题");
        var darkThemeItem = new ToolStripMenuItem("深色");
        var lightThemeItem = new ToolStripMenuItem("浅色");
        var xuanlingThemeItem = new ToolStripMenuItem("玄翎");
        darkThemeItem.Click += (_, _) => SelectTheme(AppThemeMode.Dark);
        lightThemeItem.Click += (_, _) => SelectTheme(AppThemeMode.Light);
        xuanlingThemeItem.Click += (_, _) => SelectTheme(AppThemeMode.Xuanling);
        themeItem.DropDownItems.AddRange([darkThemeItem, lightThemeItem, xuanlingThemeItem]);
        moreMenu.Items.Add(themeItem);
        moreMenu.Items.Add(recurringManager);
        moreMenu.Items.Add(new ToolStripSeparator());
        moreMenu.Items.Add(clearToday);
        moreMenu.Items.Add(clearHistory);
        moreMenu.Font = UiTheme.BodyFont;
        UiTheme.StyleMenu(moreMenu);
        moreMenu.Opening += (_, _) =>
        {
            clearToday.Text = currentView == MainView.Tomorrow ? "清空明日计划" : "清空今日任务";
            darkThemeItem.Checked = UiTheme.Mode == AppThemeMode.Dark;
            lightThemeItem.Checked = UiTheme.Mode == AppThemeMode.Light;
            xuanlingThemeItem.Checked = UiTheme.Mode == AppThemeMode.Xuanling;
        };
    }

    private static Label CreateLabel(string text, Point location, Size size, Font font, Color color)
    {
        return new Label
        {
            Text = text,
            Location = location,
            Size = size,
            Font = font,
            ForeColor = color,
            BackColor = UiTheme.WindowTop
        };
    }

    private void ConfigureIconButton(IconButton button, IconKind icon, string tooltip, Point location, Size size)
    {
        button.Icon = icon;
        button.Location = location;
        button.Size = size;
        button.BackColor = UiTheme.WindowTop;
        button.AccessibleName = tooltip;
        toolTip.SetToolTip(button, tooltip);
    }

    private void WireTimers()
    {
        slideTimer.Interval = 15;
        slideTimer.Tick += (_, _) => TickSlide();

        hoverTimer.Interval = 120;
        hoverTimer.Tick += (_, _) => TickHover();
        hoverTimer.Start();

        particleTimer.Interval = 10;
        particleTimer.Tick += (_, _) => TickParticles();

        undoTimer.Interval = UndoDurationMs;
        undoTimer.Tick += (_, _) => HideUndoBar();

        dateTimer.Interval = 30_000;
        dateTimer.Tick += (_, _) =>
        {
            CheckDateRollover();
            CheckEveningReminder();
        };
        dateTimer.Start();

        statusTimer.Interval = 3200;
        statusTimer.Tick += (_, _) =>
        {
            statusTimer.Stop();
            statusToast.Visible = false;
        };

        FormClosing += (_, _) => CancelRowAnimationTimers();
    }

    private void RenderAll()
    {
        RenderScheduledTasks();
        RenderHistory();
        UpdateHeaderStats();
    }

    private void ShowMainView(MainView view)
    {
        if (settingsVisible)
        {
            return;
        }

        currentView = view;
        todayTab.Active = view == MainView.Today;
        tomorrowTab.Active = view == MainView.Tomorrow;
        historyTab.Active = view == MainView.History;
        todayView.Visible = view is MainView.Today or MainView.Tomorrow;
        historyView.Visible = view == MainView.History;
        titleLabel.Text = view switch
        {
            MainView.Today => "今日计划",
            MainView.Tomorrow => "明日计划",
            _ => "完成记录"
        };
        if (view is MainView.Today or MainView.Tomorrow)
        {
            RenderScheduledTasks();
            taskInput.PlaceholderText = view == MainView.Today ? "写下一件想完成的事" : "提前安排明天的事";
        }
        UpdateDateText();
        if (view == MainView.History)
        {
            RenderHistory();
        }
    }

    private void ShowSettings()
    {
        settingsVisible = true;
        ApplySettingsToControls();
        SetMainChromeVisible(false);
        settingsView.Visible = true;
        settingsView.BringToFront();
        HideUndoBar();
        SetExpandedState(true);
    }

    private void HideSettings()
    {
        settingsVisible = false;
        settingsView.Visible = false;
        SetMainChromeVisible(true);
        undoBar.BringToFront();
    }

    private void SetMainChromeVisible(bool visible)
    {
        titleLabel.Visible = visible;
        dateLabel.Visible = visible;
        dragGrip.Visible = visible;
        progressRing.Visible = visible;
        themeCharacter.Visible = visible && UiTheme.Mode == AppThemeMode.Xuanling && xuanlingAvatar is not null;
        settingsButton.Visible = visible;
        moreButton.Visible = visible;
        closeButton.Visible = visible;
        todayTab.Visible = visible;
        tomorrowTab.Visible = visible;
        historyTab.Visible = visible;
        todayView.Visible = visible && currentView is MainView.Today or MainView.Tomorrow;
        historyView.Visible = visible && currentView == MainView.History;
    }

    private void ApplySettingsToControls()
    {
        UiTheme.ReduceMotion = state.Settings.ReducedMotion;
        suppressSettingsEvents = true;
        autoHideToggle.Checked = state.Settings.AutoHideEnabled;
        hideDelaySlider.Value = state.Settings.AutoHideDelayMs;
        hideDelayValue.Text = $"{hideDelaySlider.Value / 1000d:0.##}s";
        particleChoice.Selected = state.Settings.ParticleIntensity;
        reducedMotionToggle.Checked = state.Settings.ReducedMotion;
        startupToggle.Checked = state.Settings.StartWithWindows;
        hideDelaySlider.Enabled = state.Settings.AutoHideEnabled;
        themeChoice.SelectedTheme = state.Settings.Theme;
        dockChips.SelectedIndex = (int)state.Settings.DockSide;
        if (screenChips is not null)
        {
            screenChips.SelectedIndex = ResolvePreferredScreenIndex();
        }
        var scaleIndex = Array.IndexOf(AppSettings.UiScaleChoices, state.Settings.UiScalePercent);
        scaleChips.SelectedIndex = scaleIndex >= 0 ? scaleIndex : 0;
        hotkeyToggle.Checked = state.Settings.HotkeyEnabled;
        hotkeyChips.SelectedIndex = (int)state.Settings.Hotkey;
        hotkeyChips.Enabled = state.Settings.HotkeyEnabled;
        reminderToggle.Checked = state.Settings.ReminderEnabled;
        reminderChips.SelectedIndex = Math.Clamp(state.Settings.ReminderHour - 17, 0, 4);
        reminderChips.Enabled = state.Settings.ReminderEnabled;
        UpdateDataPathControls();
        suppressSettingsEvents = false;
    }

    private int ResolvePreferredScreenIndex()
    {
        if (availableScreens.Length == 0)
        {
            return 0;
        }

        var preferred = state.Settings.PreferredScreenDevice;
        if (string.IsNullOrWhiteSpace(preferred))
        {
            var primaryIndex = Array.FindIndex(availableScreens, screen => screen.Primary);
            return Math.Max(0, primaryIndex);
        }

        var index = Array.FindIndex(
            availableScreens,
            screen => string.Equals(screen.DeviceName, preferred, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            return index;
        }

        var fallback = Array.FindIndex(availableScreens, screen => screen.Primary);
        return Math.Max(0, fallback);
    }

    private void UpdateDataPathControls()
    {
        dataPathLabel.Text = DataDirectory;
        bool isCustom;
        try
        {
            isCustom = !string.Equals(
                Path.GetFullPath(DataDirectory),
                Path.GetFullPath(StateStore.DefaultRootDirectory),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or System.Security.SecurityException or ArgumentException)
        {
            isCustom = false;
        }

        resetDataFolderButton.Visible = isCustom;
    }

    private void SaveSettingsFromControls()
    {
        if (suppressSettingsEvents)
        {
            return;
        }

        var previous = state.Settings.Clone();
        state.Settings.AutoHideEnabled = autoHideToggle.Checked;
        state.Settings.AutoHideDelayMs = hideDelaySlider.Value;
        state.Settings.ParticleIntensity = particleChoice.Selected;
        state.Settings.ReducedMotion = reducedMotionToggle.Checked;
        state.Settings.StartWithWindows = startupToggle.Checked;
        state.Settings.DockSide = (DockSide)dockChips.SelectedIndex;
        if (screenChips is not null && availableScreens.Length > 0)
        {
            var chosen = availableScreens[Math.Clamp(screenChips.SelectedIndex, 0, availableScreens.Length - 1)];
            state.Settings.PreferredScreenDevice = chosen.Primary ? null : chosen.DeviceName;
        }
        state.Settings.HotkeyEnabled = hotkeyToggle.Checked;
        state.Settings.Hotkey = (HotkeyChoice)hotkeyChips.SelectedIndex;
        state.Settings.ReminderEnabled = reminderToggle.Checked;
        state.Settings.ReminderHour = 17 + Math.Clamp(reminderChips.SelectedIndex, 0, 4);
        UiTheme.ReduceMotion = state.Settings.ReducedMotion;
        hideDelaySlider.Enabled = state.Settings.AutoHideEnabled;
        hotkeyChips.Enabled = state.Settings.HotkeyEnabled;
        reminderChips.Enabled = state.Settings.ReminderEnabled;

        if (!SaveState())
        {
            state.Settings = previous;
            ApplySettingsToControls();
            return;
        }

        if (previous.HotkeyEnabled != state.Settings.HotkeyEnabled || previous.Hotkey != state.Settings.Hotkey)
        {
            ApplyHotkeyRegistration();
        }

        if (previous.DockSide != state.Settings.DockSide
            || !string.Equals(previous.PreferredScreenDevice, state.Settings.PreferredScreenDevice, StringComparison.OrdinalIgnoreCase))
        {
            ApplyDockPlacement();
        }

        if (!state.Settings.AutoHideEnabled)
        {
            SetExpandedState(true);
        }

        if (state.Settings.ReducedMotion)
        {
            particles.Clear();
            particleTimer.Stop();
            particleClock.Stop();
            lastParticleElapsedMs = 0;
            particleLayer.Hide();
        }
    }

    /// <summary>停靠边/显示器变化后立即重新贴边（窗口高度按新屏幕重算需重启，此处只调位置）。</summary>
    private void ApplyDockPlacement()
    {
        workArea = ResolveWorkArea();
        slideTimer.Stop();
        slideUsesPosition = CanSlideOffscreen();
        peek.PointRight = state.Settings.DockSide == DockSide.Left;
        UpdatePeekRailPlacement();
        Width = slideUsesPosition ? ExpandedWidth : targetWidth;
        Top = WindowTop;
        Left = slideUsesPosition ? SlidingDockedLeft(targetWidth) : DockedLeft(Width);
        content.Visible = targetWidth == ExpandedWidth;
        peek.Visible = targetWidth == CollapsedWidth;
        if (peek.Visible)
        {
            peek.BringToFront();
        }
        peek.Invalidate();
        SyncParticleLayerPosition();
    }

    private void ScaleChipsOnChanged()
    {
        if (suppressSettingsEvents)
        {
            return;
        }

        var percent = AppSettings.UiScaleChoices[Math.Clamp(scaleChips.SelectedIndex, 0, AppSettings.UiScaleChoices.Length - 1)];
        if (percent == state.Settings.UiScalePercent)
        {
            return;
        }

        var previous = state.Settings.UiScalePercent;
        state.Settings.UiScalePercent = percent;
        if (!SaveState())
        {
            state.Settings.UiScalePercent = previous;
            ApplySettingsToControls();
            return;
        }

        PromptRestart("界面大小将在重启应用后生效。");
    }

    private void UpdateDateText()
    {
        var date = currentView == MainView.Tomorrow ? currentDate.AddDays(1) : currentDate;
        dateLabel.Text = date.ToDateTime(TimeOnly.MinValue).ToString("M 月 d 日  dddd", new CultureInfo("zh-CN"));
    }

    private void UpdateHeaderStats()
    {
        var active = state.TasksForDate(currentDate).Count();
        var completedToday = state.HistoryTasks.Count(task =>
            task.CompletedAt is not null
            && DateOnly.FromDateTime(task.CompletedAt.Value.LocalDateTime) == currentDate);
        var total = active + completedToday;
        progressRing.Completed = completedToday;
        progressRing.Total = total;
        if (currentView == MainView.Tomorrow)
        {
            var tomorrowCount = state.TasksForDate(currentDate.AddDays(1)).Count();
            remainingLabel.Text = tomorrowCount == 0 ? "明天还没有计划" : $"明天计划 {tomorrowCount} 项";
        }
        else
        {
            remainingLabel.Text = active == 0
                ? completedToday > 0 ? "今天已全部完成" : "今天还没有任务"
                : $"{active} 个待办";
        }
        var progress = total == 0 ? 0f : (float)completedToday / total;
        peek.Remaining = active;
        peek.Progress = progress;
        if (trayIcon is not null)
        {
            trayIcon.Text = active > 0
                ? $"Daily Goal - {active} 个待办"
                : "Daily Goal - 今日已完成";
        }
        if (trayRemainingItem is not null)
        {
            trayRemainingItem.Text = active > 0 ? $"今日剩余 {active} 项" : "今日任务已完成";
        }
    }

    private bool SaveState()
    {
        if (stateStore.Save(state, out var error))
        {
            if (!backupService.EnsureDailySnapshot(state, currentDate, out var backupError) && !backupWarningShown)
            {
                backupWarningShown = true;
                ShowTransientStatus(backupError ?? "今日自动备份创建失败。", true);
            }
            return true;
        }

        ShowTransientStatus(error ?? "任务暂时无法保存。", true);
        return false;
    }

    private void ShowTransientStatus(string message, bool error = false)
    {
        statusTimer.Stop();
        statusToast.Message = message;
        statusToast.IsError = error;
        statusToast.Visible = true;
        statusToast.BringToFront();
        statusTimer.Start();
    }

    private void AppendStorageWarning(string? warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return;
        }

        pendingStorageWarning = string.IsNullOrWhiteSpace(pendingStorageWarning)
            ? warning
            : $"{pendingStorageWarning}{Environment.NewLine}{warning}";
    }

    private void ShowPendingStorageWarning()
    {
        if (string.IsNullOrWhiteSpace(pendingStorageWarning))
        {
            return;
        }

        var warning = pendingStorageWarning;
        pendingStorageWarning = null;
        ShowStorageWarning(warning);
    }

    private void ShowStorageWarning(string message)
    {
        if (suppressDialogs)
        {
            AppendStorageWarning(message);
            return;
        }

        AppDialog.Info(this, "Daily Goal", message);
    }

    /// <summary>破坏性操作确认；测试静默模式下直接放行（与既有 suppressDialogs 行为一致）。</summary>
    private bool ConfirmAction(string title, string message, string confirmText, bool danger = true)
    {
        return suppressDialogs || AppDialog.Confirm(this, title, message, confirmText, danger);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        dwmCornersApplied = NativeMethods.ApplyRoundedCorners(Handle);
        if (dwmCornersApplied)
        {
            Region?.Dispose();
            Region = null;
        }

        ApplyHotkeyRegistration();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (hotkeyRegistered)
        {
            _ = NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            hotkeyRegistered = false;
        }

        base.OnHandleDestroyed(e);
    }

    private void UpdateWindowRegion()
    {
        if (Width <= 0 || Height <= 0 || dwmCornersApplied)
        {
            return;
        }

        using var path = PaintHelpers.RoundRect(
            new RectangleF(0, 0, Width, Height),
            Width <= CollapsedWidth ? UiTheme.Sf(9) : UiTheme.RadiusLarge);
        Region?.Dispose();
        Region = new Region(path);
    }

    private void SyncParticleLayerPosition()
    {
        if (particleLayer.Visible)
        {
            particleLayer.RepositionFor(this);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesDisposed)
        {
            resourcesDisposed = true;
            CancelRowAnimationTimers();
            if (particleTimerBoosted)
            {
                _ = NativeMethods.TimeEndPeriod(1);
                particleTimerBoosted = false;
            }
            slideTimer.Dispose();
            hoverTimer.Dispose();
            particleTimer.Dispose();
            undoTimer.Dispose();
            dateTimer.Dispose();
            statusTimer.Dispose();
            particleClock.Stop();
            taskDragGhost?.Dispose();
            particleLayer.Dispose();
            if (trayIcon is not null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            trayMenu?.Dispose();
            trayMenu = null;
            toolTip.Dispose();
            moreMenu.Dispose();
            taskMenu.Dispose();
            templateMenu.Dispose();
            xuanlingAvatar?.Dispose();
            xuanlingAvatar = null;
            xuanlingScene?.Dispose();
            xuanlingScene = null;
        }

        base.Dispose(disposing);
    }
}
