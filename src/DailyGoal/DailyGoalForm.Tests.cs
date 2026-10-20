using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;

namespace DailyGoal;

internal sealed partial class DailyGoalForm
{
    public string RunSelfTest()
    {
        var monitorBounds = new Rectangle(0, 0, 1920, 1080);
        var fullscreenGeometry = FullscreenDetector.CoversMonitor(
            new Rectangle(0, 0, 1920, 1080),
            monitorBounds);
        var nearFullscreenGeometry = FullscreenDetector.CoversMonitor(
            new Rectangle(2, 1, 1918, 1080),
            monitorBounds);
        var regularWindowGeometry = FullscreenDetector.CoversMonitor(
            new Rectangle(0, 0, 1920, 1040),
            monitorBounds);
        var fullscreenGeometryValid = fullscreenGeometry
            && nearFullscreenGeometry
            && !regularWindowGeometry;
        var adaptiveScaleValid = Math.Abs(UiScaleResolver.Resolve(0, new Rectangle(0, 0, 800, 560)) - 0.68f) < 0.01f
            && Math.Abs(UiScaleResolver.Resolve(0, new Rectangle(0, 0, 1024, 728)) - 0.833f) < 0.01f
            && Math.Abs(UiScaleResolver.Resolve(0, new Rectangle(0, 0, 1366, 728)) - 0.833f) < 0.01f
            && Math.Abs(UiScaleResolver.Resolve(0, new Rectangle(0, 0, 1920, 1040)) - 1f) < 0.01f
            && Math.Abs(UiScaleResolver.Resolve(150, new Rectangle(0, 0, 1366, 728)) - 0.833f) < 0.01f
            && Math.Abs(UiScaleResolver.Resolve(150, new Rectangle(0, 0, 2560, 1400)) - 1.5f) < 0.01f;

        var hiddenAtStart = targetWidth == CollapsedWidth
            && Width == (slideUsesPosition ? ExpandedWidth : CollapsedWidth)
            && Left == (slideUsesPosition ? SlidingDockedLeft(CollapsedWidth) : DockedLeft(CollapsedWidth));
        var hiddenFromTaskbar = !ShowInTaskbar;

        taskInput.Text = "第一件任务";
        AddTask();
        taskInput.Text = "第二件任务";
        AddTask();
        taskInput.Text = "第三件任务";
        AddTask();
        var added = state.ActiveTasks.Count() == 3 && taskRows.Count == 3;
        var firstSaveSnapshot = File.Exists(Path.Combine(
            DataDirectory,
            "backups",
            $"tasks-{currentDate:yyyyMMdd}.json"));

        var oldRows = taskRows.Values.Cast<Control>().ToArray();
        RenderToday();
        var rowsDisposed = oldRows.All(control => control.IsDisposed);

        var first = state.ActiveTasks.First();
        var editArgs = new TaskTextEditEventArgs("修改后的任务");
        EditTask(first.Id, editArgs);
        var edited = !editArgs.Cancel && first.Text == "修改后的任务";

        var originalOrder = first.SortOrder;
        MoveTask(first.Id, 1);
        var reordered = state.Tasks.First(task => task.Id == first.Id).SortOrder != originalOrder;

        var draggedOrder = state.ActiveTasks.Select(task => task.Id).Reverse().ToArray();
        var dragReordered = ApplyTaskOrder(draggedOrder)
            && state.ActiveTasks.Select(task => task.Id).SequenceEqual(draggedOrder);
        RenderToday();

        var animatedRow = taskList.Rows.First();
        taskList.SetRowIndex(animatedRow, taskList.Rows.Count - 1);
        var motionStarted = taskList.IsAnimating;
        var motionTimeout = Stopwatch.StartNew();
        while (taskList.IsAnimating && motionTimeout.ElapsedMilliseconds < 700)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        var motionCompleted = motionStarted
            && !taskList.IsAnimating
            && taskList.IndexOf(animatedRow) == taskList.Rows.Count - 1;
        taskList.ApplyOrder(state.ActiveTasks.Select(task => task.Id), false);
        taskList.ArrangeRows(true);

        var completingTask = state.ActiveTasks.First();
        var completingRow = taskRows[completingTask.Id];
        CompleteTask(completingTask, completingRow);
        var completed = completingTask.CompletedAt is not null
            && undoEntry is UndoCompletion undoCompletion
            && undoCompletion.TaskId == completingTask.Id;
        UndoLast();
        var undone = completingTask.CompletedAt is null && state.ActiveTasks.Any(task => task.Id == completingTask.Id);

        completingRow = taskRows[completingTask.Id];
        CompleteTask(completingTask, completingRow);
        RestoreHistoryTask(completingTask.Id);
        var restored = completingTask.CompletedAt is null && state.ActiveTasks.First().Id == completingTask.Id;

        completingRow = taskRows[completingTask.Id];
        CompleteTask(completingTask, completingRow);
        DeleteHistoryTask(completingTask.Id);
        HideUndoBar();
        var historyDeleted = state.Tasks.All(task => task.Id != completingTask.Id)
            && new StateStore(DataDirectory).Load(currentDate).State.Tasks.All(task => task.Id != completingTask.Id);

        taskInput.Text = "待删除任务";
        AddTask();
        var deletable = state.ActiveTasks.First(task => task.Text == "待删除任务");
        DeleteTask(deletable.Id);
        var deleteRecorded = state.Tasks.All(task => task.Id != deletable.Id) && undoEntry is UndoDeletion;
        UndoLast();
        var deleteUndone = state.ActiveTasks.Any(task => task.Id == deletable.Id && task.Text == "待删除任务");
        DeleteTask(deletable.Id);
        HideUndoBar();

        taskInput.Text = "分组测试任务";
        AddTask();
        var sectionTask = state.ActiveTasks.First(task => task.Text == "分组测试任务");
        CompleteTask(sectionTask, taskRows[sectionTask.Id]);
        RenderScheduledTasks();
        var completedSectionShown = taskList.FooterCount >= 2;
        RestoreHistoryTask(sectionTask.Id);
        var completedSectionCleared = sectionTask.CompletedAt is null && taskList.FooterCount == 0;
        DeleteTask(sectionTask.Id);
        HideUndoBar();
        var deleteUndoValid = deleteRecorded && deleteUndone;
        var completedSectionValid = completedSectionShown && completedSectionCleared;

        var batchAdded = AddTasksFromLines(["批量一", "批量二", "批量三"]);
        var batchOrder = state.TasksForDate(currentDate).Select(task => task.Text).Take(3).ToArray();
        var batchAddValid = batchAdded == 3 && batchOrder.SequenceEqual(["批量一", "批量二", "批量三"]);
        foreach (var text in new[] { "批量一", "批量二", "批量三" })
        {
            var batchTask = state.ActiveTasks.FirstOrDefault(task => task.Text == text);
            if (batchTask is not null)
            {
                DeleteTask(batchTask.Id);
            }
        }
        HideUndoBar();

        var accentTask = state.ActiveTasks.First();
        SetTaskAccent(accentTask, (accentTask.AccentIndex + 2) % 5);
        var accentPersisted = new StateStore(DataDirectory).Load(currentDate).State.Tasks
            .First(task => task.Id == accentTask.Id).AccentIndex == accentTask.AccentIndex;

        var rolloverState = new AppState
        {
            LastOpenedDate = currentDate.AddDays(-1),
            Tasks =
            [
                new TaskRecord
                {
                    Id = "rollover-task",
                    Text = "跨日任务",
                    Day = currentDate.AddDays(-1),
                    SortOrder = 0,
                    AccentIndex = 0
                }
            ]
        };
        var firstRollover = rolloverState.ApplyDailyRollover(currentDate);
        var secondRollover = rolloverState.ApplyDailyRollover(currentDate);
        var rolloverIdempotent = firstRollover
            && !secondRollover
            && rolloverState.Tasks[0].CarryCount == 1
            && rolloverState.Tasks[0].Day == currentDate;

        rolloverState.Tasks.Add(new TaskRecord
        {
            Id = "old-history",
            Text = "过期记录",
            Day = currentDate.AddDays(-31),
            SortOrder = 1,
            AccentIndex = 1,
            CompletedAt = DateTimeOffset.Now.AddDays(-31)
        });
        rolloverState.Tasks.Add(new TaskRecord
        {
            Id = "recent-history",
            Text = "保留记录",
            Day = currentDate.AddDays(-29),
            SortOrder = 2,
            AccentIndex = 2,
            CompletedAt = DateTimeOffset.Now.AddDays(-29)
        });
        rolloverState.PruneHistory(currentDate);
        var retentionCorrect = rolloverState.Tasks.All(task => task.Id != "old-history")
            && rolloverState.Tasks.Any(task => task.Id == "recent-history");

        var legacyDirectory = Path.Combine(DataDirectory, "legacy");
        Directory.CreateDirectory(legacyDirectory);
        File.WriteAllText(
            Path.Combine(legacyDirectory, "tasks.json"),
            "[{\"Id\":\"legacy-1\",\"Text\":\"旧任务\",\"Completed\":false}]");
        var legacyStore = new StateStore(legacyDirectory);
        var legacyLoad = legacyStore.Load(currentDate);
        var legacyMigrated = legacyLoad.MigratedLegacy
            && legacyLoad.State.ActiveTasks.Single().Id == "legacy-1"
            && Directory.GetFiles(legacyDirectory, "tasks.v1-backup-*.json").Length == 1
            && legacyStore.Save(legacyLoad.State, out _)
            && !File.Exists(Path.Combine(legacyDirectory, "tasks.json.tmp"));

        var recoveryDirectory = Path.Combine(DataDirectory, "recovery");
        Directory.CreateDirectory(recoveryDirectory);
        var recoverableState = new AppState
        {
            LastOpenedDate = currentDate,
            Tasks =
            [
                new TaskRecord
                {
                    Id = "recovered-1",
                    Text = "恢复任务",
                    Day = currentDate,
                    SortOrder = 0,
                    AccentIndex = 3
                }
            ]
        };
        File.WriteAllText(
            Path.Combine(recoveryDirectory, "tasks.json.bak"),
            JsonSerializer.Serialize(recoverableState));
        File.WriteAllText(Path.Combine(recoveryDirectory, "tasks.json"), "{ invalid json");
        var recoveryLoad = new StateStore(recoveryDirectory).Load(currentDate);
        var backupRecovered = recoveryLoad.State.ActiveTasks.Single().Id == "recovered-1"
            && Directory.GetFiles(recoveryDirectory, "tasks.corrupt-*.json").Length == 1;

        state.Settings.AutoHideEnabled = true;
        state.Settings.ReducedMotion = false;
        var slideResizeCount = 0;
        EventHandler resizeCounter = (_, _) => slideResizeCount += 1;
        Resize += resizeCounter;
        SetExpandedState(true);
        var expandTimeout = Stopwatch.StartNew();
        while (slideTimer.Enabled && expandTimeout.ElapsedMilliseconds < 700)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        SetExpandedState(false);
        var collapseTimeout = Stopwatch.StartNew();
        while (slideTimer.Enabled && collapseTimeout.ElapsedMilliseconds < 700)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Resize -= resizeCounter;
        var slideAnimationStable = !slideTimer.Enabled
            && targetWidth == CollapsedWidth
            && Left == (slideUsesPosition ? SlidingDockedLeft(CollapsedWidth) : DockedLeft(CollapsedWidth))
            && (slideUsesPosition
                ? Width == ExpandedWidth && slideResizeCount == 0
                : Width == CollapsedWidth && slideResizeCount > 0);

        var windowDrag = RunWindowDragSelfTest();

        undoBar.Visible = true;
        taskInput.Clear();
        var transientUiDoesNotLock = !InteractionLocked;
        undoBar.Visible = false;

        state.Settings.AutoHideEnabled = false;
        state.Settings.ReducedMotion = true;
        SetExpandedState(false);
        var autoHideSettingHonored = targetWidth == ExpandedWidth && Width == ExpandedWidth;
        state.Settings.AutoHideEnabled = true;
        settingsVisible = true;
        var settingsLockHonored = InteractionLocked;
        settingsVisible = false;

        state.Settings.AutoHideDelayMs = 1500;
        state.Settings.ParticleIntensity = ParticleIntensity.Standard;
        state.Settings.Theme = AppThemeMode.Light;
        state.Settings.HotkeyEnabled = false;
        state.Settings.Hotkey = HotkeyChoice.CtrlShiftD;
        state.Settings.ReminderEnabled = true;
        state.Settings.ReminderHour = 20;
        state.Settings.DockSide = DockSide.Left;
        state.Settings.WindowTopOffset = 64;
        state.Settings.CompletedSectionCollapsed = true;
        state.Settings.UiScalePercent = 125;
        var settingsSaved = SaveState();
        var reloadedSettings = new StateStore(DataDirectory).Load(currentDate).State.Settings;
        var settingsPersisted = settingsSaved
            && reloadedSettings.AutoHideDelayMs == 1500
            && reloadedSettings.ParticleIntensity == ParticleIntensity.Standard
            && reloadedSettings.Theme == AppThemeMode.Light
            && !reloadedSettings.HotkeyEnabled
            && reloadedSettings.Hotkey == HotkeyChoice.CtrlShiftD
            && reloadedSettings.ReminderEnabled
            && reloadedSettings.ReminderHour == 20
            && reloadedSettings.DockSide == DockSide.Left
            && reloadedSettings.WindowTopOffset == 64
            && reloadedSettings.CompletedSectionCollapsed
            && reloadedSettings.UiScalePercent == 125;
        state.Settings.DockSide = DockSide.Right;
        state.Settings.CompletedSectionCollapsed = false;
        state.Settings.ReminderEnabled = false;
        _ = SaveState();

        SelectTheme(AppThemeMode.Xuanling);
        var xuanlingPersisted = new StateStore(DataDirectory).Load(currentDate).State.Settings.Theme == AppThemeMode.Xuanling
            && UiTheme.Mode == AppThemeMode.Xuanling;

        SwitchPalette(AppThemeMode.Light);
        var lightApplied = UiTheme.Mode == AppThemeMode.Light
            && UiTheme.Surface.R > 200
            && content.BackColor == UiTheme.WindowTop
            && taskInput.BackColor == UiTheme.Surface;
        SwitchPalette(AppThemeMode.Xuanling);
        var xuanlingApplied = UiTheme.Mode == AppThemeMode.Xuanling
            && UiTheme.Sky.B > UiTheme.Sky.R
            && UiTheme.Coral.R > UiTheme.Coral.B
            && xuanlingAvatar is not null
            && xuanlingScene is { Width: 512, Height: 512 }
            && themeCharacter.CharacterImage is not null
            && todayEmpty.CharacterImage is not null
            && taskList.ThemeWatermark is not null
            && content.BackColor == UiTheme.WindowTop;
        SwitchPalette(AppThemeMode.Dark);
        SelectTheme(AppThemeMode.Dark);
        var themeSwitched = lightApplied
            && xuanlingApplied
            && xuanlingPersisted
            && UiTheme.Mode == AppThemeMode.Dark
            && UiTheme.Surface.R < 100
            && content.BackColor == UiTheme.WindowTop
            && taskInput.BackColor == UiTheme.Surface;

        var blockedPath = Path.Combine(DataDirectory, "blocked-path");
        File.WriteAllText(blockedPath, "not a directory");
        var failedSaveDetected = !new StateStore(blockedPath).Save(new AppState(), out var failedSaveError)
            && !string.IsNullOrWhiteSpace(failedSaveError);
        var reliability = ReliabilitySelfTests.Run(DataDirectory, currentDate);
        var planning = PlanningSelfTests.Run(currentDate);
        var planningInteractions = RunPlanningInteractionSelfTest();
        var startupRegistration = RunStartupRegistrationSelfTest();
        var reminderValid = RunReminderSelfTest();

        if (!fullscreenGeometryValid || !adaptiveScaleValid || !hiddenAtStart || !hiddenFromTaskbar || !added || !rowsDisposed || !edited || !reordered || !dragReordered || !motionCompleted || !completed || !undone
            || !restored || !historyDeleted || !deleteUndoValid || !completedSectionValid || !batchAddValid || !accentPersisted
            || !rolloverIdempotent || !retentionCorrect || !legacyMigrated || !backupRecovered
            || !firstSaveSnapshot || !startupRegistration || !planningInteractions || !reminderValid
            || !slideAnimationStable || !transientUiDoesNotLock || !windowDrag)
        {
            throw new InvalidOperationException(
                $"Self-test failed: fullscreen={fullscreenGeometryValid}, adaptiveScale={adaptiveScaleValid}, hidden={hiddenAtStart}, taskbarHidden={hiddenFromTaskbar}, added={added}, disposed={rowsDisposed}, "
                + $"edited={edited}, reordered={reordered}, drag={dragReordered}, motion={motionCompleted}, completed={completed}, undone={undone}, "
                + $"restored={restored}, historyDeleted={historyDeleted}, deleteUndo={deleteUndoValid}, completedSection={completedSectionValid}, "
                + $"batchAdd={batchAddValid}, accent={accentPersisted}, rollover={rolloverIdempotent}, retention={retentionCorrect}, "
                + $"legacy={legacyMigrated}, recovery={backupRecovered}, autoHide={autoHideSettingHonored}, "
                + $"slideStable={slideAnimationStable}/{slideResizeCount}, transientUnlock={transientUiDoesNotLock}, windowDrag={windowDrag}, "
                + $"settingsLock={settingsLockHonored}, settingsPersisted={settingsPersisted}, failedSave={failedSaveDetected}, "
                + $"firstSaveSnapshot={firstSaveSnapshot}, startupRegistration={startupRegistration}, planningInteractions={planningInteractions}, "
                + $"reminder={reminderValid}.");
        }

        if (!autoHideSettingHonored || !settingsLockHonored || !settingsPersisted || !failedSaveDetected || !themeSwitched)
        {
            throw new InvalidOperationException(
                $"Settings self-test failed: autoHide={autoHideSettingHonored}, settingsLock={settingsLockHonored}, "
                + $"settingsPersisted={settingsPersisted}, xuanlingPersisted={xuanlingPersisted}, failedSave={failedSaveDetected}, themeSwitched={themeSwitched}.");
        }

        return $"fullscreenGeometry={fullscreenGeometryValid}; adaptiveScale={adaptiveScaleValid}; slideStable={slideAnimationStable}/{slideResizeCount}; transientUnlock={transientUiDoesNotLock}; windowDrag={windowDrag}; active={state.ActiveTasks.Count()}; taskbarHidden={hiddenFromTaskbar}; disposed={rowsDisposed}; edited={edited}; reordered={reordered}; drag={dragReordered}; motion={motionCompleted}; "
            + $"undo={undone}; restored={restored}; historyDeleted={historyDeleted}; deleteUndo={deleteUndoValid}; completedSection={completedSectionValid}; batchAdd={batchAddValid}; "
            + $"accent={accentPersisted}; reminder={reminderValid}; themeSwitched={themeSwitched}; rollover={rolloverIdempotent}; migration={legacyMigrated}; recovery={backupRecovered}; {reliability}; {planning}";
    }

