using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DailyGoal;

/// <summary>短暂状态提示：圆角卡片 + 左侧色条（普通为薄荷，错误为警示红），替代方框 Label。</summary>
internal sealed class StatusToast : Control
{
    private string message = "";
    private bool isError;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Message
    {
        get => message;
        set
        {
            message = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsError
    {
        get => isError;
        set
        {
            isError = value;
            Invalidate();
        }
    }

    public StatusToast()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = UiTheme.WindowTop;
        TabStop = false;
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(BackColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var accent = IsError ? UiTheme.Danger : UiTheme.Mint;
        var rect = new RectangleF(1f, 1f, Width - 2f, Height - 3f);
        using var path = PaintHelpers.RoundRect(rect, UiTheme.RadiusMedium);
        using var shadowPath = PaintHelpers.RoundRect(new RectangleF(2f, 4f, Width - 4f, Height - 4f), UiTheme.RadiusMedium);
        using (var shadow = new SolidBrush(UiTheme.CardShadow))
        {
            e.Graphics.FillPath(shadow, shadowPath);
        }

        using (var fill = new SolidBrush(UiTheme.Surface))
        {
            e.Graphics.FillPath(fill, path);
        }

        using (var pen = new Pen(UiTheme.WithAlpha(accent, 130), 1f))
        {
            e.Graphics.DrawPath(pen, path);
        }

        PaintHelpers.DrawTopHighlight(e.Graphics, rect, UiTheme.RadiusMedium);
        using var barPath = PaintHelpers.RoundRect(
            new RectangleF(rect.Left + UiTheme.Sf(7), rect.Top + UiTheme.Sf(9), UiTheme.Sf(4), rect.Height - UiTheme.Sf(18)),
            UiTheme.Sf(2));
        using (var bar = new SolidBrush(UiTheme.WithAlpha(accent, 220)))
        {
            e.Graphics.FillPath(bar, barPath);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Message,
            UiTheme.SmallBoldFont,
            new Rectangle(UiTheme.S(22), 0, Width - UiTheme.S(34), Height),
            IsError ? UiTheme.Danger : UiTheme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }
}
