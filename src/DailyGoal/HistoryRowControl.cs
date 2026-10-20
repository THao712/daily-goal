using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class HistoryRowControl : Control
{
	private bool hovered;

	private bool restoreHovered;

	private bool deleteHovered;

	public TaskRecord Task { get; }

	private Rectangle RestoreBounds => new Rectangle(Math.Max(0, base.Width - UiTheme.S(104)), UiTheme.S(17), UiTheme.S(38), UiTheme.S(38));

	private Rectangle DeleteBounds => new Rectangle(Math.Max(0, base.Width - UiTheme.S(56)), UiTheme.S(17), UiTheme.S(38), UiTheme.S(38));

	public event EventHandler? RestoreRequested;

	public event EventHandler? DeleteRequested;

	public HistoryRowControl(TaskRecord task)
	{
		Task = task;
		SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.Canvas;
		Cursor = Cursors.Default;
		base.TabStop = true;
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hovered = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		restoreHovered = RestoreBounds.Contains(e.Location);
		deleteHovered = DeleteBounds.Contains(e.Location);
		Cursor = ((restoreHovered || deleteHovered) ? Cursors.Hand : Cursors.Default);
		Invalidate();
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hovered = false;
		restoreHovered = false;
		deleteHovered = false;
		Cursor = Cursors.Default;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			if (RestoreBounds.Contains(e.Location))
			{
				this.RestoreRequested?.Invoke(this, EventArgs.Empty);
			}
			else if (DeleteBounds.Contains(e.Location))
			{
				this.DeleteRequested?.Invoke(this, EventArgs.Empty);
			}
		}
		base.OnMouseUp(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		Keys keyCode = e.KeyCode;
		if ((keyCode == Keys.Return || keyCode == Keys.Space) ? true : false)
		{
			this.RestoreRequested?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
		else if (keyCode == Keys.Delete)
		{
			this.DeleteRequested?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		RectangleF cardRect = new RectangleF(2f, 2f, base.Width - 4, base.Height - 4);
		using GraphicsPath path = PaintHelpers.RoundRect(cardRect, UiTheme.RadiusMedium);
		using SolidBrush brush = new SolidBrush(hovered ? UiTheme.SurfaceHover : UiTheme.Surface);
		e.Graphics.FillPath(brush, path);
		using Pen pen = new Pen(hovered ? UiTheme.WithAlpha(UiTheme.Accent(Task.AccentIndex), 118) : UiTheme.Border, 1f);
		e.Graphics.DrawPath(pen, path);
		PaintHelpers.DrawTopHighlight(e.Graphics, cardRect, UiTheme.RadiusMedium, 12);
		using SolidBrush brush2 = new SolidBrush(UiTheme.WithAlpha(UiTheme.Accent(Task.AccentIndex), 190));
		e.Graphics.FillRectangle(brush2, UiTheme.S(8), UiTheme.S(13), UiTheme.S(3), base.Height - UiTheme.S(26));
		TextRenderer.DrawText(e.Graphics, Task.Text, UiTheme.BodyFont, new Rectangle(UiTheme.S(26), UiTheme.S(10), base.Width - UiTheme.S(154), UiTheme.S(31)), UiTheme.TextMuted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		string text = Task.CompletedAt?.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture) ?? "";
		TextRenderer.DrawText(e.Graphics, text, UiTheme.SmallFont, new Rectangle(UiTheme.S(26), UiTheme.S(43), base.Width - UiTheme.S(154), UiTheme.S(22)), UiTheme.TextFaint, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		if (restoreHovered)
		{
			using GraphicsPath path2 = PaintHelpers.RoundRect(RestoreBounds, UiTheme.RadiusSmall);
			using SolidBrush brush3 = new SolidBrush(UiTheme.WithAlpha(UiTheme.Mint, 28));
			e.Graphics.FillPath(brush3, path2);
		}
		IconPainter.Draw(e.Graphics, IconKind.Restore, RectangleF.Inflate(RestoreBounds, -UiTheme.Sf(7), -UiTheme.Sf(7)), restoreHovered ? UiTheme.Mint : UiTheme.TextFaint, 1.65f);
		if (deleteHovered)
		{
			using GraphicsPath path3 = PaintHelpers.RoundRect(DeleteBounds, UiTheme.RadiusSmall);
			using SolidBrush brush4 = new SolidBrush(UiTheme.WithAlpha(UiTheme.Danger, 30));
			e.Graphics.FillPath(brush4, path3);
		}
		IconPainter.Draw(e.Graphics, IconKind.Delete, RectangleF.Inflate(DeleteBounds, -UiTheme.Sf(7), -UiTheme.Sf(7)), deleteHovered ? UiTheme.Danger : UiTheme.TextFaint, 1.65f);
		if (Focused && ShowFocusCues)
		{
			UiTheme.DrawFocusRing(e.Graphics, path);
		}
	}
}