    /// <summary>
    /// 拖动吸附自检：展开态按住标题区、收起态按住贴条都能拖动整窗，
    /// 松手后按可见部分所在的半屏自动识别屏幕边并吸附过去。
    /// </summary>
    private bool RunWindowDragSelfTest()
    {
        var snapshot = state.Settings.Clone();
        var virtualScreen = SystemInformation.VirtualScreen;

        Point PointerForLocation(Point location) => new(
            windowDragPointer.X + (location.X - windowDragOrigin.X),
            windowDragPointer.Y + (location.Y - windowDragOrigin.Y));

        state.Settings.ReducedMotion = true;
        state.Settings.DockSide = DockSide.Right;
        state.Settings.WindowTopOffset = 12;
        state.Settings.PreferredScreenDevice = null;
        ApplyDockPlacement();
        SetExpandedState(true);

        var area = workArea;
        var top = area.Top + UiTheme.S(24);

        // 1. 展开态拖动：窗口跟随指针，拖动中既不会收起也不会被判定为点击。
        var leftHalfLocation = new Point(area.Left + UiTheme.S(24), top);
        BeginWindowDrag(new Point(Left + UiTheme.S(120), Top + UiTheme.S(30)));
        UpdateWindowDrag(PointerForLocation(leftHalfLocation));
        var followedPointer = Location == leftHalfLocation && windowDragMoved;
        var dragLocksInteraction = InteractionLocked;
        lastInside = DateTime.Now.AddSeconds(-5);
        TickHover();
        // 无头自检里窗体没有 Show()，子控件的 Visible 恒为 false，只能看状态与动画是否被改。
        var dragHeldExpanded = targetWidth == ExpandedWidth && !slideTimer.Enabled;
        EndWindowDrag();

        var snappedToLeft = state.Settings.DockSide == DockSide.Left
            && peek.PointRight
            && Left == (slideUsesPosition ? SlidingDockedLeft(targetWidth) : DockedLeft(targetWidth))
            && Top == WindowTop
            && state.Settings.WindowTopOffset == top - workArea.Top
            && state.Settings.PreferredScreenDevice is null;
        var persistedDockSide = new StateStore(DataDirectory).Load(currentDate).State.Settings.DockSide == DockSide.Left;
        statusTimer.Stop();
        statusToast.Visible = false;

        // 2. 拖到右半边：自动吸附回右侧。
        var rightHalfLocation = new Point(area.Right - ExpandedWidth - UiTheme.S(24), top);
        BeginWindowDrag(new Point(Left + UiTheme.S(120), Top + UiTheme.S(30)));
        UpdateWindowDrag(PointerForLocation(rightHalfLocation));
        EndWindowDrag();
        var snappedToRight = state.Settings.DockSide == DockSide.Right
            && !peek.PointRight
            && Left == (slideUsesPosition ? SlidingDockedLeft(targetWidth) : DockedLeft(targetWidth))
            && Top == WindowTop;

        // 3. 只按不拖：窗口不动，停靠边也不变。
        var restingLocation = Location;
        BeginWindowDrag(new Point(Left + UiTheme.S(80), Top + UiTheme.S(20)));
        EndWindowDrag();
        var clickIgnored = Location == restingLocation && state.Settings.DockSide == DockSide.Right;

        // 4. 收起态拖贴条：横向拖到左半边同样自动换边，贴条跟着挪到窗体的另一侧。
        SetExpandedState(false);
        var railLocation = new Point(area.Left + UiTheme.S(24), top);
        BeginWindowDrag(new Point(Left + VisibleBounds().Left + UiTheme.S(8), Top + UiTheme.S(40)));
        UpdateWindowDrag(PointerForLocation(railLocation));
        EndWindowDrag();
        var railSnappedToLeft = state.Settings.DockSide == DockSide.Left
            && targetWidth == CollapsedWidth
            && peek.PointRight
            && Left == (slideUsesPosition ? SlidingDockedLeft(targetWidth) : DockedLeft(targetWidth))
            && Top == WindowTop
            && peek.Bounds == new Rectangle(VisibleBounds().Left, 0, CollapsedWidth, Height);

        // 5. 往屏幕外拖：可见部分被夹在虚拟桌面内，标题区始终抓得回来。
        BeginWindowDrag(new Point(Left + VisibleBounds().Left + UiTheme.S(8), Top + UiTheme.S(20)));
        UpdateWindowDrag(new Point(windowDragPointer.X, virtualScreen.Top - UiTheme.S(4000)));
        var dragClampedVertically = Top >= virtualScreen.Top;
        UpdateWindowDrag(new Point(virtualScreen.Right + UiTheme.S(4000), windowDragPointer.Y));
        var clampedVisible = VisibleBounds();
        var dragClampedHorizontally = Left + clampedVisible.Left + clampedVisible.Width <= virtualScreen.Right;
        EndWindowDrag();

        state.Settings.DockSide = snapshot.DockSide;
        state.Settings.WindowTopOffset = snapshot.WindowTopOffset;
        state.Settings.PreferredScreenDevice = snapshot.PreferredScreenDevice;
        ApplyDockPlacement();
        state.Settings.ReducedMotion = true;
        SetExpandedState(false);
        state.Settings.ReducedMotion = snapshot.ReducedMotion;
        statusTimer.Stop();
        statusToast.Visible = false;

        return followedPointer
            && dragLocksInteraction
            && dragHeldExpanded
            && snappedToLeft
            && persistedDockSide
            && snappedToRight
            && clickIgnored
            && railSnappedToLeft
            && dragClampedVertically
            && dragClampedHorizontally;
    }

