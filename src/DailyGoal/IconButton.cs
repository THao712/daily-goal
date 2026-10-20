using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class IconButton : Control
{
	private bool hovered;

	private bool pressed;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public IconKind Icon { get; set; }

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public IconButtonTone Tone { get; set; }

	public IconButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.WindowTop;
		Cursor = Cursors.Hand;
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
		pressed = false;
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
		RectangleF rectangleF = new RectangleF(1f, 1f + num, (float)base.Width - 2f, (float)base.Height - 2f - num);
		using GraphicsPath path = PaintHelpers.RoundRect(rectangleF, UiTheme.RadiusMedium);
		Color color = Tone switch
		{
			IconButtonTone.Accent => UiTheme.Mint, 
			IconButtonTone.Danger => UiTheme.Danger, 
			_ => UiTheme.TextMuted, 
		};
		using (SolidBrush brush = new SolidBrush((Tone != IconButtonTone.Accent) ? ((!hovered) ? BackColor : ((Tone == IconButtonTone.Danger) ? UiTheme.DangerSoft : UiTheme.SurfaceHover)) : (hovered ? UiTheme.AccentHover : UiTheme.Mint)))
		{
			e.Graphics.FillPath(brush, path);
		}
		if (Tone == IconButtonTone.Accent)
		{
			using Pen pen = new Pen(Color.FromArgb(118, 255, 255, 255), 1f);
			e.Graphics.DrawPath(pen, path);
		}
		Color color2 = ((Tone == IconButtonTone.Accent) ? UiTheme.OnAccent(UiTheme.Mint) : (hovered ? color : UiTheme.TextMuted));
		RectangleF bounds = RectangleF.Inflate(rectangleF, -UiTheme.Sf(6), -UiTheme.Sf(6));
		IconPainter.Draw(e.Graphics, Icon, bounds, color2, (Tone == IconButtonTone.Accent) ? 1.9f : 1.7f);
		if (Focused && ShowFocusCues)
		{
			UiTheme.DrawFocusRing(e.Graphics, path);
		}
	}
}
