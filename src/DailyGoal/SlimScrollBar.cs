using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace DailyGoal;

/// <summary>
/// 主题化细滚动条：语义与 VScrollBar 一致（Value ∈ [Minimum, Maximum-LargeChange+1]），
/// 支持拇指拖动与轨道点击跳转，悬停时拇指加粗。
/// </summary>
internal sealed class SlimScrollBar : Control
{
    private int minimum;
    private int maximum = 100;
    private int largeChange = 10;
    private int value;
    private bool hovered;
    private bool dragging;
    private int dragOffset;

    public event EventHandler? ValueChanged;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => minimum;
        set
        {
            minimum = value;
            Value = this.value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => maximum;
        set
        {
            maximum = Math.Max(minimum, value);
            Value = this.value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LargeChange
    {
        get => largeChange;
        set
        {
            largeChange = Math.Max(1, value);
            Value = this.value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SmallChange { get; set; } = 34;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            var clamped = Math.Clamp(value, minimum, MaxValue);
            if (this.value == clamped)
            {
                return;
            }

            this.value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int MaxValue => Math.Max(minimum, maximum - largeChange + 1);

    private int Range => Math.Max(1, maximum - minimum + 1);

    public SlimScrollBar()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        TabStop = false;
        Width = UiTheme.S(10);
        Cursor = Cursors.Default;
    }

    private Rectangle ThumbBounds
    {
        get
        {
            var track = ClientRectangle;
            if (track.Height <= 0 || Range <= largeChange)
            {
                return Rectangle.Empty;
            }

            var thumbHeight = Math.Max(UiTheme.S(30), (int)((float)track.Height * largeChange / Range));
            thumbHeight = Math.Min(thumbHeight, track.Height);
            var scrollable = Math.Max(1, MaxValue - minimum);
            var y = (int)((float)(value - minimum) / scrollable * (track.Height - thumbHeight));
            return new Rectangle(0, y, track.Width, thumbHeight);
        }
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
        if (!dragging)
        {
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            base.OnMouseDown(e);
            return;
        }

        var thumb = ThumbBounds;
        if (thumb.Contains(e.Location))
        {
            dragging = true;
            dragOffset = e.Y - thumb.Top;
            Capture = true;
        }
        else if (!thumb.IsEmpty)
        {
            dragging = true;
            dragOffset = thumb.Height / 2;
            Capture = true;
            MoveThumbTo(e.Y);
        }

        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging)
        {
            MoveThumbTo(e.Y);
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (dragging && e.Button == MouseButtons.Left)
        {
            dragging = false;
            Capture = false;
            Invalidate();
        }
        base.OnMouseUp(e);
    }

    private void MoveThumbTo(int mouseY)
    {
        var thumb = ThumbBounds;
        if (thumb.IsEmpty)
        {
            return;
        }

        var travel = Math.Max(1, ClientSize.Height - thumb.Height);
        var scrollable = Math.Max(1, MaxValue - minimum);
        var position = (float)(mouseY - dragOffset) / travel;
        Value = minimum + (int)MathF.Round(Math.Clamp(position, 0f, 1f) * scrollable);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        if (Parent is not null)
        {
            pevent.Graphics.Clear(Parent.BackColor);
        }
        else
        {
            pevent.Graphics.Clear(UiTheme.Canvas);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var thumb = ThumbBounds;
        if (thumb.IsEmpty)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var active = hovered || dragging;
        var width = active ? thumb.Width : Math.Max(3, thumb.Width - UiTheme.S(4));
        var rect = new RectangleF(thumb.Right - width, thumb.Top, width - 1f, thumb.Height);
        using var path = PaintHelpers.RoundRect(rect, Math.Min(rect.Width, UiTheme.Sf(5)));
        using var brush = new SolidBrush(UiTheme.WithAlpha(active ? UiTheme.TextMuted : UiTheme.TextFaint, active ? 190 : 120));
        e.Graphics.FillPath(brush, path);
    }
}
