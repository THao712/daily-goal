using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DailyGoal;

/// <summary>设置页的主题选择：三张迷你色卡（窗口底色 + 卡片 + 强调色点），选中卡带薄荷描边。</summary>
internal sealed class ThemeChoiceControl : Control
{
    private static readonly (AppThemeMode Mode, string Label)[] Items =
    [
        (AppThemeMode.Dark, "深色"),
        (AppThemeMode.Light, "浅色"),
        (AppThemeMode.Xuanling, "玄翎")
    ];

    private AppThemeMode selectedTheme = AppThemeMode.Dark;
    private int hoveredIndex = -1;

    public event EventHandler? SelectedChanged;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AppThemeMode SelectedTheme
    {
        get => selectedTheme;
        set
        {
            if (selectedTheme != value)
            {
                selectedTheme = value;
                Invalidate();
            }
        }
    }

    public ThemeChoiceControl()
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
        return Math.Clamp(x * Items.Length / Math.Max(1, Width), 0, Items.Length - 1);
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
            var mode = Items[IndexAt(e.X)].Mode;
            if (mode != selectedTheme)
            {
                selectedTheme = mode;
                Invalidate();
                SelectedChanged?.Invoke(this, EventArgs.Empty);
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
        var cellWidth = (float)Width / Items.Length;
        for (var index = 0; index < Items.Length; index += 1)
        {
            var (mode, label) = Items[index];
            var palette = UiTheme.PaletteFor(mode);
            var rect = new RectangleF(index * cellWidth + 3f, 1f, cellWidth - 6f, Height - 3f);
            using var path = PaintHelpers.RoundRect(rect, UiTheme.RadiusMedium);
            using (var background = new SolidBrush(palette.WindowTop))
            {
                e.Graphics.FillPath(background, path);
            }

            var previous = e.Graphics.Clip;
            e.Graphics.SetClip(path);
            var cardRect = new RectangleF(
                rect.Left + UiTheme.Sf(10),
                rect.Top + UiTheme.Sf(10),
                rect.Width - UiTheme.Sf(20),
                UiTheme.Sf(16));
            using (var cardPath = PaintHelpers.RoundRect(cardRect, UiTheme.Sf(4)))
            using (var card = new SolidBrush(palette.Surface))
            using (var cardPen = new Pen(palette.Border, 1f))
            {
                e.Graphics.FillPath(card, cardPath);
                e.Graphics.DrawPath(cardPen, cardPath);
            }

            var dotY = cardRect.Bottom + UiTheme.Sf(7);
            var dotSize = UiTheme.Sf(8);
            Color[] dots = [palette.Mint, palette.Coral, palette.Sky];
            for (var dot = 0; dot < dots.Length; dot += 1)
            {
                using var dotBrush = new SolidBrush(dots[dot]);
                e.Graphics.FillEllipse(
                    dotBrush,
                    cardRect.Left + dot * (dotSize + UiTheme.Sf(5)),
                    dotY,
                    dotSize,
                    dotSize);
            }

            TextRenderer.DrawText(
                e.Graphics,
                label,
                UiTheme.SmallBoldFont,
                new Rectangle((int)rect.Left, (int)(rect.Bottom - UiTheme.Sf(26)), (int)rect.Width, UiTheme.S(22)),
                palette.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
            e.Graphics.Clip = previous;

            var selected = mode == selectedTheme;
            using var borderPen = new Pen(
                selected ? UiTheme.Mint : index == hoveredIndex ? UiTheme.BorderStrong : UiTheme.Border,
                selected ? 2f : 1f);
            e.Graphics.DrawPath(borderPen, path);
        }

        if (Focused && ShowFocusCues)
        {
            using var focusPath = PaintHelpers.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), UiTheme.RadiusMedium);
            UiTheme.DrawFocusRing(e.Graphics, focusPath);
        }
    }
}