    private bool RunReminderSelfTest()
    {
        var directory = Path.Combine(DataDirectory, "reminder");
        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        var noon = new DateTimeOffset(currentDate.ToDateTime(new TimeOnly(12, 0)), offset);
        var evening = new DateTimeOffset(currentDate.ToDateTime(new TimeOnly(19, 5)), offset);
        using var form = new DailyGoalForm(
            directory,
            enableTrayIcon: false,
            suppressDialogsForTests: true,
            clockOverride: new FixedAppClock(noon));
        form.state.Settings.ReminderEnabled = true;
        form.state.Settings.ReminderHour = 19;
        form.AddTaskToDate("提醒测试", currentDate);
        var beforeHour = !form.ShouldRemindNow(noon);
        var atHour = form.ShouldRemindNow(evening);
        form.state.LastReminderDate = currentDate;
        var onlyOnce = !form.ShouldRemindNow(evening);
        form.state.LastReminderDate = null;
        form.state.Settings.ReminderEnabled = false;
        var whenDisabled = !form.ShouldRemindNow(evening);
        form.state.Settings.ReminderEnabled = true;
        form.state.Tasks.Clear();
        var noTasks = !form.ShouldRemindNow(evening);
        return beforeHour && atHour && onlyOnce && whenDisabled && noTasks;
    }

