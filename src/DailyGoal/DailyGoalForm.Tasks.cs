using System.Globalization;
using System.Runtime.InteropServices;

namespace DailyGoal;

internal sealed partial class DailyGoalForm
{
    private DateOnly SelectedDate => currentView == MainView.Tomorrow ? currentDate.AddDays(1) : currentDate;

    private IEnumerable<TaskRecord> CurrentListTasks =>
        currentView == MainView.Inbox ? state.InboxTasks : state.TasksForDate(SelectedDate);

    private bool TaskBelongsToCurrentList(TaskRecord task) =>
        currentView == MainView.Inbox
            ? task.IsInbox
            : currentView is MainView.Today or MainView.Tomorrow && !task.IsInbox && task.Day == SelectedDate;

    private IEnumerable<TaskRecord> CompletedTodayTasks => state.HistoryTasks.Where(task =>
        task.CompletedAt is not null
        && DateOnly.FromDateTime(task.CompletedAt.Value.LocalDateTime) == currentDate);

    private void RenderToday()
    {
        RenderScheduledTasks();
    }

    private void RenderScheduledTasks()
    {
        var previousScroll = taskList.ScrollValue;
        CancelRowAnimationTimers();
        taskRows.Clear();
        taskList.SuspendLayout();
        DisposeChildren(taskList);

        var rowWidth = Math.Max(UiTheme.S(350), taskList.ClientSize.Width - taskList.Padding.Horizontal - UiTheme.S(18));
        foreach (var task in CurrentListTasks)
        {
            var row = CreateTaskRow(task, rowWidth);
            taskRows[task.Id] = row;
            taskList.Controls.Add(row);
        }

        taskList.ResumeLayout();
        taskList.ArrangeRows(true);
        RefreshCompletedSection();
        taskList.ScrollValue = previousScroll;
        UpdateHeaderStats();
    }

    /// <summary>重建今日列表底部的「已完成」分组（明日与待办箱视图没有该分组）。</summary>
    private void RefreshCompletedSection()
    {
        var previousScroll = taskList.ScrollValue;
        taskList.ClearFooters();
        if (currentView == MainView.Today)
        {
            var completed = CompletedTodayTasks.ToArray();
            if (completed.Length > 0)
            {
                var header = new CompletedSectionHeader
                {
                    Height = UiTheme.S(36),
                    Margin = new Padding(0, UiTheme.S(6), 0, UiTheme.S(6)),
                    CompletedCount = completed.Length,
                    Collapsed = state.Settings.CompletedSectionCollapsed
                };
                header.ToggleRequested += (_, _) => ToggleCompletedSection();
                header.MouseWheel += (_, args) => taskList.ScrollByWheel(args.Delta);
                taskList.AddFooter(header);

                if (!state.Settings.CompletedSectionCollapsed)
                {
                    foreach (var task in completed)
                    {
                        var row = new CompletedRowControl(task)
                        {
                            Height = UiTheme.S(56),
                            Margin = new Padding(0, 0, 0, UiTheme.S(8))
                        };
                        row.RestoreRequested += (_, _) => RestoreHistoryTask(task.Id);
                        row.MouseWheel += (_, args) => taskList.ScrollByWheel(args.Delta);
                        taskList.AddFooter(row);
                    }
                }
            }
        }

        taskList.ScrollValue = previousScroll;
        UpdateTodayEmptyState();
    }

    private void ToggleCompletedSection()
    {
        state.Settings.CompletedSectionCollapsed = !state.Settings.CompletedSectionCollapsed;
        _ = stateStore.Save(state, out _);
        RefreshCompletedSection();
    }

    /// <summary>今日视图在“无待办且无完成”时才显示空状态插画；明日与待办箱只看待办。</summary>
    private void UpdateTodayEmptyState()
    {
        if (currentView == MainView.Inbox)
        {
            todayEmpty.Title = "待办箱很清爽";
            todayEmpty.Subtitle = "把暂时不急的想法先存到这里";
        }
        else if (currentView == MainView.Tomorrow)
        {
            todayEmpty.Title = "明天还没有安排";
            todayEmpty.Subtitle = "提前写一两条，明早会更从容";
        }
        else
        {
            todayEmpty.Title = "今天很轻盈";
            todayEmpty.Subtitle = "写下一件想完成的事";
        }

        var showEmpty = taskRows.Count == 0
            && (currentView != MainView.Today || !CompletedTodayTasks.Any());
        taskList.Visible = !showEmpty;
        todayEmpty.Visible = showEmpty;
        if (showEmpty)
        {
            todayEmpty.Invalidate();
            todayEmpty.BringToFront();
        }
    }

