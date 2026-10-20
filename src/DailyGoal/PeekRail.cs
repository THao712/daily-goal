using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class PeekRail : Control
{
	private int remaining;

	private float progress;

	private bool dragCandidate;

	private bool dragging;

	private Point dragStartScreen;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int Remaining
	{
		get
		{
			return remaining;
		}
		set
		{
			remaining = Math.Max(0, value);
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public float Progress
	{
		get
		{
			return progress;
		}
		set
		{
			progress = Math.Clamp(value, 0f, 1f);
			Invalidate();
		}
	}

	/// <summary>停靠在左边缘时箭头指向右（展开方向）。</summary>
	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool PointRight { get; set; }

	/// <summary>按住贴条拖动时触发，用于沿屏幕边缘调整窗口位置，也可以横向拖到另一侧。</summary>
	public event EventHandler<Point>? RailDragStarted;

	public event EventHandler<Point>? RailDragMoved;

	public event EventHandler? RailDragEnded;

	public PeekRail()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		Cursor = Cursors.Hand;
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			dragCandidate = true;
			dragStartScreen = PointToScreen(e.Location);
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (dragging)
		{
			RailDragMoved?.Invoke(this, PointToScreen(e.Location));
		}
		else if (dragCandidate && e.Button == MouseButtons.Left)
		{
			var screen = PointToScreen(e.Location);
			if (Math.Abs(screen.X - dragStartScreen.X) > SystemInformation.DragSize.Width
				|| Math.Abs(screen.Y - dragStartScreen.Y) > SystemInformation.DragSize.Height)
			{
				dragging = true;
				Capture = true;
				Cursor = Cursors.SizeAll;
				RailDragStarted?.Invoke(this, dragStartScreen);
				RailDragMoved?.Invoke(this, screen);
			}
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			dragCandidate = false;
			if (dragging)
			{
				dragging = false;
				Capture = false;
				Cursor = Cursors.Hand;
				RailDragEnded?.Invoke(this, EventArgs.Empty);
			}
		}
		base.OnMouseUp(e);
	}

	protected override void OnMouseCaptureChanged(EventArgs e)
	{
		if (dragging && !Capture)
		{
			dragging = false;
			dragCandidate = false;
			Cursor = Cursors.Hand;
			RailDragEnded?.Invoke(this, EventArgs.Empty);
		}
		base.OnMouseCaptureChanged(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		e.Graphics.Clear(UiTheme.WindowTop);
		using LinearGradientBrush brush = new LinearGradientBrush(base.ClientRectangle, UiTheme.SurfaceHover, UiTheme.SurfaceMuted, 90f);
		using GraphicsPath path = PaintHelpers.RoundRect(new RectangleF(1f, 1f, base.Width - 2, base.Height - 2), UiTheme.Sf(9));
		e.Graphics.FillPath(brush, path);
		using Pen pen = new Pen(UiTheme.BorderStrong, 1f);
		e.Graphics.DrawPath(pen, path);
		RectangleF rectangleF = new RectangleF(UiTheme.Sf(5), UiTheme.Sf(18), base.Width - UiTheme.Sf(10), base.Width - UiTheme.Sf(10));
		Color badgeColor = Remaining == 0 ? UiTheme.Mint : UiTheme.Blend(UiTheme.Coral, UiTheme.Mint, Progress);
		using SolidBrush brush2 = new SolidBrush(badgeColor);
		e.Graphics.FillEllipse(brush2, rectangleF);
	TextRenderer.DrawText(e.Graphics, (Remaining > 99) ? "99" : Remaining.ToString(CultureInfo.InvariantCulture), UiTheme.SmallBoldFont, Rectangle.Round(rectangleF), UiTheme.OnAccent(badgeColor), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		RectangleF rectangle = new RectangleF((float)base.Width / 2f - UiTheme.Sf(2), UiTheme.Sf(66), UiTheme.Sf(4), Math.Max(UiTheme.Sf(24), base.Height - UiTheme.Sf(118)));
		using GraphicsPath path2 = PaintHelpers.RoundRect(rectangle, UiTheme.Sf(2));
		using SolidBrush brush3 = new SolidBrush(UiTheme.WithAlpha(UiTheme.TextFaint, 74));
		e.Graphics.FillPath(brush3, path2);
		float num = rectangle.Height * Progress;
		if (num > 1f)
		{
			using GraphicsPath path3 = PaintHelpers.RoundRect(new RectangleF(rectangle.X, rectangle.Bottom - num, rectangle.Width, num), UiTheme.Sf(2));
			using SolidBrush brush4 = new SolidBrush(UiTheme.Mint);
			e.Graphics.FillPath(brush4, path3);
		}
		var chevronBounds = new RectangleF(UiTheme.Sf(6), base.Height - UiTheme.Sf(37), base.Width - UiTheme.Sf(12), UiTheme.Sf(20));
		if (PointRight)
		{
			var state = e.Graphics.Save();
			var centerX = chevronBounds.Left + chevronBounds.Width / 2f;
			e.Graphics.TranslateTransform(centerX, 0f);
			e.Graphics.ScaleTransform(-1f, 1f);
			e.Graphics.TranslateTransform(-centerX, 0f);
			IconPainter.Draw(e.Graphics, IconKind.Expand, chevronBounds, UiTheme.TextMuted, 1.7f);
			e.Graphics.Restore(state);
		}
		else
		{
			IconPainter.Draw(e.Graphics, IconKind.Expand, chevronBounds, UiTheme.TextMuted, 1.7f);
		}
	}
}
