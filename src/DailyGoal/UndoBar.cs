using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class UndoBar : Control
{
	private bool hovered;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public string Message { get; set; } = "";

	public event EventHandler? UndoClicked;

	public UndoBar()
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
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && base.ClientRectangle.Contains(e.Location))
		{
			this.UndoClicked?.Invoke(this, EventArgs.Empty);
		}
		base.OnMouseUp(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		Keys keyCode = e.KeyCode;
		if ((keyCode == Keys.Return || keyCode == Keys.Space) ? true : false)
		{
			this.UndoClicked?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		RectangleF rectangleF = new RectangleF(1f, 1f, base.Width - 2, base.Height - 3);
		using GraphicsPath path = PaintHelpers.RoundRect(rectangleF, UiTheme.RadiusMedium);
		using GraphicsPath path2 = PaintHelpers.RoundRect(new RectangleF(2f, 4f, base.Width - 4, base.Height - 4), UiTheme.RadiusMedium);
		using SolidBrush brush = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
		e.Graphics.FillPath(brush, path2);
		using SolidBrush brush2 = new SolidBrush(hovered ? UiTheme.SurfaceHover : UiTheme.Surface);
		e.Graphics.FillPath(brush2, path);
		using Pen pen = new Pen(UiTheme.WithAlpha(UiTheme.Mint, 130), 1f);
		e.Graphics.DrawPath(pen, path);
		PaintHelpers.DrawTopHighlight(e.Graphics, rectangleF, UiTheme.RadiusMedium);
		using GraphicsPath path3 = PaintHelpers.RoundRect(new RectangleF(rectangleF.Left + UiTheme.Sf(7), rectangleF.Top + UiTheme.Sf(9), UiTheme.Sf(4), rectangleF.Height - UiTheme.Sf(18)), UiTheme.Sf(2));
		using SolidBrush brush3 = new SolidBrush(UiTheme.WithAlpha(UiTheme.Mint, 220));
		e.Graphics.FillPath(brush3, path3);
		TextRenderer.DrawText(e.Graphics, Message, UiTheme.SmallBoldFont, new Rectangle(UiTheme.S(22), 0, base.Width - UiTheme.S(94), base.Height), UiTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		TextRenderer.DrawText(e.Graphics, "撤销", UiTheme.BodyBoldFont, new Rectangle(base.Width - UiTheme.S(70), 0, UiTheme.S(56), base.Height), hovered ? UiTheme.AccentHover : UiTheme.Mint, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
	}
}