    private TaskRowControl CreateTaskRow(TaskRecord task, int rowWidth)
    {
        var row = new TaskRowControl(task)
        {
            Width = rowWidth,
            Height = TaskRowControl.BaseHeight,
            Margin = new Padding(0, 0, 0, UiTheme.S(10)),
            CompletionEnabled = task.IsInbox || task.Day == currentDate
        };
        row.CompleteRequested += (_, _) => CompleteTask(task, row);
        row.MoreRequested += (_, _) => ShowTaskMenu(task, row);
        row.ContextMenuRequested += (_, screenPoint) => ShowTaskMenu(task, row, screenPoint);
        row.DeleteRequested += (_, _) => DeleteTaskWithRecurrence(task.Id);
        row.SubTaskToggled += (_, subId) => ToggleSubTask(task.Id, subId);
        row.DetailsEditRequested += (_, _) => EditTaskDetails(task);
        row.EditCommitted += (_, args) =>
        {
            EditTask(task.Id, args);
            if (!args.Cancel)
            {
                UpdateRowTooltip(row);
            }
        };
        row.KeyboardReorderRequested += (_, args) => MoveTask(task.Id, args.Direction);
        row.TaskDragStarted += TaskRowOnDragStarted;
        row.TaskDragMoved += TaskRowOnDragMoved;
        row.TaskDragEnded += TaskRowOnDragEnded;
        row.MouseWheel += (_, args) => taskList.ScrollByWheel(args.Delta);
        UpdateRowTooltip(row);
        return row;
    }

    /// <summary>标题被截断时挂 tooltip 展示全文，未截断则移除。</summary>
    private void UpdateRowTooltip(TaskRowControl row)
    {
        toolTip.SetToolTip(row, row.IsTextOverflowing ? row.Task.Text : null);
    }

    private void InsertScheduledTaskRow(TaskRecord task)
    {
        if (!TaskBelongsToCurrentList(task) || taskRows.ContainsKey(task.Id))
        {
            return;
        }
        var rowWidth = Math.Max(UiTheme.S(350), taskList.ClientSize.Width - taskList.Padding.Horizontal - UiTheme.S(18));
        var row = CreateTaskRow(task, rowWidth);
        taskRows[task.Id] = row;
        taskList.Controls.Add(row);
        taskList.ApplyOrder(CurrentListTasks.Select(candidate => candidate.Id), true);
        UpdateTodayEmptyState();
        row.Focus();
    }

    private void RemoveScheduledTaskRow(string taskId)
    {
        if (taskRows.Remove(taskId, out var row) && !row.IsDisposed)
        {
            taskList.Controls.Remove(row);
            row.Dispose();
        }
        taskList.ApplyOrder(CurrentListTasks.Select(candidate => candidate.Id), true);
        UpdateTodayEmptyState();
    }

