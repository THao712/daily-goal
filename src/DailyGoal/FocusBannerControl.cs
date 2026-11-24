using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DailyGoal;

/// <summary>
/// 展开态列表顶部的专注计时横幅：显示当前专注任务、倒计时/正计时、进度条与暂停/完成/结束操作。
/// </summary>
internal sealed class FocusBannerControl : Control
{
    private enum HoverTarget
    {
        None,
        Pause,
        Complete,
        Stop
    }

    private string taskText = "";
    private string timeText = "25:00";
    private float progress;
    private bool paused;
    private HoverTarget hovered;

    public event EventHandler? PauseResumeRequested;
    public event EventHandler? CompleteTaskRequested;
    public event EventHandler? StopRequested;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TaskText
    {
        get => taskText;
        set
        {
            if (!string.Equals(taskText, value, StringComparison.Ordinal))
            {
                taskText = value;
                Invalidate();
            }
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TimeText
    {
        get => timeText;
        set
        {
            if (!string.Equals(timeText, value, StringComparison.Ordinal))
            {
                timeText = value;
                Invalidate();
            }
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Progress
    {
        get => progress;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(progress - clamped) > 0.002f)
            {
                progress = clamped;
                Invalidate();
            }
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Paused
    {
        get => paused;
        set
        {
            if (paused != value)
            {
                paused = value;
                Invalidate();
            }
        }
    }

    private Rectangle StopBounds => new(Width - UiTheme.S(60), UiTheme.S(11), UiTheme.S(50), UiTheme.S(30));
    private Rectangle CompleteBounds => new(Width - UiTheme.S(116), UiTheme.S(11), UiTheme.S(50), UiTheme.S(30));
    private Rectangle PauseBounds => new(Width - UiTheme.S(172), UiTheme.S(11), UiTheme.S(50), UiTheme.S(30));

    public FocusBannerControl()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = UiTheme.Canvas;
        TabStop = false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hit = StopBounds.Contains(e.Location)
            ? HoverTarget.Stop
            : CompleteBounds.Contains(e.Location)
                ? HoverTarget.Complete
                : PauseBounds.Contains(e.Location)
                    ? HoverTarget.Pause
                    : HoverTarget.None;
        if (hit != hovered)
        {
            hovered = hit;
            Cursor = hit == HoverTarget.None ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (hovered != HoverTarget.None)
        {
            hovered = HoverTarget.None;
            Cursor = Cursors.Default;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (PauseBounds.Contains(e.Location))
            {
                PauseResumeRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (CompleteBounds.Contains(e.Location))
            {
                CompleteTaskRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (StopBounds.Contains(e.Location))
            {
                StopRequested?.Invoke(this, EventArgs.Empty);
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
        var accent = paused ? UiTheme.TextMuted : UiTheme.Sun;
        var rect = new RectangleF(1f, 1f, Width - 2f, Height - 3f);
        using var path = PaintHelpers.RoundRect(rect, UiTheme.RadiusMedium);
        using (var fill = new SolidBrush(UiTheme.Surface))
        {
            e.Graphics.FillPath(fill, path);
        }

        using (var border = new Pen(UiTheme.WithAlpha(accent, 150), 1.2f))
        {
            e.Graphics.DrawPath(border, path);
        }

        // 左侧色条 + 专注图标
        using (var strip = PaintHelpers.RoundRect(
            new RectangleF(rect.Left + UiTheme.Sf(6), rect.Top + UiTheme.Sf(8), UiTheme.Sf(4), rect.Height - UiTheme.Sf(16)),
            UiTheme.Sf(2)))
        using (var stripBrush = new SolidBrush(accent))
        {
            e.Graphics.FillPath(stripBrush, strip);
        }

        var iconRect = new RectangleF(UiTheme.Sf(18), (Height - UiTheme.Sf(20)) / 2f - UiTheme.Sf(2), UiTheme.Sf(20), UiTheme.Sf(20));
        IconPainter.Draw(e.Graphics, IconKind.Focus, iconRect, accent, 1.7f);

        // 倒计时数字
        var timeRect = new Rectangle(UiTheme.S(44), UiTheme.S(8), UiTheme.S(76), UiTheme.S(34));
        TextRenderer.DrawText(
            e.Graphics,
            timeText,
            UiTheme.HeadingFont,
            timeRect,
            UiTheme.Text,
            TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

        // 任务名
        var labelLeft = timeRect.Right + UiTheme.S(6);
        var labelWidth = Math.Max(UiTheme.S(60), PauseBounds.Left - labelLeft - UiTheme.S(8));
        TextRenderer.DrawText(
            e.Graphics,
            paused ? $"已暂停 · {taskText}" : taskText,
            UiTheme.SmallBoldFont,
            new Rectangle(labelLeft, UiTheme.S(10), labelWidth, UiTheme.S(30)),
            UiTheme.TextMuted,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

        DrawPillButton(e.Graphics, PauseBounds, paused ? "继续" : "暂停", hovered == HoverTarget.Pause, UiTheme.Sky);
        DrawPillButton(e.Graphics, CompleteBounds, "完成", hovered == HoverTarget.Complete, UiTheme.Mint);
        DrawPillButton(e.Graphics, StopBounds, "结束", hovered == HoverTarget.Stop, UiTheme.Danger);

        // 底部细进度条
        var trackLeft = rect.Left + UiTheme.Sf(14);
        var trackWidth = Math.Max(0f, rect.Width - UiTheme.Sf(28));
        var trackY = rect.Bottom - UiTheme.Sf(5);
        using (var trackPath = PaintHelpers.RoundRect(new RectangleF(trackLeft, trackY, trackWidth, UiTheme.Sf(3)), UiTheme.Sf(1.5f)))
        using (var trackBrush = new SolidBrush(UiTheme.WithAlpha(UiTheme.Border, 120)))
        {
            e.Graphics.FillPath(trackBrush, trackPath);
        }

        if (progress > 0.005f)
        {
            var fillWidth = Math.Max(UiTheme.Sf(4), trackWidth * progress);
            using var progressPath = PaintHelpers.RoundRect(new RectangleF(trackLeft, trackY, fillWidth, UiTheme.Sf(3)), UiTheme.Sf(1.5f));
            using var progressBrush = new SolidBrush(UiTheme.Blend(UiTheme.Sun, UiTheme.Mint, progress));
            e.Graphics.FillPath(progressBrush, progressPath);
        }
    }

    private static void DrawPillButton(Graphics graphics, Rectangle bounds, string text, bool hot, Color accent)
    {
        using var path = PaintHelpers.RoundRect(bounds, UiTheme.RadiusSmall);
        using var fill = new SolidBrush(hot ? UiTheme.WithAlpha(accent, 42) : UiTheme.SurfaceMuted);
        graphics.FillPath(fill, path);
        using var pen = new Pen(hot ? accent : UiTheme.Border, 1f);
        graphics.DrawPath(pen, path);
        TextRenderer.DrawText(
            graphics,
            text,
            UiTheme.SmallBoldFont,
            bounds,
            hot ? accent : UiTheme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