    private bool RunPlanningInteractionSelfTest()
    {
        var directory = Path.Combine(DataDirectory, "planning-interactions");
        var now = new DateTimeOffset(currentDate.ToDateTime(new TimeOnly(9, 0)), TimeZoneInfo.Local.GetUtcOffset(DateTime.Now));
        using var form = new DailyGoalForm(
            directory,
            enableTrayIcon: false,
            suppressDialogsForTests: true,
            clockOverride: new FixedAppClock(now));

        if (!form.AddTaskToDate("每日整理", currentDate))
        {
            return false;
        }
        var first = form.state.TasksForDate(currentDate).Single();
        form.SetRecurrence(first, RecurrenceRule.Daily);
        var edit = new TaskTextEditEventArgs("每日整理工作台");
        form.EditTask(first.Id, edit);
        form.SaveAsTemplate(first);
        form.SaveAsTemplate(first);
        var series = form.state.RecurringTasks.Single();
        var editedSeries = !edit.Cancel && series.Text == "每日整理工作台";
        var templateDeduplicated = form.state.Templates.Count == 1;
        form.DeleteTaskWithRecurrence(first.Id, RecurringDeleteAction.SkipOccurrence);
        var skipped = form.state.Tasks.All(task => task.Id != first.Id) && series.Enabled;

        form.AddTaskToDate("每周复盘", currentDate);
        var second = form.state.TasksForDate(currentDate).Single();
        form.SetRecurrence(second, RecurrenceRule.Weekly);
        var secondSeries = form.state.RecurringTasks.Single(candidate => candidate.Id == second.RecurrenceId);
        form.DeleteTaskWithRecurrence(second.Id, RecurringDeleteAction.StopSeries);
        var stopped = !secondSeries.Enabled && form.state.Tasks.All(task => task.Id != second.Id);
        return editedSeries && templateDeduplicated && skipped && stopped;
    }

