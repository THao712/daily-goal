using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class HealingPanel : Panel
{
	public HealingPanel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(UiTheme.WindowTop);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using GraphicsPath path = PaintHelpers.RoundRect(new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f), UiTheme.RadiusLarge);
		using Pen pen = new Pen(UiTheme.Border, 1f);
		e.Graphics.DrawPath(pen, path);
		base.OnPaint(e);
	}
}
