using System.Globalization;

namespace DailyGoal;

internal sealed partial class DailyGoalForm
{
    private readonly FocusBannerControl focusBanner = new();
    private readonly System.Windows.Forms.Timer focusTimer = new();
    private string? focusedTaskId;
    private string focusedTaskText = "";
    private bool focusPaused;
    private bool focusCountUp;
    private int focusPlannedSeconds;
    private int focusElapsedSeconds;
    private bool focusBalloonPending;

    public bool IsFocusRunning => focusedTaskId is not null;
    public bool IsFocusPaused => focusPaused;
    public int FocusElapsedSeconds => focusElapsedSeconds;
    public string? FocusedTaskId => focusedTaskId;

    private void WireFocusBanner()
    {
        focusBanner.Visible = false;
        focusBanner.PauseResumeRequested += (_, _) => ToggleFocusPause();
        focusBanner.CompleteTaskRequested += (_, _) => CompleteFocusedTask();
        focusBanner.StopRequested += (_, _) => StopFocus(recordElapsed: true);
        todayView.Controls.Add(focusBanner);

        focusTimer.Interval = 1000;
        focusTimer.Tick += (_, _) => TickFocus();
    }

    internal void StartFocus(TaskRecord task, int minutes)
    {
        if (!task.IsActive)
        {
            return;
        }

        if (focusedTaskId is not null && focusedTaskId != task.Id)
        {
            RecordFocusProgress(forceSessionIncrement: false);
        }

        focusedTaskId = task.Id;
        focusedTaskText = task.Text;
        focusPaused = false;
        focusCountUp = minutes <= 0;
        focusPlannedSeconds = focusCountUp ? 0 : Math.Max(60, minutes * 60);
        focusElapsedSeconds = 0;
        focusBalloonPending = false;

        focusBanner.Visible = true;
        UpdateFocusVisuals();
        LayoutTodayListArea();
        focusTimer.Start();

        var modeText = focusCountUp ? "正计时专注" : $"{minutes} 分钟专注";
        ShowTransientStatus($"已开始{modeText}：{task.Text}");
    }

    internal void ToggleFocusPause()
    {
        if (focusedTaskId is null)
        {
            return;
        }

        focusPaused = !focusPaused;
        if (focusPaused)
        {
            focusTimer.Stop();
        }
        else
        {
            focusTimer.Start();
        }

        UpdateFocusVisuals();
    }

    internal void StopFocus(bool recordElapsed = true, bool silent = false)
    {
        if (focusedTaskId is null)
        {
            return;
        }

        focusTimer.Stop();
        var recordedMinutes = recordElapsed ? RecordFocusProgress(forceSessionIncrement: false) : 0;
        focusedTaskId = null;
        focusedTaskText = "";
        focusPaused = false;
        focusElapsedSeconds = 0;
        focusPlannedSeconds = 0;
        focusBanner.Visible = false;
        UpdateFocusVisuals();
        LayoutTodayListArea();

        if (!silent)
        {
            ShowTransientStatus(recordedMinutes > 0
                ? $"专注已结束，累计记录 {recordedMinutes} 分钟"
                : "专注已结束");
        }
    }

    private void CompleteFocusedTask()
    {
        if (focusedTaskId is null)
        {
            return;
        }

        var taskId = focusedTaskId;
        var task = state.Tasks.FirstOrDefault(candidate => candidate.Id == taskId && candidate.IsActive);
        StopFocus(recordElapsed: true, silent: true);
        if (task is null)
        {
            return;
        }

        if (task.IsInbox || task.Day == currentDate)
        {
            if (taskRows.TryGetValue(task.Id, out var row))
            {
                CompleteTask(task, row);
            }
            else
            {
                CompleteTaskWithoutRow(task);
            }
        }
        else
        {
            ShowTransientStatus("已记录专注时长（明日任务将在明天可勾选）");
        }
    }

    internal void TickFocus(int stepSeconds = 1)
    {
        if (focusedTaskId is null || focusPaused)
        {
            return;
        }

        focusElapsedSeconds += Math.Max(1, stepSeconds);
        if (!focusCountUp && focusElapsedSeconds >= focusPlannedSeconds)
        {
            FinishPlannedFocusSession();
            return;
        }

        UpdateFocusVisuals();
    }

