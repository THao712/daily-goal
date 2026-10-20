using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DailyGoal;

/// <summary>通用分段选择芯片：任意标签数量的单选，选中态用薄荷描边 + 强调文字（设置页多处复用）。</summary>
internal sealed class ChoiceChips : Control
{
    private string[] labels = [];
    private int selectedIndex;
    private int hoveredIndex = -1;

    public event EventHandler? SelectedChanged;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string[] Labels
    {
        get => labels;
        set
        {
            labels = value ?? [];
            selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, labels.Length - 1));
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(0, labels.Length - 1));
            if (selectedIndex != clamped)
            {
                selectedIndex = clamped;
                Invalidate();
            }
        }
    }

    public ChoiceChips()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.Selectable
            | ControlStyles.ResizeRedraw
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = UiTheme.WindowTop;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    private int IndexAt(int x)
    {
        if (labels.Length == 0)
        {
            return -1;
        }

        return Math.Clamp(x * labels.Length / Math.Max(1, Width), 0, labels.Length - 1);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var index = IndexAt(e.X);
        if (hoveredIndex != index)
        {
            hoveredIndex = index;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hoveredIndex = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Select(IndexAt(e.X));
        }
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Left or Keys.Up)
        {
            Select(Math.Max(0, selectedIndex - 1));
            e.Handled = true;
        }
        else if (e.KeyCode is Keys.Right or Keys.Down)
        {
            Select(Math.Min(labels.Length - 1, selectedIndex + 1));
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void Select(int index)
    {
        if (index >= 0 && index != selectedIndex)
        {
            selectedIndex = index;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(BackColor);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (labels.Length == 0)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var cellWidth = (float)Width / labels.Length;
        for (var index = 0; index < labels.Length; index += 1)
        {
            var rect = new RectangleF(index * cellWidth + 1f, 1f, cellWidth - 2f, Height - 2f);
            using var path = PaintHelpers.RoundRect(rect, UiTheme.RadiusMedium);
            var selected = index == selectedIndex;
            var fill = !Enabled
                ? UiTheme.SurfaceMuted
                : selected ? UiTheme.AccentSoft : index == hoveredIndex ? UiTheme.SurfaceHover : UiTheme.Surface;
            using (var brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
            }

            var border = !Enabled
                ? UiTheme.WithAlpha(UiTheme.Border, 130)
                : selected ? UiTheme.WithAlpha(UiTheme.Mint, 150) : UiTheme.Border;
            using (var pen = new Pen(border, selected && Enabled ? 1.4f : 1f))
            {
                e.Graphics.DrawPath(pen, path);
            }

            var textColor = !Enabled
                ? UiTheme.TextFaint
                : selected ? UiTheme.AccentText : UiTheme.TextMuted;
            TextRenderer.DrawText(
                e.Graphics,
                labels[index],
                UiTheme.SmallBoldFont,
                Rectangle.Round(rect),
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
        }

        if (Focused && ShowFocusCues)
        {
            using var focusPath = PaintHelpers.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), UiTheme.RadiusMedium);
            UiTheme.DrawFocusRing(e.Graphics, focusPath);
        }
    }
}
