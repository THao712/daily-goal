using System.Drawing.Drawing2D;

namespace DailyGoal;

/// <summary>周一到周日的多选芯片（每周重复规则用），至少保留一个选中项。</summary>
internal sealed class WeekdayPicker : Control
{
    private static readonly DayOfWeek[] Order =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    private static readonly string[] Names = ["一", "二", "三", "四", "五", "六", "日"];

    private readonly HashSet<DayOfWeek> selected = [DayOfWeek.Monday];
    private int hoveredIndex = -1;

    public event EventHandler? SelectionChanged;

    public IReadOnlyCollection<DayOfWeek> Selected => selected;

    public void SetSelection(IEnumerable<DayOfWeek> days)
    {
        selected.Clear();
        foreach (var day in days)
        {
            selected.Add(day);
        }

        if (selected.Count == 0)
        {
            selected.Add(DayOfWeek.Monday);
        }

        Invalidate();
    }

    public WeekdayPicker()
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
        return Math.Clamp(x * Order.Length / Math.Max(1, Width), 0, Order.Length - 1);
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
            Toggle(Order[IndexAt(e.X)]);
        }
        base.OnMouseUp(e);
    }

    private void Toggle(DayOfWeek day)
    {
        if (selected.Contains(day))
        {
            if (selected.Count == 1)
            {
                return;
            }

            selected.Remove(day);
        }
        else
        {
            selected.Add(day);
        }

        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(BackColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var cellWidth = (float)Width / Order.Length;
        for (var index = 0; index < Order.Length; index += 1)
        {
            var rect = new RectangleF(index * cellWidth + 2f, 1f, cellWidth - 4f, Height - 2f);
            using var path = PaintHelpers.RoundRect(rect, UiTheme.RadiusSmall);
            var isSelected = selected.Contains(Order[index]);
            using (var brush = new SolidBrush(
                isSelected ? UiTheme.Mint : index == hoveredIndex ? UiTheme.SurfaceHover : UiTheme.Surface))
            {
                e.Graphics.FillPath(brush, path);
            }

            using (var pen = new Pen(isSelected ? UiTheme.Mint : UiTheme.Border, 1f))
            {
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Names[index],
                UiTheme.SmallBoldFont,
                Rectangle.Round(rect),
                isSelected ? UiTheme.OnAccent(UiTheme.Mint) : UiTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
        }
    }
}
