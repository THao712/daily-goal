using System.Globalization;
using System.Runtime.InteropServices;

namespace DailyGoal;

internal sealed partial class DailyGoalForm
{
    private DateOnly SelectedDate => currentView == MainView.Tomorrow ? currentDate.AddDays(1) : currentDate;

    private IEnumerable<TaskRecord> CompletedTodayTasks => state.HistoryTasks.Where(task =>
        task.CompletedAt is not null
        && DateOnly.FromDateTime(task.CompletedAt.Value.LocalDateTime) == currentDate);

    private void RenderToday()
    {
        RenderScheduledTasks();
    }

    private void RenderScheduledTasks()
    {
        CancelRowAnimationTimers();
        taskRows.Clear();
        taskList.SuspendLayout();
        DisposeChildren(taskList);

        var rowWidth = Math.Max(UiTheme.S(350), taskList.ClientSize.Width - taskList.Padding.Horizontal - UiTheme.S(18));
        foreach (var task in state.TasksForDate(SelectedDate))
        {
            var row = CreateTaskRow(task, rowWidth);
            taskRows[task.Id] = row;
            taskList.Controls.Add(row);
        }

        taskList.ResumeLayout();
        taskList.ArrangeRows(true);
        RefreshCompletedSection();
        UpdateHeaderStats();
    }

    /// <summary>重建今日列表底部的「已完成」分组（明日视图没有该分组）。</summary>
    private void RefreshCompletedSection()
    {
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

        UpdateTodayEmptyState();
    }

    private void ToggleCompletedSection()
    {
        state.Settings.CompletedSectionCollapsed = !state.Settings.CompletedSectionCollapsed;
        _ = stateStore.Save(state, out _);
        RefreshCompletedSection();
    }

    /// <summary>今日视图在“无待办且无完成”时才显示空状态插画；明日视图只看待办。</summary>
    private void UpdateTodayEmptyState()
    {
        var showEmpty = taskRows.Count == 0
            && (currentView != MainView.Today || !CompletedTodayTasks.Any());
        taskList.Visible = !showEmpty;
        todayEmpty.Visible = showEmpty;
        if (showEmpty)
        {
            todayEmpty.BringToFront();
        }
    }

    private TaskRowControl CreateTaskRow(TaskRecord task, int rowWidth)
    {
        var row = new TaskRowControl(task)
        {
            Width = rowWidth,
            Height = UiTheme.S(82),
            Margin = new Padding(0, 0, 0, UiTheme.S(10)),
            CompletionEnabled = task.Day == currentDate
        };
        row.CompleteRequested += (_, _) => CompleteTask(task, row);
        row.MoreRequested += (_, _) => ShowTaskMenu(task, row);
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
        if (task.Day != SelectedDate || currentView == MainView.History || taskRows.ContainsKey(task.Id))
        {
            return;
        }
        var rowWidth = Math.Max(UiTheme.S(350), taskList.ClientSize.Width - taskList.Padding.Horizontal - UiTheme.S(18));
        var row = CreateTaskRow(task, rowWidth);
        taskRows[task.Id] = row;
        taskList.Controls.Add(row);
        taskList.ApplyOrder(state.TasksForDate(SelectedDate).Select(candidate => candidate.Id), true);
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
        taskList.ApplyOrder(state.TasksForDate(SelectedDate).Select(candidate => candidate.Id), true);
        UpdateTodayEmptyState();
    }