    private sealed class FixedAppClock(DateTimeOffset now) : IAppClock
    {
        public DateTimeOffset Now { get; } = now;
        public DateOnly Today => DateOnly.FromDateTime(Now.LocalDateTime);
    }

    private bool RunStartupRegistrationSelfTest()
    {
        var directory = Path.Combine(DataDirectory, "startup-registration");
        var startupState = new AppState
        {
            LastOpenedDate = currentDate,
            Settings = new AppSettings { StartWithWindows = true }
        };
        if (!new StateStore(directory).Save(startupState, out _))
        {
            return false;
        }

        var registration = new FakeStartupRegistration();
        using var form = new DailyGoalForm(
            directory,
            enableTrayIcon: true,
            startupRegistrationOverride: registration,
            suppressDialogsForTests: true);
        var reconciled = registration.Enabled
            && string.Equals(registration.ExecutablePath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
        form.HandleTaskbarCreated();
        var trayRecovered = form.trayIcon?.Visible == true;

        form.startupToggle.Checked = false;
        form.StartupToggleOnChanged(form.startupToggle, EventArgs.Empty);
        var disabled = !registration.Enabled && !form.state.Settings.StartWithWindows;

        form.startupToggle.Checked = true;
        form.StartupToggleOnChanged(form.startupToggle, EventArgs.Empty);
        var enabled = registration.Enabled && form.state.Settings.StartWithWindows;

        registration.FailNext = true;
        form.startupToggle.Checked = false;
        form.StartupToggleOnChanged(form.startupToggle, EventArgs.Empty);
        var failureRolledBack = registration.Enabled
            && form.state.Settings.StartWithWindows
            && form.startupToggle.Checked;
        if (!reconciled || !trayRecovered || !disabled || !enabled || !failureRolledBack)
        {
            throw new InvalidOperationException(
                $"Startup registration self-test failed: reconciled={reconciled}, trayRecovered={trayRecovered}, disabled={disabled}, "
                + $"enabled={enabled}, rollback={failureRolledBack}, fakeEnabled={registration.Enabled}, "
                + $"state={form.state.Settings.StartWithWindows}, toggle={form.startupToggle.Checked}, "
                + $"path={registration.ExecutablePath}, process={Environment.ProcessPath}.");
        }

        return true;
    }

    private sealed class FakeStartupRegistration : IStartupRegistration
    {
        public bool Enabled { get; private set; }
        public string? ExecutablePath { get; private set; }
        public bool FailNext { get; set; }

        public bool IsEnabled(string executablePath)
        {
            return Enabled && string.Equals(ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase);
        }

        public bool SetEnabled(bool enabled, string executablePath, out string? error)
        {
            if (FailNext)
            {
                FailNext = false;
                error = "simulated registry failure";
                return false;
            }

            Enabled = enabled;
            ExecutablePath = enabled ? executablePath : null;
            error = null;
            return true;
        }
    }

    public string RunParticleSelfTest()
    {
        Width = ExpandedWidth;
        Height = appHeight;
        Left = workArea.Right - Width - 2;
        Top = workArea.Top + 12;
        targetWidth = ExpandedWidth;
        content.Visible = true;
        peek.Visible = false;
        state.Settings.AutoHideEnabled = false;
        Show();
        Application.DoEvents();

        taskInput.Text = "粒子测试任务";
        AddTask();
        Application.DoEvents();
        var task = state.ActiveTasks.First();
        var row = taskRows[task.Id];

        var counts = new Dictionary<ParticleIntensity, int>();
        foreach (var intensity in Enum.GetValues<ParticleIntensity>())
        {
            particles.Clear();
            particleLayer.Hide();
            particleTimer.Stop();
            state.Settings.ParticleIntensity = intensity;
            LaunchParticles(row, task);
            counts[intensity] = particles.Count;
        }

        particles.Clear();
        particleLayer.Hide();
        particleTimer.Stop();
        state.Settings.ParticleIntensity = ParticleIntensity.Celebration;
        LaunchParticles(row, task);
        LaunchParticles(row, task);
        LaunchParticles(row, task);
        Application.DoEvents();
        var cappedCount = particles.Count;
        var layerVisible = particleLayer.Visible;

        var timeout = Stopwatch.StartNew();
        while (particles.Count > 0 && timeout.ElapsedMilliseconds < 2200)
        {
            Application.DoEvents();
            Thread.Sleep(4);
        }

        Application.DoEvents();
        var animationCompleted = particles.Count == 0 && !particleLayer.Visible;
        var rendered = particleLayer.RenderCount >= 10 && particleLayer.LastRenderSucceeded;

        state.Settings.ReducedMotion = true;
        LaunchParticles(row, task);
        var reducedMotionHonored = particles.Count == 0;
        Hide();

        var intensityCountsCorrect = counts[ParticleIntensity.Light] < counts[ParticleIntensity.Standard]
            && counts[ParticleIntensity.Standard] < counts[ParticleIntensity.Celebration];
        if (!layerVisible || cappedCount != MaxActiveParticles || !animationCompleted || !rendered
            || !reducedMotionHonored || !intensityCountsCorrect)
        {
            throw new InvalidOperationException(
                $"Particle self-test failed: visible={layerVisible}, cap={cappedCount}, completed={animationCompleted}, "
                + $"renders={particleLayer.RenderCount}, renderOk={particleLayer.LastRenderSucceeded}, "
                + $"reduced={reducedMotionHonored}, counts={string.Join(',', counts.Values)}.");
        }

        return $"light={counts[ParticleIntensity.Light]}; standard={counts[ParticleIntensity.Standard]}; "
            + $"celebration={counts[ParticleIntensity.Celebration]}; cap={cappedCount}; "
            + $"renders={particleLayer.RenderCount}; completed={animationCompleted}";
    }

    public string RunUiSelfTest(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        state.Settings.AutoHideEnabled = false;
        Width = ExpandedWidth;
        Height = appHeight;
        Left = workArea.Right - Width - 2;
        Top = workArea.Top + 12;
        targetWidth = ExpandedWidth;
        content.Visible = true;
        peek.Visible = false;
        Show();
        Application.DoEvents();

        AddUiFixtureData();
        RenderAll();
        ShowMainView(MainView.Today);
        taskRows["ui-1"].Focus();
        Application.DoEvents();
        var todayPath = CaptureUi(outputDirectory, "today.png");
        var todayLayout = ValidateUiLayout();
        var completedSectionVisible = taskList.FooterCount >= 2;
        SwitchPalette(AppThemeMode.Xuanling);
        Application.DoEvents();
        var xuanlingTasksPath = CaptureUi(outputDirectory, "xuanling-tasks.png");
        var xuanlingTasksLayout = ValidateUiLayout();
        var xuanlingTasksArtwork = themeCharacter.Visible && taskList.ThemeWatermark is not null;
        SwitchPalette(AppThemeMode.Dark);
        Application.DoEvents();
        ShowTaskMenu(state.Tasks.First(task => task.Id == "ui-1"), taskRows["ui-1"]);
        var menuFade = Stopwatch.StartNew();
        while (taskMenu.Opacity < 1 && menuFade.ElapsedMilliseconds < 600)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        Application.DoEvents();
        var recurrenceMenuPath = CaptureUi(outputDirectory, "recurrence-menu.png");
        var recurrenceMenuVisible = taskMenu.Visible
            && taskMenu.Items.Cast<ToolStripItem>().Any(item => item.Text?.StartsWith("重复：每日", StringComparison.Ordinal) == true)
            && taskMenu.Items.Cast<ToolStripItem>().Any(item => item.Text == "颜色");
        taskMenu.Close();

        ShowMainView(MainView.Tomorrow);
        Application.DoEvents();
        var tomorrowPath = CaptureUi(outputDirectory, "tomorrow.png");
        var tomorrowRow = taskRows["ui-tomorrow"];
        CompleteTask(tomorrowRow.Task, tomorrowRow);
        var tomorrowValid = !tomorrowRow.CompletionEnabled
            && tomorrowRow.Task.CompletedAt is null
            && state.TasksForDate(currentDate).All(task => task.Id != "ui-tomorrow");
        var tomorrowLayout = ValidateUiLayout();

        ShowMainView(MainView.Today);
        Application.DoEvents();

        var dragRow = taskRows["ui-1"];
        var grabOffset = new Point(180, dragRow.Height / 2);
        var dragStart = dragRow.PointToScreen(grabOffset);
        TaskRowOnDragStarted(dragRow, new TaskDragStartedEventArgs(dragStart, grabOffset));
        var dragDestination = taskList.PointToScreen(new Point(taskList.Width / 2, taskRows["ui-3"].Top + 20));
        TaskRowOnDragMoved(dragRow, new TaskDragEventArgs(dragDestination));
        Application.DoEvents();
        var dragPath = CaptureUi(outputDirectory, "drag.png", false);
        var dragLayout = ValidateUiLayout();
        FinishTaskDrag(false);
        var draggedOrder = taskList.Rows.Select(row => row.Task.Id).ToArray();
        var dragGestureValid = draggedOrder.FirstOrDefault() == "ui-2"
            && state.TasksForDate(currentDate).Select(task => task.Id).SequenceEqual(draggedOrder);

        state.Tasks.RemoveAll(task => task.IsActive && task.Day == currentDate);
        state.Tasks.RemoveAll(task => task.CompletedAt is not null
            && DateOnly.FromDateTime(task.CompletedAt.Value.LocalDateTime) == currentDate);
        RenderToday();
        Application.DoEvents();
        var emptyPath = CaptureUi(outputDirectory, "empty.png");
        var emptyLayout = ValidateUiLayout();
        SwitchPalette(AppThemeMode.Xuanling);
        Application.DoEvents();
        var xuanlingPath = CaptureUi(outputDirectory, "xuanling-empty.png");
        var themeMenu = moreMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "主题");
        var xuanlingLayout = ValidateUiLayout();
        var xuanlingArtworkVisible = UiTheme.Mode == AppThemeMode.Xuanling
            && themeCharacter.Visible
            && todayEmpty.Visible
            && todayEmpty.CharacterImage is not null
            && themeMenu?.DropDownItems.Count == 3;
        SwitchPalette(AppThemeMode.Dark);
        Application.DoEvents();

        AddUiFixtureData();
        RenderAll();

        ShowTemplateManager();
        Application.DoEvents();
        var templatePath = CaptureUi(outputDirectory, "templates.png");
        var templateLayout = ValidateUiLayout();
        var templateData = new DataObject();
        templateData.SetData(typeof(int), 0);
        var templateDropPoint = templateList.PointToScreen(new Point(20, templateList.ItemHeight + 8));
        TemplateListOnDragDrop(
            templateList,
            new DragEventArgs(templateData, 0, templateDropPoint.X, templateDropPoint.Y, DragDropEffects.Move, DragDropEffects.Move));
        var templateReordered = state.Templates.OrderBy(template => template.SortOrder).First().Id == "ui-template-2";
        templateList.SelectedIndex = 0;
        templateEditor.Text = "修改后的常用模板";
        RenameSelectedTemplate();
        var templateRenamed = state.Templates.Any(template => template.Text == "修改后的常用模板");
        DeleteSelectedTemplate();
        var templateDeleted = state.Templates.Count == 1;
        HideTemplateManager();

        ShowRecurringManager(null);
        Application.DoEvents();
        var recurringPath = CaptureUi(outputDirectory, "recurring.png");
        var recurringLayout = ValidateUiLayout();
        var recurringManagerValid = recurringList.Items.Count == 1
            && SelectedRecurringSeries is { Id: "ui-series" };
        recurringWeekdays.SetSelection([DayOfWeek.Monday, DayOfWeek.Wednesday]);
        recurringRuleChips.SelectedIndex = (int)RecurrenceRule.Weekly;
        ApplyRecurringRuleChange();
        var recurringRuleChanged = state.RecurringTasks.Single(series => series.Id == "ui-series") is
        {
            Rule: RecurrenceRule.Weekly,
            WeeklyDays.Count: 2
        };
        recurringRuleChips.SelectedIndex = (int)RecurrenceRule.Daily;
        ApplyRecurringRuleChange();
        HideRecurringManager();

        ShowMainView(MainView.History);
        Application.DoEvents();
        var historyPath = CaptureUi(outputDirectory, "history.png");
        var historyLayout = ValidateUiLayout();

        ShowSettings();
        Application.DoEvents();
        var settingsPath = CaptureUi(outputDirectory, "settings.png");
        var settingsLayout = ValidateUiLayout();

        HideSettings();
        SwitchPalette(AppThemeMode.Light);
        ShowMainView(MainView.Today);
        Application.DoEvents();
        var lightPath = CaptureUi(outputDirectory, "light-today.png");
        var lightLayout = ValidateUiLayout();
        SwitchPalette(AppThemeMode.Dark);
        Application.DoEvents();

        state.Settings.ReducedMotion = true;
        state.Settings.AutoHideEnabled = true;
        SetExpandedState(false);
        Application.DoEvents();
        var collapsedPath = CaptureUi(outputDirectory, "collapsed.png");
        var collapsedLayout = ValidateUiLayout();

        ShowMainView(MainView.History);
        ShowSettings();
        SetExpandedState(false);
        HandleExternalActivation();
        Application.DoEvents();
        var externalActivationValid = !settingsVisible
            && currentView == MainView.Today
            && targetWidth == ExpandedWidth
            && taskInput.Focused;

        string compactPath;
        string compactTemplatesPath;
        bool compactLayout;
        bool compactTemplatesLayout;
        using (var compact = new DailyGoalForm(Path.Combine(DataDirectory, "compact"), 500, false))
        {
            compact.state.Settings.AutoHideEnabled = false;
            compact.Width = ExpandedWidth;
            compact.Height = 500;
            var compactWorkArea = compact.workArea;
            compact.Left = compactWorkArea.Right - compact.Width - 2;
            compact.Top = compactWorkArea.Top + 12;
            compact.targetWidth = ExpandedWidth;
            compact.content.Visible = true;
            compact.peek.Visible = false;
            compact.Show();
            compact.AddUiFixtureData();
            compact.RenderAll();
            compact.ShowMainView(MainView.Today);
            Application.DoEvents();
            compactPath = compact.CaptureUi(outputDirectory, "compact.png");
            compactLayout = compact.ValidateUiLayout();
            compact.ShowTemplateManager();
            Application.DoEvents();
            compactTemplatesPath = compact.CaptureUi(outputDirectory, "compact-templates.png");
            compactTemplatesLayout = compact.ValidateUiLayout();
            compact.Hide();
        }

        var layoutValid = todayLayout && completedSectionVisible && xuanlingTasksLayout && xuanlingTasksArtwork && recurrenceMenuVisible && tomorrowLayout && tomorrowValid && dragLayout && dragGestureValid && emptyLayout
            && xuanlingLayout && xuanlingArtworkVisible
            && templateLayout && templateReordered && templateRenamed && templateDeleted
            && recurringLayout && recurringManagerValid && recurringRuleChanged
            && historyLayout && settingsLayout && lightLayout
            && collapsedLayout && compactLayout && compactTemplatesLayout && externalActivationValid;
        Hide();
        if (!layoutValid)
        {
            throw new InvalidOperationException(
                "UI self-test failed: "
                + $"today={todayLayout}, completedSection={completedSectionVisible}, xuanlingTasks={xuanlingTasksLayout}, xuanlingArt={xuanlingTasksArtwork}, "
                + $"recurrenceMenu={recurrenceMenuVisible}, tomorrow={tomorrowLayout}/{tomorrowValid}, drag={dragLayout}/{dragGestureValid}, empty={emptyLayout}, "
                + $"xuanlingEmpty={xuanlingLayout}/{xuanlingArtworkVisible}, template={templateLayout}/{templateReordered}/{templateRenamed}/{templateDeleted}, "
                + $"recurring={recurringLayout}/{recurringManagerValid}/{recurringRuleChanged}, history={historyLayout}, settings={settingsLayout}, "
                + $"light={lightLayout}, collapsed={collapsedLayout}, compact={compactLayout}/{compactTemplatesLayout}, activation={externalActivationValid}.");
        }

        return $"today={todayPath}; xuanlingTasks={xuanlingTasksPath}; recurrenceMenu={recurrenceMenuPath}; tomorrow={tomorrowPath}; drag={dragPath}; empty={emptyPath}; xuanling={xuanlingPath}; templates={templatePath}; recurring={recurringPath}; history={historyPath}; settings={settingsPath}; light={lightPath}; collapsed={collapsedPath}; "
            + $"compact={compactPath}; compactTemplates={compactTemplatesPath}; tomorrowValid={tomorrowValid}; dragGesture={dragGestureValid}; templateActions={templateReordered && templateRenamed && templateDeleted}; recurringManager={recurringManagerValid && recurringRuleChanged}; externalActivation={externalActivationValid}; layout={layoutValid}";
    }

