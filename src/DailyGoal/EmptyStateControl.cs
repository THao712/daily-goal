using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class EmptyStateControl : Control
{
	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public string Title { get; set; } = "今天很轻盈";

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public string Subtitle { get; set; } = "写下一件想完成的事";

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Image? CharacterImage { get; set; }

	public EmptyStateControl()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.Canvas;
	}

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
		pevent.Graphics.Clear(BackColor);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		if (UiTheme.Mode == AppThemeMode.Xuanling && CharacterImage is not null)
		{
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			var maximumSize = base.Height >= UiTheme.S(420) ? UiTheme.S(236) : UiTheme.S(154);
			var imageSize = Math.Clamp(Math.Min(base.Width - UiTheme.S(56), base.Height - UiTheme.S(88)), UiTheme.S(96), maximumSize);
			var totalHeight = imageSize + UiTheme.S(58);
			var imageTop = Math.Max(UiTheme.S(8), (base.Height - totalHeight) / 2);
			var imageBounds = new Rectangle((base.Width - imageSize) / 2, imageTop, imageSize, imageSize);
			using (var glow = new SolidBrush(UiTheme.WithAlpha(UiTheme.Sky, 24)))
			{
				e.Graphics.FillEllipse(glow, Rectangle.Inflate(imageBounds, UiTheme.S(9), UiTheme.S(7)));
			}
			e.Graphics.DrawImage(CharacterImage, imageBounds);
			var titleTop = imageBounds.Bottom + UiTheme.S(4);
			TextRenderer.DrawText(e.Graphics, Title, UiTheme.HeadingFont, new Rectangle(UiTheme.S(24), titleTop, base.Width - UiTheme.S(48), UiTheme.S(26)), UiTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			TextRenderer.DrawText(e.Graphics, Subtitle, UiTheme.SmallFont, new Rectangle(UiTheme.S(24), titleTop + UiTheme.S(27), base.Width - UiTheme.S(48), UiTheme.S(24)), UiTheme.TextFaint, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			return;
		}
		float num = (float)base.Width / 2f;
		float num2 = (float)base.Height / 2f - UiTheme.Sf(38);
		using SolidBrush brush = new SolidBrush(UiTheme.AccentSoft);
		e.Graphics.FillEllipse(brush, num - UiTheme.Sf(36), num2 - UiTheme.Sf(36), UiTheme.Sf(72), UiTheme.Sf(72));
		using Pen pen = new Pen(UiTheme.AccentSoftLine, 2f);
		e.Graphics.DrawEllipse(pen, num - UiTheme.Sf(23), num2 - UiTheme.Sf(23), UiTheme.Sf(46), UiTheme.Sf(46));
		using Pen pen2 = new Pen(UiTheme.Mint, UiTheme.Sf(2.6f))
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		e.Graphics.DrawLines(pen2,
		[
			new PointF(num - UiTheme.Sf(11), num2 + UiTheme.Sf(1)),
			new PointF(num - UiTheme.Sf(2), num2 + UiTheme.Sf(10)),
			new PointF(num + UiTheme.Sf(16), num2 - UiTheme.Sf(11))
		]);
		TextRenderer.DrawText(e.Graphics, Title, UiTheme.HeadingFont, new Rectangle(UiTheme.S(24), (int)num2 + UiTheme.S(42), base.Width - UiTheme.S(48), UiTheme.S(26)), UiTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		TextRenderer.DrawText(e.Graphics, Subtitle, UiTheme.SmallFont, new Rectangle(UiTheme.S(24), (int)num2 + UiTheme.S(69), base.Width - UiTheme.S(48), UiTheme.S(24)), UiTheme.TextFaint, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
	}
}
