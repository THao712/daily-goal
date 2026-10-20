using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class ToggleSwitch : Control
{
	private bool isChecked;

	private bool hovered;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool Checked
	{
		get
		{
			return isChecked;
		}
		set
		{
			if (isChecked != value)
			{
				isChecked = value;
				Invalidate();
			}
		}
	}

	public event EventHandler? CheckedChanged;

	public ToggleSwitch()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.WindowTop;
		Cursor = Cursors.Hand;
		base.TabStop = true;
		base.Size = new Size(UiTheme.S(56), UiTheme.S(32));
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

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && base.ClientRectangle.Contains(e.Location))
		{
			Toggle();
		}
		base.OnMouseUp(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		Keys keyCode = e.KeyCode;
		if ((keyCode == Keys.Return || keyCode == Keys.Space) ? true : false)
		{
			Toggle();
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	private void Toggle()
	{
		Checked = !Checked;
		this.CheckedChanged?.Invoke(this, EventArgs.Empty);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		RectangleF rectangle = new RectangleF(1f, 3f, base.Width - 2, base.Height - 6);
		using GraphicsPath path = PaintHelpers.RoundRect(rectangle, rectangle.Height / 2f);
		using SolidBrush brush = new SolidBrush(Checked ? UiTheme.WithAlpha(UiTheme.Mint, hovered ? 245 : 225) : UiTheme.WithAlpha(UiTheme.ToggleOffTrack, hovered ? 235 : 205));
		e.Graphics.FillPath(brush, path);
		float num = rectangle.Height - 4f;
		float x = (Checked ? (rectangle.Right - num - 2f) : (rectangle.Left + 2f));
		using SolidBrush brush2 = new SolidBrush(Color.White);
		e.Graphics.FillEllipse(brush2, x, rectangle.Top + 2f, num, num);
		if (Focused && ShowFocusCues)
		{
			UiTheme.DrawFocusRing(e.Graphics, path);
		}
	}
}