    private void FinishPlannedFocusSession()
    {
        focusTimer.Stop();
        var title = focusedTaskText;
        var minutes = RecordFocusProgress(forceSessionIncrement: true);
        focusedTaskId = null;
        focusedTaskText = "";
        focusPaused = false;
        focusElapsedSeconds = 0;
        focusPlannedSeconds = 0;
        focusBanner.Visible = false;
        UpdateFocusVisuals();
        LayoutTodayListArea();

        PlayFeedbackSound(FeedbackSoundKind.FocusComplete);
        ShowTransientStatus($"专注完成：{title}（+{minutes} 分钟）");
        if (trayIcon is not null)
        {
            focusBalloonPending = true;
            trayIcon.BalloonTipTitle = "Daily Goal · 专注完成";
            trayIcon.BalloonTipText = $"已完成 {minutes} 分钟专注：{title}";
            trayIcon.ShowBalloonTip(7000);
        }
    }

    private int RecordFocusProgress(bool forceSessionIncrement)
    {
        if (focusedTaskId is null)
        {
            return 0;
        }

        var minutes = focusElapsedSeconds >= 60
            ? (int)Math.Round(focusElapsedSeconds / 60d, MidpointRounding.AwayFromZero)
            : (forceSessionIncrement ? Math.Max(1, focusPlannedSeconds / 60) : 0);
        if (minutes <= 0 && !forceSessionIncrement)
        {
            return 0;
        }

        var task = state.Tasks.FirstOrDefault(candidate => candidate.Id == focusedTaskId);
        if (task is null)
        {
            return 0;
        }

        task.FocusMinutes += Math.Max(1, minutes);
        task.FocusSessions += 1;
        _ = SaveState();
        if (taskRows.TryGetValue(task.Id, out var row) && !row.IsDisposed)
        {
            row.Invalidate();
        }

        return Math.Max(1, minutes);
    }

    private void UpdateFocusVisuals()
    {
        if (focusedTaskId is null)
        {
            peek.FocusActive = false;
            peek.FocusPaused = false;
            peek.FocusProgress = 0f;
            peek.FocusBadgeText = "";
            return;
        }

        string formatted;
        string railBadge;
        float progress;
        if (focusCountUp)
        {
            var span = TimeSpan.FromSeconds(focusElapsedSeconds);
            formatted = $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
            railBadge = span.TotalMinutes >= 1
                ? $"{Math.Min(99, (int)span.TotalMinutes)}m"
                : $"{span.Seconds}s";
            progress = Math.Clamp((focusElapsedSeconds % 1500) / 1500f, 0.04f, 1f);
        }
        else
        {
            var remaining = Math.Max(0, focusPlannedSeconds - focusElapsedSeconds);
            var span = TimeSpan.FromSeconds(remaining);
            formatted = $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
            railBadge = remaining >= 60
                ? $"{(int)Math.Ceiling(remaining / 60d)}m"
                : $"{remaining}s";
            progress = focusPlannedSeconds <= 0 ? 0f : (float)focusElapsedSeconds / focusPlannedSeconds;
        }

        focusBanner.TaskText = focusedTaskText;
        focusBanner.TimeText = formatted;
        focusBanner.Progress = progress;
        focusBanner.Paused = focusPaused;

        peek.FocusActive = true;
        peek.FocusPaused = focusPaused;
        peek.FocusProgress = progress;
        peek.FocusBadgeText = railBadge;
    }

    private void LayoutTodayListArea()
    {
        var bannerSpace = focusBanner.Visible ? UiTheme.S(64) : 0;
        if (focusBanner.Visible)
        {
            focusBanner.Location = new Point(UiTheme.S(32), UiTheme.S(80));
            focusBanner.Size = new Size(ExpandedWidth - UiTheme.S(64), UiTheme.S(54));
            focusBanner.BringToFront();
        }

        remainingLabel.Location = new Point(UiTheme.S(35), UiTheme.S(84) + bannerSpace);
        var listTop = UiTheme.S(116) + bannerSpace;
        var listHeight = Math.Max(UiTheme.S(120), todayView.Height - listTop - UiTheme.S(12));
        taskList.Location = new Point(UiTheme.S(22), listTop);
        taskList.Size = new Size(ExpandedWidth - UiTheme.S(44), listHeight);
        todayEmpty.Location = taskList.Location;
        todayEmpty.Size = taskList.Size;
    }

    private void CompleteTaskWithoutRow(TaskRecord task)
    {
        if (!task.IsActive)
        {
            return;
        }

        var snapshot = state.Clone();
        var previousSortOrder = task.SortOrder;
        var completedAt = clock.Now;
        task.CompletedAt = completedAt;
        state.NormalizeActiveSortOrders();
        state.PruneHistory(currentDate);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        MarkHistoryDirty();
        ShowUndo(new UndoCompletion(task.Id, previousSortOrder, completedAt), $"已完成“{task.Text}”");
        RenderScheduledTasks();
        UpdateHeaderStats();
        PlayCompletionFeedback();
    }
}