    private void RenderHistory()
    {
        historyDirty = false;
        historyList.SuspendLayout();
        DisposeChildren(historyList);

        var allHistory = state.HistoryTasks.ToArray();
        var filteredHistory = string.IsNullOrWhiteSpace(historySearchQuery)
            ? allHistory
            : allHistory.Where(task =>
                task.Text.Contains(historySearchQuery, StringComparison.OrdinalIgnoreCase)
                || (task.Notes?.Contains(historySearchQuery, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToArray();

        var rowWidth = Math.Max(UiTheme.S(350), historyList.ClientSize.Width - historyList.Padding.Horizontal - UiTheme.S(18));
        foreach (var group in filteredHistory.GroupBy(task => DateOnly.FromDateTime(task.CompletedAt!.Value.LocalDateTime)))
        {
            var header = new HistoryDateHeader(FormatHistoryDate(group.Key))
            {
                Width = rowWidth,
                Height = UiTheme.S(32),
                Margin = new Padding(0, UiTheme.S(2), 0, UiTheme.S(2))
            };
            historyList.Controls.Add(header);

            foreach (var task in group)
            {
                var row = new HistoryRowControl(task)
                {
                    Width = rowWidth,
                    Height = UiTheme.S(72),
                    Margin = new Padding(0, 0, 0, UiTheme.S(8))
                };
                row.RestoreRequested += (_, _) => RestoreHistoryTask(task.Id);
                row.DeleteRequested += (_, _) => DeleteHistoryTask(task.Id);
                row.MouseWheel += (_, args) => historyList.ScrollByWheel(args.Delta);
                historyList.Controls.Add(row);
            }
        }

        historyList.ResumeLayout();
        historyList.Relayout();
        var empty = filteredHistory.Length == 0;
        historyList.Visible = !empty;
        historyEmpty.Title = !string.IsNullOrWhiteSpace(historySearchQuery) ? "没有匹配的记录" : "还没有完成记录";
        historyEmpty.Subtitle = !string.IsNullOrWhiteSpace(historySearchQuery) ? "换个关键词或清空搜索框试试" : "完成一件事后，它会出现在这里";
        historyEmpty.Visible = empty;
        if (empty)
        {
            historyEmpty.Invalidate();
            historyEmpty.BringToFront();
        }

        var countsByDay = allHistory
            .Where(task => task.CompletedAt is not null)
            .GroupBy(task => DateOnly.FromDateTime(task.CompletedAt!.Value.LocalDateTime))
            .ToDictionary(group => group.Key, group => group.Count());

        var counts = new int[7];
        for (var offset = 0; offset < 7; offset += 1)
        {
            counts[offset] = countsByDay.GetValueOrDefault(currentDate.AddDays(offset - 6), 0);
        }

        historySummary.ReferenceDate = currentDate;
        historySummary.DailyCounts = counts;
        historySummary.StreakDays = ComputeStreak(countsByDay);
        historySummary.HeatCells = BuildHeatCells(countsByDay);
    }

    /// <summary>连续完成天数：从今天（今天还没完成则从昨天）向前数有完成记录的天数，受 30 天保留期封顶。</summary>
    private int ComputeStreak(IReadOnlyDictionary<DateOnly, int> countsByDay)
    {
        var probe = countsByDay.ContainsKey(currentDate) ? currentDate : currentDate.AddDays(-1);
        var streak = 0;
        while (streak < 30 && countsByDay.TryGetValue(probe, out var count) && count > 0)
        {
            streak += 1;
            probe = probe.AddDays(-1);
        }

        return streak;
    }

    /// <summary>近 5 周热力图数据：列 = 周（最右为本周），行 = 周一→周日；未来或超出保留期的格子为 -1。</summary>
    private int[] BuildHeatCells(IReadOnlyDictionary<DateOnly, int> countsByDay)
    {
        static int MondayIndex(DayOfWeek day) => day == DayOfWeek.Sunday ? 6 : (int)day - 1;

        var cells = new int[35];
        Array.Fill(cells, -1);
        var currentWeekMonday = currentDate.AddDays(-MondayIndex(currentDate.DayOfWeek));
        var oldestKept = currentDate.AddDays(-29);
        for (var week = 0; week < 5; week += 1)
        {
            var monday = currentWeekMonday.AddDays(-7 * (4 - week));
            for (var dayIndex = 0; dayIndex < 7; dayIndex += 1)
            {
                var day = monday.AddDays(dayIndex);
                if (day > currentDate || day < oldestKept)
                {
                    continue;
                }

                cells[week * 7 + dayIndex] = countsByDay.GetValueOrDefault(day, 0);
            }
        }

        return cells;
    }

    private static void DisposeChildren(Control parent)
    {
        if (parent is TaskFlowPanel taskPanel)
        {
            taskPanel.ClearRows();
            return;
        }

        if (parent is StackScrollPanel stackPanel)
        {
            stackPanel.ClearStack();
            return;
        }

        while (parent.Controls.Count > 0)
        {
            var child = parent.Controls[0];
            parent.Controls.RemoveAt(0);
            child.Dispose();
        }
    }

    private string FormatHistoryDate(DateOnly date)
    {
        if (date == currentDate)
        {
            return "今天";
        }

        if (date == currentDate.AddDays(-1))
        {
            return "昨天";
        }

        return date.ToDateTime(TimeOnly.MinValue).ToString("M 月 d 日  dddd", new CultureInfo("zh-CN"));
    }

    private void AddTask(bool shiftAlternateDay = false)
    {
        var raw = taskInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            taskInput.Clear();
            return;
        }

        var parsed = QuickCaptureParser.Parse(raw);
        var target = parsed.Target;
        if (shiftAlternateDay && target == CaptureTarget.CurrentView)
        {
            target = currentView == MainView.Tomorrow ? CaptureTarget.Today : CaptureTarget.Tomorrow;
        }

        var isInbox = target == CaptureTarget.Inbox || (target == CaptureTarget.CurrentView && currentView == MainView.Inbox);
        var date = target switch
        {
            CaptureTarget.Today => currentDate,
            CaptureTarget.Tomorrow => currentDate.AddDays(1),
            CaptureTarget.Inbox => currentDate,
            _ => SelectedDate
        };

        var created = AddTaskRecord(parsed.Text, date, isInbox, parsed.AccentIndex, parsed.IsPinned);
        if (created is null)
        {
            return;
        }

        if (parsed.Recurrence is { } rule && !created.IsInbox)
        {
            SetRecurrence(created, rule, quiet: true);
        }

        taskInput.Clear();
        taskInput.Focus();

        if (!TaskBelongsToCurrentList(created))
        {
            var destination = created.IsInbox
                ? "待办箱"
                : created.Day == currentDate ? "今日计划" : "明日计划";
            ShowTransientStatus($"已添加到{destination}：{created.Text}");
        }
        else if (parsed.Recurrence is not null)
        {
            ShowTransientStatus($"已添加并设为{(parsed.Recurrence == RecurrenceRule.Daily ? "每日" : "工作日")}重复");
        }
    }

    /// <summary>粘贴多行文本时按行拆成多条任务；单行走默认粘贴。返回是否已处理。</summary>
    private bool TryAddTasksFromClipboard()
    {
        string text;
        try
        {
            if (!Clipboard.ContainsText())
            {
                return false;
            }

            text = Clipboard.GetText();
        }
        catch (ExternalException)
        {
            return false;
        }

        if (!text.Contains('\n', StringComparison.Ordinal))
        {
            return false;
        }

        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(30)
            .ToArray();
        if (lines.Length <= 1)
        {
            return false;
        }

        var added = AddTasksFromLines(lines);
        if (added > 0)
        {
            taskInput.Clear();
            ShowTransientStatus($"已批量添加 {added} 项任务");
        }

        return added > 0;
    }

    /// <summary>批量按行建任务；倒序插入使第一行位于列表顶部。</summary>
    internal int AddTasksFromLines(IReadOnlyList<string> lines)
    {
        var added = 0;
        var isInbox = currentView == MainView.Inbox;
        foreach (var line in lines.Reverse())
        {
            var text = line.Length > 160 ? line[..160] : line;
            if (AddTaskRecord(text, SelectedDate, isInbox) is not null)
            {
                added += 1;
            }
        }

        return added;
    }

    private void CompleteTask(TaskRecord task, TaskRowControl row)
    {
        if (!task.IsActive || (!task.IsInbox && task.Day != currentDate) || row.CompletedVisual)
        {
            return;
        }

        if (focusedTaskId == task.Id)
        {
            StopFocus(recordElapsed: true, silent: true);
        }

        var snapshot = state.Clone();
        var previousSortOrder = task.SortOrder;
        var completedAt = clock.Now;
        task.CompletedAt = completedAt;
        if (task.IsInbox)
        {
            state.NormalizeInboxSortOrders();
        }
        else
        {
            state.NormalizeSortOrders(currentDate);
        }
        state.PruneHistory(currentDate);
        if (!SaveState())
        {
            state = snapshot;
            RenderScheduledTasks();
            return;
        }

        var allDoneNow = !task.IsInbox && !state.TasksForDate(currentDate).Any();

        row.CompletedVisual = true;
        row.Pulse = state.Settings.ReducedMotion ? 0f : 1f;
        row.CheckPulse = state.Settings.ReducedMotion ? 0f : 1f;
        row.StrikeProgress = state.Settings.ReducedMotion ? 1f : 0f;
        if (!state.Settings.ReducedMotion)
        {
            StartRowPulse(row);
            LaunchParticles(row, task);
            if (allDoneNow)
            {
                LaunchAllDoneCelebration();
            }
        }

        PlayCompletionFeedback(allDoneNow);
        var undoText = allDoneNow
            ? $"今日全部完成 ✨ 已完成“{task.Text}”"
            : $"已完成“{task.Text}”";
        ShowUndo(new UndoCompletion(task.Id, previousSortOrder, completedAt), undoText);
        UpdateHeaderStats();
        MarkHistoryDirty();
        ScheduleCompletedRowRemoval(task.Id, row);
    }

    private void PlayCompletionFeedback(bool allDone = false)
    {
        PlayFeedbackSound(allDone ? FeedbackSoundKind.AllDone : FeedbackSoundKind.TaskComplete);
    }

    private void StartRowPulse(TaskRowControl row)
    {
        var timer = CreateRowAnimationTimer(16);
        timer.Tick += (_, _) =>
        {
            if (row.IsDisposed)
            {
                DisposeRowAnimationTimer(timer);
                return;
            }

            row.Pulse = Math.Max(0f, row.Pulse - 0.12f);
            row.CheckPulse = Math.Max(0f, row.CheckPulse - 0.1f);
            row.StrikeProgress = Math.Min(1f, row.StrikeProgress + 0.09f);
            if (row.Pulse <= 0 && row.CheckPulse <= 0 && row.StrikeProgress >= 1f)
            {
                DisposeRowAnimationTimer(timer);
            }
        };
        timer.Start();
    }

    private void ScheduleCompletedRowRemoval(string taskId, TaskRowControl row)
    {
        CancelCompletedRowRemoval(taskId);
        var delay = state.Settings.ReducedMotion ? 250 : CompletionDelayMs;
        var timer = CreateRowAnimationTimer(delay);
        completionTimers[taskId] = timer;
        timer.Tick += (_, _) =>
        {
            completionTimers.Remove(taskId);
            DisposeRowAnimationTimer(timer);
            if (state.Settings.ReducedMotion)
            {
                RemoveCompletedRow(taskId, row);
            }
            else
            {
                AnimateCompletedRowCollapse(taskId, row);
            }
        };
        timer.Start();
    }

    private void AnimateCompletedRowCollapse(string taskId, TaskRowControl row)
    {
        if (row.IsDisposed || row.IsRemoving)
        {
            RemoveCompletedRow(taskId, row);
            return;
        }

        row.IsRemoving = true;
        row.Enabled = false;
        var timer = CreateRowAnimationTimer(16);
        timer.Tick += (_, _) =>
        {
            if (row.IsDisposed || row.Height <= 4)
            {
                DisposeRowAnimationTimer(timer);
                RemoveCompletedRow(taskId, row);
                return;
            }

            row.Height = Math.Max(0, row.Height - 6);
            row.Margin = new Padding(0, 0, 0, Math.Max(0, row.Margin.Bottom - 1));
        };
        timer.Start();
    }

    private void RemoveCompletedRow(string taskId, TaskRowControl row)
    {
        taskRows.Remove(taskId);
        if (!row.IsDisposed)
        {
            taskList.Controls.Remove(row);
            row.Dispose();
        }

        RefreshCompletedSection();
    }

    private void ShowUndo(UndoEntry entry, string message)
    {
        undoEntry = entry;
        undoShownAt = DateTime.Now;
        undoBar.Message = message;
        undoBar.RemainingRatio = 1f;
        undoBar.Visible = true;
        undoBar.BringToFront();
        undoTimer.Stop();
        undoTimer.Start();
        undoCountdownTimer.Stop();
        undoCountdownTimer.Start();
    }

    private void HideUndoBar()
    {
        undoTimer.Stop();
        undoCountdownTimer.Stop();
        undoEntry = null;
        undoBar.Visible = false;
    }

    private bool UndoRefersTo(string taskId)
    {
        return undoEntry switch
        {
            UndoCompletion completion => completion.TaskId == taskId,
            UndoDeletion deletion => deletion.Snapshot.Id == taskId,
            _ => false
        };
    }

    private void UndoLast()
    {
        switch (undoEntry)
        {
            case UndoCompletion completion:
                UndoCompletionEntry(completion);
                break;
            case UndoDeletion deletion:
                UndoDeletionEntry(deletion);
                break;
        }
    }

    private void UndoCompletionEntry(UndoCompletion entry)
    {
        var task = state.Tasks.FirstOrDefault(candidate => candidate.Id == entry.TaskId);
        if (task?.CompletedAt is null || task.CompletedAt != entry.CompletedAt)
        {
            HideUndoBar();
            return;
        }

        var snapshot = state.Clone();
        if (task.IsInbox)
        {
            foreach (var active in state.InboxTasks.Where(candidate => candidate.SortOrder >= entry.PreviousSortOrder))
            {
                active.SortOrder += 1;
            }
            task.CompletedAt = null;
            task.SortOrder = entry.PreviousSortOrder;
            state.NormalizeInboxSortOrders();
        }
        else
        {
            foreach (var active in state.TasksForDate(currentDate).Where(candidate => candidate.SortOrder >= entry.PreviousSortOrder))
            {
                active.SortOrder += 1;
            }

            task.CompletedAt = null;
            task.Day = currentDate;
            task.SortOrder = entry.PreviousSortOrder;
            state.NormalizeSortOrders(currentDate);
        }
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        CancelCompletedRowRemoval(task.Id);
        HideUndoBar();
        RenderToday();
        MarkHistoryDirty();
        UpdateHeaderStats();
        if (taskRows.TryGetValue(task.Id, out var row))
        {
            row.Focus();
        }
    }

    private void UndoDeletionEntry(UndoDeletion entry)
    {
        if (state.Tasks.Any(candidate => candidate.Id == entry.Snapshot.Id))
        {
            HideUndoBar();
            return;
        }

        var snapshot = state.Clone();
        var restored = entry.Snapshot.Clone();
        if (restored.IsActive)
        {
            var list = restored.IsInbox ? state.InboxTasks : state.TasksForDate(restored.Day);
            foreach (var active in list.Where(candidate => candidate.SortOrder >= restored.SortOrder))
            {
                active.SortOrder += 1;
            }
        }

        state.Tasks.Add(restored);
        if (entry.ReenableRecurrenceId is not null)
        {
            var recurring = state.RecurringTasks.FirstOrDefault(candidate => candidate.Id == entry.ReenableRecurrenceId);
            if (recurring is not null)
            {
                recurring.Enabled = true;
            }
        }

        if (restored.IsInbox)
        {
            state.NormalizeInboxSortOrders();
        }
        else
        {
            state.NormalizeSortOrders(restored.Day);
        }
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        HideUndoBar();
        RenderScheduledTasks();
        MarkHistoryDirty();
        UpdateHeaderStats();
    }

    private void RestoreHistoryTask(string taskId)
    {
        var task = state.Tasks.FirstOrDefault(candidate => candidate.Id == taskId && !candidate.IsActive);
        if (task is null)
        {
            return;
        }

        var snapshot = state.Clone();
        foreach (var active in state.TasksForDate(currentDate))
        {
            active.SortOrder += 1;
        }

        task.CompletedAt = null;
        task.IsInbox = false;
        task.Day = currentDate;
        task.SortOrder = 0;
        task.RecurrenceId = null;
        state.NormalizeSortOrders(currentDate);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        if (UndoRefersTo(taskId))
        {
            HideUndoBar();
        }

        RenderToday();
        MarkHistoryDirty();
        UpdateHeaderStats();
    }

    private void DeleteHistoryTask(string taskId)
    {
        var task = state.Tasks.FirstOrDefault(candidate => candidate.Id == taskId && !candidate.IsActive);
        if (task is null)
        {
            return;
        }

        var snapshot = state.Clone();
        var deleted = task.Clone();
        state.Tasks.Remove(task);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        ShowUndo(new UndoDeletion(deleted, null), $"已删除“{deleted.Text}”");
        RefreshCompletedSection();
        MarkHistoryDirty();
        UpdateHeaderStats();
    }

    private void DeleteTask(string taskId)
    {
        var snapshot = state.Clone();
        var task = state.Tasks.FirstOrDefault(task => task.Id == taskId && task.IsActive);
        if (task is null)
        {
            return;
        }

        if (focusedTaskId == task.Id)
        {
            StopFocus(recordElapsed: false, silent: true);
        }

        var deleted = task.Clone();
        state.Tasks.Remove(task);

        if (task.IsInbox)
        {
            state.NormalizeInboxSortOrders();
        }
        else
        {
            state.NormalizeSortOrders(task.Day);
        }
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        ShowUndo(new UndoDeletion(deleted, null), $"已删除“{deleted.Text}”");
        RemoveScheduledTaskRow(taskId);
        UpdateHeaderStats();
    }

    private void EditTask(string taskId, TaskTextEditEventArgs args)
    {
        var task = state.Tasks.FirstOrDefault(candidate => candidate.Id == taskId && candidate.IsActive);
        if (task is null)
        {
            args.Cancel = true;
            return;
        }

        var previous = task.Text;
        var recurring = task.RecurrenceId is null
            ? null
            : state.RecurringTasks.FirstOrDefault(candidate => candidate.Id == task.RecurrenceId);
        var previousRecurringText = recurring?.Text;
        task.Text = args.Text;
        if (recurring is not null)
        {
            recurring.Text = args.Text;
        }
        if (!SaveState())
        {
            task.Text = previous;
            if (recurring is not null && previousRecurringText is not null)
            {
                recurring.Text = previousRecurringText;
            }
            args.Cancel = true;
        }
    }

    private void MoveTask(string taskId, int direction)
    {
        var active = CurrentListTasks.ToList();
        var index = active.FindIndex(task => task.Id == taskId);
        var targetIndex = index + Math.Sign(direction);
        if (index < 0 || targetIndex < 0 || targetIndex >= active.Count)
        {
            return;
        }

        var snapshot = state.Clone();
        (active[index].SortOrder, active[targetIndex].SortOrder) = (active[targetIndex].SortOrder, active[index].SortOrder);
        if (currentView == MainView.Inbox)
        {
            state.NormalizeInboxSortOrders();
        }
        else
        {
            state.NormalizeSortOrders(SelectedDate);
        }
        if (!SaveState())
        {
            state = snapshot;
        }

        taskList.ApplyOrder(CurrentListTasks.Select(task => task.Id), true);
        if (taskRows.TryGetValue(taskId, out var row))
        {
            row.Focus();
        }
    }

    private void TaskRowOnDragStarted(object? sender, TaskDragStartedEventArgs e)
    {
        if (sender is not TaskRowControl row || row.IsDisposed || dragInProgress)
        {
            return;
        }

        dragOriginalOrder = taskList.Rows.Select(candidate => candidate.Task.Id).ToList();
        dragInProgress = true;
        activeDragRow = row;
        dragGrabOffset = e.GrabOffset;
        row.IsDragPlaceholder = true;
        taskList.DropIndicatorIndex = taskList.IndexOf(row);

        taskDragGhost?.Dispose();
        taskDragGhost = new TaskDragGhost(row.Task, row.Size);
        taskDragGhost.ShowFor(this, e.ScreenLocation, e.GrabOffset);
        lastInside = DateTime.Now;
    }

    private void TaskRowOnDragMoved(object? sender, TaskDragEventArgs e)
    {
        if (!dragInProgress || sender is not TaskRowControl row || row != activeDragRow)
        {
            return;
        }

        taskDragGhost?.UpdatePosition(e.ScreenLocation, dragGrabOffset);
        var clientPoint = taskList.PointToClient(e.ScreenLocation);
        taskList.AutoScrollForDrag(clientPoint.Y);
        taskList.MoveDragPlaceholder(row, clientPoint.Y);
        lastInside = DateTime.Now;
    }

    private void TaskRowOnDragEnded(object? sender, TaskDragEventArgs e)
    {
        if (sender is not TaskRowControl row || row != activeDragRow)
        {
            return;
        }

        FinishTaskDrag(e.Canceled);
    }

    private void FinishTaskDrag(bool canceled)
    {
        var row = activeDragRow;
        var originalOrder = dragOriginalOrder?.ToArray();

        taskDragGhost?.Close();
        taskDragGhost?.Dispose();
        taskDragGhost = null;
        if (row is not null && !row.IsDisposed)
        {
            row.IsDragPlaceholder = false;
        }

        var finalOrder = taskList.Rows.Select(candidate => candidate.Task.Id).ToArray();
        var committed = !canceled && ApplyTaskOrder(finalOrder);
        if (!committed && originalOrder is not null)
        {
            taskList.ApplyOrder(originalOrder);
        }

        taskList.EndDrag();
        dragOriginalOrder = null;
        dragInProgress = false;
        activeDragRow = null;
        dragGrabOffset = Point.Empty;
        if (row is not null && !row.IsDisposed)
        {
            row.Focus();
        }
    }

    private bool ApplyTaskOrder(string[] order)
    {
        var activeIds = CurrentListTasks.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        if (order.Length != activeIds.Count || order.Any(id => !activeIds.Contains(id)))
        {
            return false;
        }

        var snapshot = state.Clone();
        for (var index = 0; index < order.Length; index += 1)
        {
            var task = state.Tasks.First(candidate => candidate.Id == order[index]);
            task.SortOrder = index;
        }

        if (currentView == MainView.Inbox)
        {
            state.NormalizeInboxSortOrders();
        }
        else
        {
            state.NormalizeSortOrders(SelectedDate);
        }
        if (SaveState())
        {
            return true;
        }

        state = snapshot;
        return false;
    }

    private void ClearTodayTasks()
    {
        if (!CurrentListTasks.Any())
        {
            return;
        }

        var title = currentView switch
        {
            MainView.Tomorrow => "清空明日计划",
            MainView.Inbox => "清空待办箱",
            _ => "清空今日任务"
        };
        var body = currentView switch
        {
            MainView.Tomorrow => "确定清空明天所有计划任务吗？",
            MainView.Inbox => "确定清空待办箱里的所有任务吗？",
            _ => "确定清空今天所有未完成任务吗？"
        };
        if (!ConfirmAction(title, body, "清空"))
        {
            return;
        }

        var snapshot = state.Clone();
        var idsToRemove = CurrentListTasks.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        if (focusedTaskId is not null && idsToRemove.Contains(focusedTaskId))
        {
            StopFocus(recordElapsed: false, silent: true);
        }
        state.Tasks.RemoveAll(task => idsToRemove.Contains(task.Id));
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        RenderScheduledTasks();
    }

    private void ClearHistory()
    {
        if (!state.HistoryTasks.Any())
        {
            return;
        }

        if (!ConfirmAction("清空完成记录", "确定清空所有已保存的完成记录吗？", "清空"))
        {
            return;
        }

        var snapshot = state.Clone();
        state.Tasks.RemoveAll(task => !task.IsActive);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        HideUndoBar();
        RefreshCompletedSection();
        MarkHistoryDirty();
        UpdateHeaderStats();
    }

    private System.Windows.Forms.Timer CreateRowAnimationTimer(int interval)
    {
        var timer = new System.Windows.Forms.Timer { Interval = interval };
        rowAnimationTimers.Add(timer);
        return timer;
    }

    private void DisposeRowAnimationTimer(System.Windows.Forms.Timer timer)
    {
        timer.Stop();
        rowAnimationTimers.Remove(timer);
        timer.Dispose();
    }

    private void CancelCompletedRowRemoval(string taskId)
    {
        if (!completionTimers.Remove(taskId, out var timer))
        {
            return;
        }

        DisposeRowAnimationTimer(timer);
    }

    private void CancelRowAnimationTimers()
    {
        foreach (var timer in rowAnimationTimers.ToArray())
        {
            timer.Stop();
            timer.Dispose();
        }

        rowAnimationTimers.Clear();
        completionTimers.Clear();
    }
}
