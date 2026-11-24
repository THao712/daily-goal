using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DailyGoal;

internal sealed partial class DailyGoalForm
{
    private readonly HealingPanel reviewView = new();
    private readonly IconButton reviewBackButton = new();
    private readonly IconButton reviewCloseButton = new();
    private readonly Label reviewStatsLabel = new();
    private readonly Label reviewSubStatsLabel = new();
    private readonly SegmentButton reviewCopyMarkdownButton = new();
    private readonly SegmentButton reviewDeferAllButton = new();
    private readonly StackScrollPanel reviewList = new();
    private readonly EmptyStateControl reviewEmpty = new();

    private void BuildReviewView()
    {
        reviewView.Location = Point.Empty;
        reviewView.Size = new Size(ExpandedWidth, appHeight);
        reviewView.BackColor = UiTheme.WindowTop;
        reviewView.Visible = false;
        content.Controls.Add(reviewView);

        ConfigureIconButton(reviewBackButton, IconKind.Back, "返回", new Point(UiTheme.S(24), UiTheme.S(23)), new Size(UiTheme.S(42), UiTheme.S(42)));
        reviewBackButton.Click += (_, _) => HideDailyReview();
        reviewView.Controls.Add(reviewBackButton);

        var title = CreateLabel("今日复盘", new Point(UiTheme.S(76), UiTheme.S(22)), new Size(UiTheme.S(300), UiTheme.S(40)), UiTheme.TitleFont, UiTheme.Text);
        reviewView.Controls.Add(title);

        ConfigureIconButton(reviewCloseButton, IconKind.Close, "关闭", new Point(ExpandedWidth - UiTheme.S(48), UiTheme.S(24)), new Size(UiTheme.S(40), UiTheme.S(40)));
        reviewCloseButton.Tone = IconButtonTone.Danger;
        reviewCloseButton.Click += (_, _) => HandleCloseButtonClick();
        reviewView.Controls.Add(reviewCloseButton);

        reviewStatsLabel.Location = new Point(UiTheme.S(34), UiTheme.S(82));
        reviewStatsLabel.Size = new Size(ExpandedWidth - UiTheme.S(68), UiTheme.S(30));
        reviewStatsLabel.Font = UiTheme.HeadingFont;
        reviewStatsLabel.ForeColor = UiTheme.Text;
        reviewStatsLabel.BackColor = UiTheme.WindowTop;
        reviewView.Controls.Add(reviewStatsLabel);

        reviewSubStatsLabel.Location = new Point(UiTheme.S(34), UiTheme.S(114));
        reviewSubStatsLabel.Size = new Size(ExpandedWidth - UiTheme.S(68), UiTheme.S(26));
        reviewSubStatsLabel.Font = UiTheme.SmallFont;
        reviewSubStatsLabel.ForeColor = UiTheme.TextMuted;
        reviewSubStatsLabel.BackColor = UiTheme.WindowTop;
        reviewView.Controls.Add(reviewSubStatsLabel);

        reviewCopyMarkdownButton.Text = "复制今日日报";
        reviewCopyMarkdownButton.Active = true;
        reviewCopyMarkdownButton.Location = new Point(UiTheme.S(34), UiTheme.S(150));
        reviewCopyMarkdownButton.Size = new Size(UiTheme.S(146), UiTheme.S(42));
        reviewCopyMarkdownButton.Click += (_, _) => CopyDailyReportToClipboard();
        reviewView.Controls.Add(reviewCopyMarkdownButton);

        reviewDeferAllButton.Text = "剩余全部顺延明天";
        reviewDeferAllButton.Location = new Point(UiTheme.S(190), UiTheme.S(150));
        reviewDeferAllButton.Size = new Size(UiTheme.S(172), UiTheme.S(42));
        reviewDeferAllButton.Click += (_, _) => DeferAllTodayToTomorrow();
        reviewView.Controls.Add(reviewDeferAllButton);

        var sectionHint = CreateLabel(
            "整理今日剩余待办（快速决定顺延明天、放进待办箱或放弃）",
            new Point(UiTheme.S(34), UiTheme.S(204)),
            new Size(ExpandedWidth - UiTheme.S(68), UiTheme.S(24)),
            UiTheme.SmallFont,
            UiTheme.TextFaint);
        reviewView.Controls.Add(sectionHint);

        reviewList.Location = new Point(UiTheme.S(22), UiTheme.S(234));
        reviewList.Size = new Size(ExpandedWidth - UiTheme.S(44), Math.Max(UiTheme.S(180), appHeight - UiTheme.S(254)));
        reviewList.Padding = new Padding(UiTheme.S(7), UiTheme.S(4), UiTheme.S(7), UiTheme.S(8));
        reviewList.BackColor = UiTheme.WindowTop;
        reviewView.Controls.Add(reviewList);

        reviewEmpty.Title = "今日待办已全部清零";
        reviewEmpty.Subtitle = "干得漂亮，好好休息一下吧";
        reviewEmpty.BackColor = UiTheme.WindowTop;
        reviewEmpty.Location = reviewList.Location;
        reviewEmpty.Size = reviewList.Size;
        reviewView.Controls.Add(reviewEmpty);
    }

