using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace DailyGoal;

/// <summary>今日列表底部「已完成」分组的折叠头：完成数 + 折叠箭头，点击整行切换。</summary>
internal sealed class CompletedSectionHeader : Control
{
    private bool collapsed;
    private bool hovered;

    public event EventHandler? ToggleRequested;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CompletedCount { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Collapsed
    {
        get => collapsed;
        set
        {
            if (collapsed != value)
            {
                collapsed = value;
                Invalidate();
            }
        }
    }

    public CompletedSectionHeader()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.Selectable
            | ControlStyles.ResizeRedraw
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = UiTheme.Canvas;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleName = "已完成任务分组";
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
        {
            ToggleRequested?.Invoke(this, EventArgs.Empty);
        }
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Return or Keys.Space)
        {
            ToggleRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(BackColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var textColor = hovered ? UiTheme.TextMuted : UiTheme.TextFaint;
        var chevronBounds = new RectangleF(UiTheme.Sf(8), Height / 2f - UiTheme.Sf(7), UiTheme.Sf(14), UiTheme.Sf(14));

        // 折叠时箭头指右（内容被收起），展开时指下。
        var state = e.Graphics.Save();
        e.Graphics.TranslateTransform(chevronBounds.Left + chevronBounds.Width / 2f, chevronBounds.Top + chevronBounds.Height / 2f);
        e.Graphics.RotateTransform(Collapsed ? 180f : 90f);
        e.Graphics.TranslateTransform(-(chevronBounds.Left + chevronBounds.Width / 2f), -(chevronBounds.Top + chevronBounds.Height / 2f));
        IconPainter.Draw(e.Graphics, IconKind.Expand, chevronBounds, textColor, 1.7f);
        e.Graphics.Restore(state);

        var label = $"已完成 · {CompletedCount}";
        TextRenderer.DrawText(
            e.Graphics,
            label,
            UiTheme.SmallBoldFont,
            new Rectangle(UiTheme.S(28), 0, Width - UiTheme.S(36), Height),
            textColor,
            TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

        var textWidth = TextRenderer.MeasureText(label, UiTheme.SmallBoldFont).Width;
        var lineStart = UiTheme.S(36) + textWidth;
        if (lineStart < Width - UiTheme.S(14))
        {
            using var pen = new Pen(UiTheme.WithAlpha(UiTheme.Border, 190), 1f);
            e.Graphics.DrawLine(pen, lineStart, Height / 2f, Width - UiTheme.S(10), Height / 2f);
        }

        if (Focused && ShowFocusCues)
        {
            using var path = PaintHelpers.RoundRect(new RectangleF(1f, 1f, Width - 2f, Height - 2f), UiTheme.RadiusSmall);
            UiTheme.DrawFocusRing(e.Graphics, path);
        }
    }
}

/// <summary>今日列表里的已完成行：灰显卡片 + 已勾选框（点击撤销完成）+ 完成时间。</summary>
internal sealed class CompletedRowControl : Control
{
    private bool hovered;
    private bool checkHovered;

    public TaskRecord Task { get; }

    public event EventHandler? RestoreRequested;

    private Rectangle CheckBounds => new(UiTheme.S(18), Height / 2 - UiTheme.S(13), UiTheme.S(26), UiTheme.S(26));

    public CompletedRowControl(TaskRecord task)
    {
        Task = task;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.Selectable
            | ControlStyles.ResizeRedraw
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = UiTheme.Canvas;
        TabStop = true;
        AccessibleName = $"已完成：{task.Text}";
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var overCheck = CheckBounds.Contains(e.Location);
        if (checkHovered != overCheck)
        {
            checkHovered = overCheck;
            Invalidate();
        }
        Cursor = overCheck ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        checkHovered = false;
        Cursor = Cursors.Default;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && CheckBounds.Contains(e.Location))
        {
            RestoreRequested?.Invoke(this, EventArgs.Empty);
        }
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Return or Keys.Space)
        {
            RestoreRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(BackColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(4f, 2f, Width - 9f, Height - 5f);
        using var path = PaintHelpers.RoundRect(rect, UiTheme.RadiusMedium);
        using (var fill = new SolidBrush(hovered ? UiTheme.SurfaceHover : UiTheme.SurfaceMuted))
        {
            e.Graphics.FillPath(fill, path);
        }

        using (var pen = new Pen(UiTheme.WithAlpha(UiTheme.Border, 170), 1f))
        {
            e.Graphics.DrawPath(pen, path);
        }

        var check = CheckBounds;
        using (var checkPath = PaintHelpers.RoundRect(
            new RectangleF(check.Left + 0.5f, check.Top + 0.5f, check.Width - 1, check.Height - 1),
            UiTheme.Sf(7)))
        {
            using var checkFill = new SolidBrush(checkHovered ? UiTheme.WithAlpha(UiTheme.Mint, 200) : UiTheme.WithAlpha(UiTheme.Mint, 150));
            e.Graphics.FillPath(checkFill, checkPath);
        }

        using (var checkPen = new Pen(UiTheme.OnAccent(UiTheme.Mint), UiTheme.Sf(2.1f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            var scale = check.Width / 32f;
            e.Graphics.DrawLines(checkPen,
            [
                new PointF(check.Left + 8f * scale, check.Top + 16.5f * scale),
                new PointF(check.Left + 13.5f * scale, check.Top + 21.5f * scale),
                new PointF(check.Left + 24.5f * scale, check.Top + 10f * scale)
            ]);
        }

        var timeText = Task.CompletedAt?.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture) ?? "";
        var timeWidth = UiTheme.S(56);
        var textBounds = new Rectangle(
            check.Right + UiTheme.S(12),
            0,
            Math.Max(40, Width - check.Right - UiTheme.S(24) - timeWidth),
            Height);
        TextRenderer.DrawText(
            e.Graphics,
            Task.Text,
            UiTheme.BodyFont,
            textBounds,
            UiTheme.TextFaint,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
        var textSize = TextRenderer.MeasureText(Task.Text, UiTheme.BodyFont, textBounds.Size, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        var strikeY = Height / 2f;
        using (var strikePen = new Pen(UiTheme.TextFaint, 1.2f))
        {
            e.Graphics.DrawLine(
                strikePen,
                textBounds.Left,
                strikeY,
                textBounds.Left + Math.Min(textBounds.Width, textSize.Width),
                strikeY);
        }

        TextRenderer.DrawText(
            e.Graphics,
            timeText,
            UiTheme.SmallFont,
            new Rectangle(Width - timeWidth - UiTheme.S(12), 0, timeWidth, Height),
            UiTheme.TextFaint,
            TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

        if (Focused && ShowFocusCues)
        {
            UiTheme.DrawFocusRing(e.Graphics, path);
        }
    }
}