    private void RenderHistory()
    {
        historyList.SuspendLayout();
        DisposeChildren(historyList);

        var history = state.HistoryTasks.ToArray();
        var rowWidth = Math.Max(UiTheme.S(350), historyList.ClientSize.Width - historyList.Padding.Horizontal - UiTheme.S(18));
        foreach (var group in history.GroupBy(task => DateOnly.FromDateTime(task.CompletedAt!.Value.LocalDateTime)))
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
        var empty = history.Length == 0;
        historyList.Visible = !empty;
        historyEmpty.Visible = empty;
        if (empty)
        {
            historyEmpty.BringToFront();
        }

        var countsByDay = history
            .Where(task => task.CompletedAt is not null)
            .GroupBy(task => DateOnly.FromDateTime(task.CompletedAt!.Value.LocalDateTime))
            .ToDictionary(group => group.Key, group => group.Count());

        var counts = new int[7];
        for (var offset = 0; offset < 7; offset += 1)
        {
            counts[offset] = countsByDay.GetValueOrDefault(currentDate.AddDays(offset - 6), 0);
        }

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

    private void AddTask()
    {
        var text = taskInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            taskInput.Clear();
            return;
        }
        if (AddTaskToDate(text, SelectedDate))
        {
            taskInput.Clear();
            taskInput.Focus();
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
        foreach (var line in lines.Reverse())
        {
            var text = line.Length > 160 ? line[..160] : line;
            if (AddTaskToDate(text, SelectedDate))
            {
                added += 1;
            }
        }

        return added;
    }

    private void CompleteTask(TaskRecord task, TaskRowControl row)
    {
        if (!task.IsActive || task.Day != currentDate || row.CompletedVisual)
        {
            return;
        }

        var snapshot = state.Clone();
        var previousSortOrder = task.SortOrder;
        var completedAt = clock.Now;
        task.CompletedAt = completedAt;
        state.NormalizeSortOrders(currentDate);
        state.PruneHistory(currentDate);
        if (!SaveState())
        {
            state = snapshot;
            RenderScheduledTasks();
            return;
        }

        row.CompletedVisual = true;
        row.Pulse = state.Settings.ReducedMotion ? 0f : 1f;
        row.CheckPulse = state.Settings.ReducedMotion ? 0f : 1f;
        row.StrikeProgress = state.Settings.ReducedMotion ? 1f : 0f;
        if (!state.Settings.ReducedMotion)
        {
            StartRowPulse(row);
            LaunchParticles(row, task);
        }

        ShowUndo(new UndoCompletion(task.Id, previousSortOrder, completedAt), $"已完成“{task.Text}”");
        UpdateHeaderStats();
        RenderHistory();
        ScheduleCompletedRowRemoval(task.Id, row);
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
        undoBar.Message = message;
        undoBar.Visible = true;
        undoBar.BringToFront();
        undoTimer.Stop();
        undoTimer.Start();
    }

    private void HideUndoBar()
    {
        undoTimer.Stop();
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
        foreach (var active in state.TasksForDate(currentDate).Where(candidate => candidate.SortOrder >= entry.PreviousSortOrder))
        {
            active.SortOrder += 1;
        }

        task.CompletedAt = null;
        task.Day = currentDate;
        task.SortOrder = entry.PreviousSortOrder;
        state.NormalizeSortOrders(currentDate);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        CancelCompletedRowRemoval(task.Id);
        HideUndoBar();
        RenderToday();
        RenderHistory();
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
            foreach (var active in state.TasksForDate(restored.Day).Where(candidate => candidate.SortOrder >= restored.SortOrder))
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

        state.NormalizeSortOrders(restored.Day);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        HideUndoBar();
        RenderScheduledTasks();
        RenderHistory();
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
        RenderHistory();
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
        RenderHistory();
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

        var deleted = task.Clone();
        state.Tasks.Remove(task);

        state.NormalizeSortOrders(task.Day);
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
        var active = state.TasksForDate(SelectedDate).ToList();
        var index = active.FindIndex(task => task.Id == taskId);
        var targetIndex = index + Math.Sign(direction);
        if (index < 0 || targetIndex < 0 || targetIndex >= active.Count)
        {
            return;
        }

        var snapshot = state.Clone();
        (active[index].SortOrder, active[targetIndex].SortOrder) = (active[targetIndex].SortOrder, active[index].SortOrder);
        state.NormalizeSortOrders(SelectedDate);
        if (!SaveState())
        {
            state = snapshot;
        }

        taskList.ApplyOrder(state.TasksForDate(SelectedDate).Select(task => task.Id), true);
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
        var activeIds = state.TasksForDate(SelectedDate).Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
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

        state.NormalizeSortOrders(SelectedDate);
        if (SaveState())
        {
            return true;
        }

        state = snapshot;
        return false;
    }

    private void ClearTodayTasks()
    {
        var selectedDate = SelectedDate;
        if (!state.TasksForDate(selectedDate).Any())
        {
            return;
        }

        var confirmed = ConfirmAction(
            currentView == MainView.Tomorrow ? "清空明日计划" : "清空今日任务",
            currentView == MainView.Tomorrow ? "确定清空明天所有计划任务吗？" : "确定清空今天所有未完成任务吗？",
            "清空");
        if (!confirmed)
        {
            return;
        }

        var snapshot = state.Clone();
        state.Tasks.RemoveAll(task => task.IsActive && task.Day == selectedDate);
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

        if (!ConfirmAction("清空完成记录", "确定清空最近 30 天的完成记录吗？", "清空"))
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
        RenderHistory();
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
