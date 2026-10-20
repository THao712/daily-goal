using System.Drawing.Drawing2D;

namespace DailyGoal;

/// <summary>标题区左侧的拖动抓手：六个小点，提示展开态也可以按住标题拖动整窗。</summary>
internal sealed class WindowGrip : Control
{
    public WindowGrip()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = UiTheme.WindowTop;
        Cursor = Cursors.SizeAll;
        TabStop = false;
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(BackColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(UiTheme.WithAlpha(UiTheme.TextFaint, 168));
        var dot = UiTheme.Sf(2.6f);
        var columnGap = UiTheme.Sf(6.4f);
        var rowGap = UiTheme.Sf(6.8f);
        var centerX = Width / 2f;
        var centerY = Height / 2f;
        for (var row = -1; row <= 1; row += 1)
        {
            for (var column = -1; column <= 1; column += 2)
            {
                e.Graphics.FillEllipse(
                    brush,
                    centerX + (column * columnGap / 2f) - (dot / 2f),
                    centerY + (row * rowGap) - (dot / 2f),
                    dot,
                    dot);
            }
        }
    }
}
