using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DailyGoal;

internal sealed class CharacterBadgeControl : Control
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? CharacterImage { get; set; }

    public CharacterBadgeControl()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        BackColor = UiTheme.WindowTop;
        TabStop = false;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (UiTheme.Mode != AppThemeMode.Xuanling || CharacterImage is null)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var imageSize = Math.Min(Width - 4, Height + 18);
        var bounds = new Rectangle((Width - imageSize) / 2, -7, imageSize, imageSize);
        using var glow = new SolidBrush(UiTheme.WithAlpha(UiTheme.Sky, 22));
        e.Graphics.FillEllipse(glow, Rectangle.Inflate(bounds, 4, 2));
        e.Graphics.DrawImage(CharacterImage, bounds);
    }
}
