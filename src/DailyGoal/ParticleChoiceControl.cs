using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class ParticleChoiceControl : Control
{
	private static readonly string[] Labels = new string[3] { "轻量", "标准", "绚丽" };

	private ParticleIntensity selected = ParticleIntensity.Celebration;

	private int hoveredIndex = -1;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public ParticleIntensity Selected
	{
		get
		{
			return selected;
		}
		set
		{
			if (selected != value)
			{
				selected = value;
				Invalidate();
			}
		}
	}

	public event EventHandler? SelectedChanged;

	public ParticleChoiceControl()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.WindowTop;
		Cursor = Cursors.Hand;
		base.TabStop = true;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = Math.Clamp(e.X * 3 / Math.Max(1, base.Width), 0, 2);
		if (hoveredIndex != num)
		{
			hoveredIndex = num;
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hoveredIndex = -1;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			SetSelected(Math.Clamp(e.X * 3 / Math.Max(1, base.Width), 0, 2));
		}
		base.OnMouseUp(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		Keys keyCode = e.KeyCode;
		if ((uint)(keyCode - 37) <= 1u)
		{
			SetSelected(Math.Max(0, (int)(Selected - 1)));
			e.Handled = true;
		}
		else
		{
			keyCode = e.KeyCode;
			if ((uint)(keyCode - 39) <= 1u)
			{
				SetSelected(Math.Min(2, (int)(Selected + 1)));
				e.Handled = true;
			}
		}
		base.OnKeyDown(e);
	}

	private void SetSelected(int index)
	{
		if (index != (int)Selected)
		{
			Selected = (ParticleIntensity)index;
			this.SelectedChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		float num = (float)base.Width / 3f;
		for (int i = 0; i < 3; i++)
		{
			RectangleF rectangleF = new RectangleF((float)i * num + 1f, 1f, num - 2f, base.Height - 2);
			using GraphicsPath path = PaintHelpers.RoundRect(rectangleF, UiTheme.RadiusMedium);
			bool flag = i == (int)Selected;
			using SolidBrush brush = new SolidBrush(flag ? UiTheme.Accent(i + 1) : ((i == hoveredIndex) ? UiTheme.SurfaceHover : UiTheme.Surface));
			e.Graphics.FillPath(brush, path);
			using Pen pen = new Pen(flag ? UiTheme.Accent(i + 1) : UiTheme.Border, 1f);
			e.Graphics.DrawPath(pen, path);
			TextRenderer.DrawText(e.Graphics, Labels[i], UiTheme.SmallBoldFont, Rectangle.Round(rectangleF), flag ? UiTheme.OnAccent(UiTheme.Accent(i + 1)) : UiTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
	}
}