    private void AddUiFixtureData()
    {
        state.Tasks.Clear();
        state.Templates.Clear();
        state.RecurringTasks.Clear();
        state.RecurringTasks.Add(new RecurringTask
        {
            Id = "ui-series",
            Text = "整理今天最重要的工作清单",
            AccentIndex = 0,
            Rule = RecurrenceRule.Daily,
            WeeklyDay = currentDate.DayOfWeek,
            CreatedAt = DateTimeOffset.Now.AddDays(-4),
            LastGeneratedDate = currentDate,
            Enabled = true
        });
        state.Tasks.AddRange(
        [
            new TaskRecord
            {
                Id = "ui-1",
                Text = "整理今天最重要的工作清单",
                CreatedAt = DateTimeOffset.Now.AddHours(-2),
                Day = currentDate,
                SortOrder = 0,
                AccentIndex = 0,
                RecurrenceId = "ui-series"
            },
            new TaskRecord
            {
                Id = "ui-tomorrow",
                Text = "准备明天上午的会议材料",
                CreatedAt = DateTimeOffset.Now,
                Day = currentDate.AddDays(1),
                SortOrder = 0,
                AccentIndex = 4
            },
            new TaskRecord
            {
                Id = "ui-2",
                Text = "完成产品页面的交互细节检查",
                CreatedAt = DateTimeOffset.Now.AddHours(-1),
                Day = currentDate,
                SortOrder = 1,
                AccentIndex = 2,
                CarryCount = 1
            },
            new TaskRecord
            {
                Id = "ui-3",
                Text = "散步二十分钟",
                CreatedAt = DateTimeOffset.Now,
                Day = currentDate,
                SortOrder = 2,
                AccentIndex = 1
            },
            new TaskRecord
            {
                Id = "ui-history-1",
                Text = "回复重要邮件",
                CreatedAt = DateTimeOffset.Now.AddDays(-1),
                Day = currentDate.AddDays(-1),
                SortOrder = 3,
                AccentIndex = 3,
                CompletedAt = new DateTimeOffset(currentDate.ToDateTime(new TimeOnly(14, 20)))
            },
            new TaskRecord
            {
                Id = "ui-history-2",
                Text = "完成昨日复盘",
                CreatedAt = DateTimeOffset.Now.AddDays(-2),
                Day = currentDate.AddDays(-2),
                SortOrder = 4,
                AccentIndex = 4,
                CompletedAt = new DateTimeOffset(currentDate.AddDays(-1).ToDateTime(new TimeOnly(15, 20)))
            }
        ]);
        state.Templates.AddRange(
        [
            new TaskTemplate { Id = "ui-template-1", Text = "晨间整理", AccentIndex = 1, SortOrder = 0 },
            new TaskTemplate { Id = "ui-template-2", Text = "下班前复盘", AccentIndex = 3, SortOrder = 1 }
        ]);
        state.LastOpenedDate = currentDate;
        state.Normalize(currentDate);
    }

