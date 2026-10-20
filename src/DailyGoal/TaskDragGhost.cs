using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class TaskDragGhost : Form
{
	private readonly TaskRecord task;

	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ClassStyle |= 131072;
			createParams.ExStyle |= 134217888;
			return createParams;
		}
	}

	public TaskDragGhost(TaskRecord task, Size size)
	{
		this.task = task;
		base.AutoScaleMode = AutoScaleMode.None;
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.StartPosition = FormStartPosition.Manual;
		base.TopMost = true;
		base.Opacity = 0.96;
		BackColor = UiTheme.Surface;
		base.ClientSize = size;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		UpdateRegion();
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		if (NativeMethods.ApplyRoundedCorners(base.Handle))
		{
			base.Region?.Dispose();
			base.Region = null;
		}
	}

	public void ShowFor(Form owner, Point screenLocation, Point grabOffset)
	{
		UpdatePosition(screenLocation, grabOffset);
		if (!base.Visible)
		{
			Show(owner);
		}
	}

	public void UpdatePosition(Point screenLocation, Point grabOffset)
	{
		base.Location = new Point(screenLocation.X - grabOffset.X, screenLocation.Y - grabOffset.Y - 5);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(UiTheme.Surface);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using GraphicsPath path = PaintHelpers.RoundRect(new RectangleF(1.5f, 1.5f, (float)base.Width - 3f, (float)base.Height - 3f), UiTheme.RadiusLarge);
		using SolidBrush brush = new SolidBrush(UiTheme.Surface);
		using Pen pen = new Pen(UiTheme.Accent(task.AccentIndex), 2.2f);
		e.Graphics.FillPath(brush, path);
		e.Graphics.DrawPath(pen, path);
		Color color = UiTheme.Accent(task.AccentIndex);
		using GraphicsPath path2 = PaintHelpers.RoundRect(new RectangleF(UiTheme.Sf(8), UiTheme.Sf(12), UiTheme.Sf(5), base.Height - UiTheme.S(24)), UiTheme.Sf(2.5f));
		using SolidBrush brush2 = new SolidBrush(color);
		e.Graphics.FillPath(brush2, path2);
		using GraphicsPath path3 = PaintHelpers.RoundRect(new Rectangle(UiTheme.S(22), base.Height / 2 - UiTheme.S(16), UiTheme.S(32), UiTheme.S(32)), UiTheme.Sf(9));
		using SolidBrush brush3 = new SolidBrush(UiTheme.SurfaceHover);
		using Pen pen2 = new Pen(UiTheme.BorderStrong, 1.4f);
		e.Graphics.FillPath(brush3, path3);
		e.Graphics.DrawPath(pen2, path3);
		Rectangle bounds = new Rectangle(UiTheme.S(68), (task.CarryCount > 0) ? UiTheme.S(10) : UiTheme.S(8), Math.Max(80, base.Width - UiTheme.S(132)), (task.CarryCount > 0) ? UiTheme.S(36) : (base.Height - UiTheme.S(16)));
		TextRenderer.DrawText(e.Graphics, task.Text, UiTheme.TaskFont, bounds, UiTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		if (task.CarryCount > 0)
		{
			Rectangle rectangle = new Rectangle(bounds.Left, base.Height - UiTheme.S(30), UiTheme.S(92), UiTheme.S(22));
			using GraphicsPath path4 = PaintHelpers.RoundRect(rectangle, UiTheme.Sf(6));
			using SolidBrush brush4 = new SolidBrush(Color.FromArgb(28, color));
			e.Graphics.FillPath(brush4, path4);
			TextRenderer.DrawText(e.Graphics, "昨日延续", UiTheme.SmallFont, rectangle, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
		using SolidBrush brush5 = new SolidBrush(UiTheme.TextFaint);
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				e.Graphics.FillEllipse(brush5, base.Width - UiTheme.S(28) + i * UiTheme.S(6), base.Height / 2 - UiTheme.S(8) + j * UiTheme.S(7), UiTheme.Sf(3), UiTheme.Sf(3));
			}
		}
	}

	private void UpdateRegion()
	{
		using GraphicsPath path = PaintHelpers.RoundRect(new RectangleF(0f, 0f, base.Width, base.Height), UiTheme.RadiusLarge);
		base.Region?.Dispose();
		base.Region = new Region(path);
	}
}
