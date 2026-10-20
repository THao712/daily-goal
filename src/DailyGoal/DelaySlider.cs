using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class DelaySlider : Control
{
	private int value = 850;

	private bool dragging;

	private bool hovered;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int Value
	{
		get
		{
			return value;
		}
		set
		{
			int num = Math.Clamp(value, 500, 3000);
			if (this.value != num)
			{
				this.value = num;
				Invalidate();
			}
		}
	}

	public event EventHandler? ValueChanged;

	public event EventHandler? ValueCommitted;

	public DelaySlider()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
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
		if (!dragging)
		{
			Invalidate();
		}
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			dragging = true;
			base.Capture = true;
			SetFromX(e.X);
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (dragging)
		{
			SetFromX(e.X);
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && dragging)
		{
			dragging = false;
			base.Capture = false;
			SetFromX(e.X);
			this.ValueCommitted?.Invoke(this, EventArgs.Empty);
		}
		base.OnMouseUp(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		Keys keyCode = e.KeyCode;
		if ((keyCode == Keys.Left || keyCode == Keys.Down) ? true : false)
		{
			SetValue(Value - 100);
			this.ValueCommitted?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
		else
		{
			keyCode = e.KeyCode;
			if ((uint)(keyCode - 38) <= 1u)
			{
				SetValue(Value + 100);
				this.ValueCommitted?.Invoke(this, EventArgs.Empty);
				e.Handled = true;
			}
		}
		base.OnKeyDown(e);
	}

	private void SetFromX(int x)
	{
		float num = Math.Clamp(((float)x - UiTheme.Sf(10)) / Math.Max(1f, (float)base.Width - UiTheme.Sf(20)), 0f, 1f);
		float num2 = 500f + num * 2500f;
		SetValue((int)Math.Round((double)num2 / 100.0) * 100);
	}

	private void SetValue(int newValue)
	{
		int num = Math.Clamp(newValue, 500, 3000);
		if (num != Value)
		{
			Value = num;
			this.ValueChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		RectangleF rectangle = new RectangleF(UiTheme.Sf(9), (float)base.Height / 2f - UiTheme.Sf(2), base.Width - UiTheme.Sf(18), UiTheme.Sf(4));
		using GraphicsPath path = PaintHelpers.RoundRect(rectangle, UiTheme.Sf(2));
		using SolidBrush brush = new SolidBrush(UiTheme.SliderTrack);
		e.Graphics.FillPath(brush, path);
		float num = (float)(Value - 500) / 2500f;
		using GraphicsPath path2 = PaintHelpers.RoundRect(new RectangleF(rectangle.X, rectangle.Y, Math.Max(UiTheme.Sf(4), rectangle.Width * num), rectangle.Height), UiTheme.Sf(2));
		using SolidBrush brush2 = new SolidBrush(UiTheme.Mint);
		e.Graphics.FillPath(brush2, path2);
		float num2 = rectangle.X + rectangle.Width * num;
		float num3 = ((hovered || dragging || Focused) ? UiTheme.Sf(16) : UiTheme.Sf(14));
		using SolidBrush brush3 = new SolidBrush(UiTheme.SliderThumbShadow);
		e.Graphics.FillEllipse(brush3, num2 - num3 / 2f + 1f, (float)base.Height / 2f - num3 / 2f + 2f, num3, num3);
		using SolidBrush brush4 = new SolidBrush(Color.White);
		e.Graphics.FillEllipse(brush4, num2 - num3 / 2f, (float)base.Height / 2f - num3 / 2f, num3, num3);
		using Pen pen = new Pen(UiTheme.Mint, 2f);
		e.Graphics.DrawEllipse(pen, num2 - num3 / 2f, (float)base.Height / 2f - num3 / 2f, num3, num3);
	}
}