    internal void ShowDailyReview()
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

        RefreshReviewView();
        SetMainChromeVisible(false);
        reviewView.Visible = true;
        reviewView.BringToFront();
        HideUndoBar();
        SetExpandedState(true);
    }

    internal void HideDailyReview()
    {
        reviewView.Visible = false;
        SetMainChromeVisible(true);
        RenderScheduledTasks();
        undoBar.BringToFront();
    }

    private void RefreshReviewView()
    {
        var completedToday = CompletedTodayTasks.ToArray();
        var remainingToday = state.TasksForDate(currentDate).ToArray();
        var total = completedToday.Length + remainingToday.Length;
        var percent = total == 0 ? 100 : (int)Math.Round(100d * completedToday.Length / total);
        var focusMinutes = completedToday.Sum(task => task.FocusMinutes) + remainingToday.Sum(task => task.FocusMinutes);

        reviewStatsLabel.Text = total == 0
            ? "今天还没有安排任务"
            : $"今日已完成 {completedToday.Length} / {total} 项（{percent}%）";
        reviewSubStatsLabel.Text = focusMinutes > 0
            ? $"今日累计专注 {focusMinutes} 分钟 · 剩余 {remainingToday.Length} 项待整理"
            : $"剩余 {remainingToday.Length} 项待整理";
        reviewDeferAllButton.Visible = remainingToday.Length > 0;

        reviewList.SuspendLayout();
        DisposeChildren(reviewList);
        var rowWidth = Math.Max(UiTheme.S(350), reviewList.ClientSize.Width - reviewList.Padding.Horizontal - UiTheme.S(18));
        foreach (var task in remainingToday)
        {
            var row = new ReviewTriageRow(task)
            {
                Width = rowWidth,
                Height = UiTheme.S(64),
                Margin = new Padding(0, 0, 0, UiTheme.S(8))
            };
            row.DeferTomorrowRequested += (_, _) =>
            {
                MoveTaskToDay(task.Id, currentDate.AddDays(1), silentToast: true);
                RefreshReviewView();
            };
            row.MoveInboxRequested += (_, _) =>
            {
                MoveTaskToInbox(task.Id, silentToast: true);
                RefreshReviewView();
            };
            row.DropRequested += (_, _) =>
            {
                DeleteTask(task.Id);
                RefreshReviewView();
            };
            row.MouseWheel += (_, args) => reviewList.ScrollByWheel(args.Delta);
            reviewList.Controls.Add(row);
        }

        reviewList.ResumeLayout();
        reviewList.Relayout();

        var empty = remainingToday.Length == 0;
        reviewList.Visible = !empty;
        reviewEmpty.Visible = empty;
        if (empty)
        {
            reviewEmpty.BringToFront();
        }
    }

    internal int DeferAllTodayToTomorrow()
    {
        var remaining = state.TasksForDate(currentDate).ToArray();
        if (remaining.Length == 0)
        {
            return 0;
        }

        var snapshot = state.Clone();
        var tomorrow = currentDate.AddDays(1);
        var nextOrder = state.TasksForDate(tomorrow).Count();
        foreach (var task in remaining)
        {
            task.Day = tomorrow;
            task.IsInbox = false;
            task.CarryCount = 0;
            task.SortOrder = nextOrder++;
        }

        state.NormalizeActiveSortOrders();
        if (!SaveState())
        {
            state = snapshot;
            return 0;
        }

        RenderScheduledTasks();
        UpdateHeaderStats();
        if (reviewView.Visible)
        {
            RefreshReviewView();
        }

        ShowTransientStatus($"已将 {remaining.Length} 项任务顺延到明天");
        return remaining.Length;
    }

    private void CopyDailyReportToClipboard()
    {
        var markdown = ReportExporter.BuildDailyMarkdown(state, currentDate);
        TryCopyText(markdown, "已复制今日 Markdown 日报");
    }

    private void CopyWeeklyReportToClipboard()
    {
        var markdown = ReportExporter.BuildWeeklyMarkdown(state, currentDate, 7);
        TryCopyText(markdown, "已复制近 7 天 Markdown 周报");
    }

    private void ExportWeeklyReportFile()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "导出 Markdown 复盘报告",
            Filter = "Markdown 文件 (*.md)|*.md|文本文件 (*.txt)|*.txt",
            DefaultExt = "md",
            AddExtension = true,
            RestoreDirectory = true,
            FileName = $"daily-goal-report-{currentDate:yyyyMMdd}.md"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, ReportExporter.BuildWeeklyMarkdown(state, currentDate, 7));
            ShowTransientStatus("Markdown 周报已导出");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            ShowStorageWarning($"导出失败：{ex.Message}");
        }
    }

    private void TryCopyText(string text, string successMessage)
    {
        try
        {
            Clipboard.SetText(text);
            ShowTransientStatus(successMessage);
        }
        catch (ExternalException)
        {
            ShowTransientStatus("剪贴板被占用，请稍后再试", true);
        }
    }

    /// <summary>日终复盘里的单行整理卡片：任务文本 + 顺延明天 / 待办箱 / 删除 快捷按钮。</summary>
    private sealed class ReviewTriageRow : Control
    {
        private enum HoverZone
        {
            None,
            Tomorrow,
            Inbox,
            Drop
        }

        private readonly TaskRecord task;
        private HoverZone hovered;

        public event EventHandler? DeferTomorrowRequested;
        public event EventHandler? MoveInboxRequested;
        public event EventHandler? DropRequested;

        private Rectangle DropBounds => new(Width - UiTheme.S(58), UiTheme.S(16), UiTheme.S(48), UiTheme.S(32));
        private Rectangle InboxBounds => new(Width - UiTheme.S(126), UiTheme.S(16), UiTheme.S(62), UiTheme.S(32));
        private Rectangle TomorrowBounds => new(Width - UiTheme.S(192), UiTheme.S(16), UiTheme.S(60), UiTheme.S(32));

        public ReviewTriageRow(TaskRecord task)
        {
            this.task = task;
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer,
                true);
            BackColor = UiTheme.WindowTop;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var next = DropBounds.Contains(e.Location)
                ? HoverZone.Drop
                : InboxBounds.Contains(e.Location)
                    ? HoverZone.Inbox
                    : TomorrowBounds.Contains(e.Location)
                        ? HoverZone.Tomorrow
                        : HoverZone.None;
            if (next != hovered)
            {
                hovered = next;
                Cursor = next == HoverZone.None ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hovered != HoverZone.None)
            {
                hovered = HoverZone.None;
                Cursor = Cursors.Default;
                Invalidate();
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (TomorrowBounds.Contains(e.Location))
                {
                    DeferTomorrowRequested?.Invoke(this, EventArgs.Empty);
                }
                else if (InboxBounds.Contains(e.Location))
                {
                    MoveInboxRequested?.Invoke(this, EventArgs.Empty);
                }
                else if (DropBounds.Contains(e.Location))
                {
                    DropRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(BackColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var card = new RectangleF(2f, 2f, Width - 4f, Height - 4f);
            using var path = PaintHelpers.RoundRect(card, UiTheme.RadiusMedium);
            using (var fill = new SolidBrush(UiTheme.Surface))
            {
                e.Graphics.FillPath(fill, path);
            }

            using (var border = new Pen(UiTheme.Border, 1f))
            {
                e.Graphics.DrawPath(border, path);
            }

            using (var bar = new SolidBrush(UiTheme.Accent(task.AccentIndex)))
            {
                e.Graphics.FillRectangle(bar, UiTheme.S(8), UiTheme.S(12), UiTheme.S(4), Height - UiTheme.S(24));
            }

            var textRect = new Rectangle(UiTheme.S(22), 0, Math.Max(UiTheme.S(80), TomorrowBounds.Left - UiTheme.S(30)), Height);
            TextRenderer.DrawText(
                e.Graphics,
                task.Text,
                UiTheme.BodyFont,
                textRect,
                UiTheme.Text,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

            DrawAction(e.Graphics, TomorrowBounds, "明天", hovered == HoverZone.Tomorrow, UiTheme.Mint);
            DrawAction(e.Graphics, InboxBounds, "待办箱", hovered == HoverZone.Inbox, UiTheme.Sky);
            DrawAction(e.Graphics, DropBounds, "放弃", hovered == HoverZone.Drop, UiTheme.Danger);
        }

        private static void DrawAction(Graphics graphics, Rectangle bounds, string text, bool hot, Color accent)
        {
            using var path = PaintHelpers.RoundRect(bounds, UiTheme.RadiusSmall);
            using var fill = new SolidBrush(hot ? UiTheme.WithAlpha(accent, 38) : UiTheme.SurfaceMuted);
            graphics.FillPath(fill, path);
            using var border = new Pen(hot ? accent : UiTheme.Border, 1f);
            graphics.DrawPath(border, path);
            TextRenderer.DrawText(
                graphics,
                text,
                UiTheme.SmallBoldFont,
                bounds,
                hot ? accent : UiTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
