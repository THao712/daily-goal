using System.Drawing;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class HistoryDateHeader : Control
{
	public HistoryDateHeader(string text)
	{
		Text = text;
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.Canvas;
	}

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
		pevent.Graphics.Clear(BackColor);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		TextRenderer.DrawText(e.Graphics, Text, UiTheme.SmallBoldFont, new Rectangle(UiTheme.S(7), 0, base.Width - UiTheme.S(14), base.Height), UiTheme.TextMuted, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
	}
}
