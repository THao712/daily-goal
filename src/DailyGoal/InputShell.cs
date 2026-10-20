using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class InputShell : Panel
{
	private bool focused;

	public InputShell()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.Canvas;
	}

	protected override void OnControlAdded(ControlEventArgs e)
	{
		if (e.Control == null)
		{
			base.OnControlAdded(e);
			return;
		}
		e.Control.GotFocus += delegate
		{
			focused = true;
			Invalidate();
		};
		e.Control.LostFocus += delegate
		{
			focused = false;
			Invalidate();
		};
		base.OnControlAdded(e);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using GraphicsPath path = PaintHelpers.RoundRect(new RectangleF(1f, 1f, base.Width - 2, base.Height - 3), UiTheme.RadiusMedium);
		using SolidBrush brush = new SolidBrush(UiTheme.Surface);
		e.Graphics.FillPath(brush, path);
		using Pen pen = new Pen(focused ? UiTheme.Mint : UiTheme.Border, focused ? 1.6f : 1f);
		e.Graphics.DrawPath(pen, path);
	}
}
