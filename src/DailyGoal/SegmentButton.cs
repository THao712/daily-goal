using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class SegmentButton : Control
{
	private bool active;

	private bool hovered;

	private bool pressed;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool Active
	{
		get
		{
			return active;
		}
		set
		{
			if (active != value)
			{
				active = value;
				Invalidate();
			}
		}
	}

	public SegmentButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.WindowTop;
		Cursor = Cursors.Hand;
		Font = UiTheme.BodyBoldFont;
		base.TabStop = true;
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
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && base.ClientRectangle.Contains(e.Location))
		{
			pressed = true;
			base.Capture = true;
			Focus();
			Invalidate();
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		bool num = e.Button == MouseButtons.Left && pressed && base.ClientRectangle.Contains(e.Location);
		pressed = false;
		base.Capture = false;
		Invalidate();
		base.OnMouseUp(e);
		if (num)
		{
			OnClick(EventArgs.Empty);
		}
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		Keys keyCode = e.KeyCode;
		if ((keyCode == Keys.Return || keyCode == Keys.Space) ? true : false)
		{
			pressed = true;
			Invalidate();
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	protected override void OnKeyUp(KeyEventArgs e)
	{
		Keys keyCode = e.KeyCode;
		if ((keyCode == Keys.Return || keyCode == Keys.Space) ? true : false)
		{
			bool num = pressed;
			pressed = false;
			Invalidate();
			e.Handled = true;
			if (num)
			{
				OnClick(EventArgs.Empty);
			}
		}
		base.OnKeyUp(e);
	}

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
		pevent.Graphics.Clear(BackColor);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		float num = (pressed ? 1f : 0f);
		RectangleF rectangleF = new RectangleF(1f, 1f + num, base.Width - 2, (float)(base.Height - 2) - num);
		using GraphicsPath path = PaintHelpers.RoundRect(rectangleF, UiTheme.RadiusMedium);
		using (SolidBrush brush = new SolidBrush(Active ? UiTheme.AccentSoft : (hovered ? UiTheme.SurfaceHover : BackColor)))
		{
			e.Graphics.FillPath(brush, path);
		}
		if (Active)
		{
			using Pen pen = new Pen(Color.FromArgb(124, UiTheme.Mint), 1f);
			e.Graphics.DrawPath(pen, path);
		}
		TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Round(rectangleF), Active ? UiTheme.AccentText : UiTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		if (Focused && ShowFocusCues)
		{
			UiTheme.DrawFocusRing(e.Graphics, path);
		}
	}
}
