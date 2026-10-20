using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class ProgressRing : Control
{
	private int completed;

	private int total;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int Completed
	{
		get
		{
			return completed;
		}
		set
		{
			completed = Math.Max(0, value);
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int Total
	{
		get
		{
			return total;
		}
		set
		{
			total = Math.Max(0, value);
			Invalidate();
		}
	}

	public ProgressRing()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.WindowTop;
	}

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
		pevent.Graphics.Clear(BackColor);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		RectangleF rect = new RectangleF(4f, 4f, base.Width - 8, base.Height - 8);
		using Pen pen = new Pen(UiTheme.Border, 3f);
		e.Graphics.DrawArc(pen, rect, -90f, 360f);
		float num = ((Total <= 0) ? 0f : Math.Clamp((float)Completed / (float)Total, 0f, 1f));
		if (num > 0f)
		{
			using Pen pen2 = new Pen(UiTheme.Mint, 3.2f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			e.Graphics.DrawArc(pen2, rect, -90f, num * 360f);
		}
		string text = ((Total <= 0) ? "0" : $"{Completed}/{Total}");
		TextRenderer.DrawText(e.Graphics, text, UiTheme.SmallBoldFont, base.ClientRectangle, UiTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}
}