    private string CaptureUi(string outputDirectory, string fileName, bool reassertTopMost = true)
    {
        var path = Path.Combine(outputDirectory, fileName);
        if (reassertTopMost)
        {
            BringToFront();
        }

        Refresh();
        Application.DoEvents();
        Thread.Sleep(90);
        if (reassertTopMost)
        {
            TopMost = false;
            TopMost = true;
            BringToFront();
            Refresh();
            Application.DoEvents();
        }

        _ = NativeMethods.DwmFlush();

        var captureBounds = targetWidth == CollapsedWidth
            ? new Rectangle(
                slideUsesPosition && state.Settings.DockSide == DockSide.Left ? ExpandedWidth - CollapsedWidth : 0,
                0,
                CollapsedWidth,
                Height)
            : ClientRectangle;
        var screenBounds = RectangleToScreen(captureBounds);
        using var bitmap = new Bitmap(Math.Max(1, screenBounds.Width), Math.Max(1, screenBounds.Height), PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(screenBounds.Location, Point.Empty, bitmap.Size, CopyPixelOperation.SourceCopy);
        }

        bitmap.Save(path, ImageFormat.Png);
        if (targetWidth != CollapsedWidth && !HasHealthyCapturedSurface(bitmap))
        {
            throw new InvalidOperationException($"UI screen capture contains an unexpected dark surface: {fileName}");
        }

        return path;
    }

    private static bool HasHealthyCapturedSurface(Bitmap bitmap)
    {
        var darkPixels = 0;
        var sampledPixels = 0;
        for (var y = 8; y < bitmap.Height - 8; y += 3)
        {
            for (var x = 8; x < bitmap.Width - 8; x += 3)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.R < 22 && color.G < 22 && color.B < 22)
                {
                    darkPixels += 1;
                }

                sampledPixels += 1;
            }
        }

        return sampledPixels == 0 || darkPixels < sampledPixels * 0.08;
    }

    private bool ValidateUiLayout()
    {
        if (targetWidth == CollapsedWidth)
        {
            var expectedRailBounds = new Rectangle(
                slideUsesPosition && state.Settings.DockSide == DockSide.Left ? ExpandedWidth - CollapsedWidth : 0,
                0,
                CollapsedWidth,
                Height);
            return peek.Visible
                && !content.Visible
                && peek.Bounds == expectedRailBounds
                && Width == (slideUsesPosition ? ExpandedWidth : CollapsedWidth);
        }

        var fixedControls = new Control[]
        {
            titleLabel,
            dateLabel,
            dragGrip,
            progressRing,
            themeCharacter,
            settingsButton,
            moreButton,
            closeButton,
            todayTab,
            tomorrowTab,
            historyTab,
            todayView,
            historyView,
            settingsView,
            settingsCloseButton,
            templateView,
            templateCloseButton,
            recurringView,
            recurringCloseButton
        };
        return fixedControls.All(control =>
            !control.Visible
            || control.Parent is null
            || control.Parent.ClientRectangle.Contains(control.Bounds));
    }
}
