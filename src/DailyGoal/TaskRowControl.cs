using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class TaskRowControl : Panel
{
	private enum PressTarget
	{
		None,
		Check,
		More,
		Drag
	}

	private readonly TextBox editor = new TextBox();

	private bool hovered;

	private bool checkHovered;

	private bool moreHovered;

	private bool dragHovered;

	private bool dragCandidate;

	private bool customDragging;

	private Point dragStart;

	private PressTarget pressedTarget;

	private bool completedVisual;

	private float pulse;

	private float checkPulse;

	private bool isDragPlaceholder;

	private float strikeProgress = 1f;

	public TaskRecord Task { get; }

	public bool Editing => editor.Visible;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool CompletionEnabled { get; set; } = true;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool IsRemoving { get; set; }

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool CompletedVisual
	{
		get
		{
			return completedVisual;
		}
		set
		{
			completedVisual = value;
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public float Pulse
	{
		get
		{
			return pulse;
		}
		set
		{
			pulse = Math.Clamp(value, 0f, 1f);
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public float CheckPulse
	{
		get
		{
			return checkPulse;
		}
		set
		{
			checkPulse = Math.Clamp(value, 0f, 1f);
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public float StrikeProgress
	{
		get
		{
			return strikeProgress;
		}
		set
		{
			strikeProgress = Math.Clamp(value, 0f, 1f);
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool IsDragPlaceholder
	{
		get
		{
			return isDragPlaceholder;
		}
		set
		{
			if (isDragPlaceholder != value)
			{
				isDragPlaceholder = value;
				Invalidate();
			}
		}
	}

	public static Point CheckCenter => new Point(CheckBounds.Left + CheckBounds.Width / 2, CheckBounds.Top + CheckBounds.Height / 2);

	private static Rectangle CheckBounds => new Rectangle(UiTheme.S(22), UiTheme.S(25), UiTheme.S(32), UiTheme.S(32));

	private Rectangle DragBounds => new Rectangle(Math.Max(0, base.Width - UiTheme.S(44)), UiTheme.S(23), UiTheme.S(32), UiTheme.S(36));

	private Rectangle MoreBounds => new Rectangle(Math.Max(0, base.Width - UiTheme.S(88)), UiTheme.S(23), UiTheme.S(38), UiTheme.S(36));

	private Rectangle TextBounds => new Rectangle(
		UiTheme.S(68),
		(Task.CarryCount > 0) ? UiTheme.S(9) : UiTheme.S(10),
		Math.Max(60, base.Width - UiTheme.S((hovered || Focused) ? 170 : 116)),
		(Task.CarryCount > 0) ? UiTheme.S(35) : (base.Height - UiTheme.S(20)));

	/// <summary>文本超出可视宽度时返回 true（用于决定是否挂 tooltip）。</summary>
	public bool IsTextOverflowing
	{
		get
		{
			var available = Math.Max(60, base.Width - UiTheme.S(170));
			return TextRenderer.MeasureText(Task.Text, UiTheme.TaskFont).Width > available;
		}
	}

	public event EventHandler? CompleteRequested;

	public event EventHandler? MoreRequested;

	public event EventHandler<TaskTextEditEventArgs>? EditCommitted;

	public event EventHandler<TaskReorderEventArgs>? KeyboardReorderRequested;

	public event EventHandler<TaskDragStartedEventArgs>? TaskDragStarted;

	public event EventHandler<TaskDragEventArgs>? TaskDragMoved;

	public event EventHandler<TaskDragEventArgs>? TaskDragEnded;

	public TaskRowControl(TaskRecord task)
	{
		Task = task;
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.Canvas;
		Cursor = Cursors.Default;
		base.TabStop = true;
		AllowDrop = false;
		editor.Visible = false;
		editor.BorderStyle = BorderStyle.FixedSingle;
		editor.Font = UiTheme.TaskFont;
		editor.BackColor = UiTheme.Surface;
		editor.ForeColor = UiTheme.Text;
		editor.KeyDown += EditorOnKeyDown;
		editor.LostFocus += delegate
		{
			if (editor.Visible)
			{
				CommitEdit();
			}
		};
		base.Controls.Add(editor);
	}

	public void BeginEdit()
	{
		if (!CompletedVisual && !IsRemoving)
		{
			editor.Bounds = new Rectangle(TextBounds.Left - 2, UiTheme.S(18), TextBounds.Width + 6, UiTheme.S(44));
			editor.Text = Task.Text;
			editor.Visible = true;
			editor.BringToFront();
			editor.Focus();
			editor.SelectAll();
			Invalidate();
		}
	}

	public void CancelEdit()
	{
		if (editor.Visible)
		{
			editor.Visible = false;
			Focus();
			Invalidate();
		}
	}

	private void EditorOnKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return)
		{
			e.SuppressKeyPress = true;
			CommitEdit();
		}
		else if (e.KeyCode == Keys.Escape)
		{
			e.SuppressKeyPress = true;
			CancelEdit();
		}
	}

	private void CommitEdit()
	{
		string text = editor.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			editor.Focus();
			return;
		}
		TaskTextEditEventArgs e = new TaskTextEditEventArgs(text);
		this.EditCommitted?.Invoke(this, e);
		if (e.Cancel)
		{
			editor.Focus();
			return;
		}
		editor.Visible = false;
		Focus();
		Invalidate();
	}

	protected override void OnEnter(EventArgs e)
	{
		Invalidate();
		base.OnEnter(e);
	}

	protected override void OnLeave(EventArgs e)
	{
		Invalidate();
		base.OnLeave(e);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hovered = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (customDragging)
		{
			this.TaskDragMoved?.Invoke(this, new TaskDragEventArgs(PointToScreen(e.Location)));
			base.OnMouseMove(e);
			return;
		}
		checkHovered = CheckBounds.Contains(e.Location) && !CompletedVisual && CompletionEnabled;
		moreHovered = MoreBounds.Contains(e.Location);
		dragHovered = DragBounds.Contains(e.Location) && !CompletedVisual;
		Cursor = (dragHovered ? Cursors.SizeAll : ((checkHovered || moreHovered) ? Cursors.Hand : Cursors.Default));
		if (dragCandidate && e.Button == MouseButtons.Left)
		{
			Size dragSize = SystemInformation.DragSize;
			if (!new Rectangle(dragStart.X - dragSize.Width / 2, dragStart.Y - dragSize.Height / 2, dragSize.Width, dragSize.Height).Contains(e.Location))
			{
				dragCandidate = false;
				customDragging = true;
				base.Capture = true;
				this.TaskDragStarted?.Invoke(this, new TaskDragStartedEventArgs(PointToScreen(e.Location), dragStart));
				this.TaskDragMoved?.Invoke(this, new TaskDragEventArgs(PointToScreen(e.Location)));
			}
		}
		Invalidate();
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		if (customDragging)
		{
			base.OnMouseLeave(e);
			return;
		}
		hovered = false;
		checkHovered = false;
		moreHovered = false;
		dragHovered = false;
		if (!dragCandidate)
		{
			pressedTarget = PressTarget.None;
		}
		Cursor = Cursors.Default;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			Focus();
			pressedTarget = ((CheckBounds.Contains(e.Location) && !CompletedVisual && CompletionEnabled) ? PressTarget.Check : (MoreBounds.Contains(e.Location) ? PressTarget.More : ((DragBounds.Contains(e.Location) && !CompletedVisual) ? PressTarget.Drag : PressTarget.None)));
			if (pressedTarget == PressTarget.Drag)
			{
				dragCandidate = true;
				dragStart = e.Location;
			}
			Invalidate();
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			if (customDragging)
			{
				customDragging = false;
				dragCandidate = false;
				pressedTarget = PressTarget.None;
				base.Capture = false;
				this.TaskDragEnded?.Invoke(this, new TaskDragEventArgs(PointToScreen(e.Location)));
				Invalidate();
				base.OnMouseUp(e);
				return;
			}
			PressTarget pressTarget = pressedTarget;
			pressedTarget = PressTarget.None;
			if (dragCandidate)
			{
				dragCandidate = false;
			}
			if (pressTarget == PressTarget.Check && CheckBounds.Contains(e.Location) && !CompletedVisual)
			{
				this.CompleteRequested?.Invoke(this, EventArgs.Empty);
			}
			else if (pressTarget == PressTarget.More && MoreBounds.Contains(e.Location))
			{
				this.MoreRequested?.Invoke(this, EventArgs.Empty);
			}
			Invalidate();
		}
		base.OnMouseUp(e);
	}

	protected override void OnMouseDoubleClick(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && TextBounds.Contains(e.Location))
		{
			BeginEdit();
		}
		base.OnMouseDoubleClick(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape && customDragging)
		{
			customDragging = false;
			dragCandidate = false;
			pressedTarget = PressTarget.None;
			base.Capture = false;
			this.TaskDragEnded?.Invoke(this, new TaskDragEventArgs(System.Windows.Forms.Cursor.Position, canceled: true));
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.Space && !CompletedVisual && CompletionEnabled)
		{
			this.CompleteRequested?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.F2 && !CompletedVisual)
		{
			BeginEdit();
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.Delete)
		{
			this.MoreRequested?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
		else if (e.Control && e.KeyCode is Keys.Up or Keys.Down)
		{
			this.KeyboardReorderRequested?.Invoke(this, new TaskReorderEventArgs((e.KeyCode != Keys.Up) ? 1 : (-1)));
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

	protected override void OnMouseCaptureChanged(EventArgs e)
	{
		if (customDragging && !base.Capture)
		{
			customDragging = false;
			dragCandidate = false;
			pressedTarget = PressTarget.None;
			this.TaskDragEnded?.Invoke(this, new TaskDragEventArgs(System.Windows.Forms.Cursor.Position, canceled: true));
		}
		base.OnMouseCaptureChanged(e);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		if (IsDragPlaceholder)
		{
			using (GraphicsPath path = PaintHelpers.RoundRect(new RectangleF(5f, 5f, base.Width - 11, base.Height - 11), UiTheme.RadiusMedium))
			{
				using SolidBrush brush = new SolidBrush(Color.FromArgb(34, UiTheme.Accent(Task.AccentIndex)));
				using Pen pen = new Pen(Color.FromArgb(178, UiTheme.Accent(Task.AccentIndex)), 1.6f)
				{
					DashStyle = DashStyle.Dash
				};
				e.Graphics.FillPath(brush, path);
				e.Graphics.DrawPath(pen, path);
				return;
			}
		}
		GraphicsState gstate = e.Graphics.Save();
		float num = (hovered ? 0f : 1f);
		RectangleF rectangle = new RectangleF(4f, 3f + num, base.Width - 9, base.Height - 9);
		using GraphicsPath path2 = PaintHelpers.RoundRect(rectangle, UiTheme.RadiusMedium);
		using GraphicsPath path3 = PaintHelpers.RoundRect(new RectangleF(rectangle.X + 1f, rectangle.Y + 4f, rectangle.Width, rectangle.Height), UiTheme.RadiusMedium);
		using SolidBrush brush2 = new SolidBrush(hovered ? UiTheme.CardShadowHover : UiTheme.CardShadow);
		e.Graphics.FillPath(brush2, path3);
		using SolidBrush brush3 = new SolidBrush(CompletedVisual ? UiTheme.SurfaceMuted : (hovered ? UiTheme.SurfaceHover : UiTheme.Surface));
		e.Graphics.FillPath(brush3, path2);
		Color color = UiTheme.Accent(Task.AccentIndex);
		using Pen pen2 = new Pen(hovered ? Color.FromArgb(176, color) : UiTheme.Border, hovered ? 1.4f : 1f);
		e.Graphics.DrawPath(pen2, path2);
		if (!CompletedVisual)
		{
			PaintHelpers.DrawTopHighlight(e.Graphics, rectangle, UiTheme.RadiusMedium);
		}
		if (Pulse > 0f)
		{
			using Pen pen3 = new Pen(UiTheme.WithAlpha(UiTheme.Accent(Task.AccentIndex), (int)(150f * Pulse)), 2f + 4f * Pulse);
			e.Graphics.DrawPath(pen3, path2);
		}
		using GraphicsPath path4 = PaintHelpers.RoundRect(new RectangleF(rectangle.Left + UiTheme.Sf(7), rectangle.Top + UiTheme.Sf(11), UiTheme.Sf(4), rectangle.Height - UiTheme.Sf(22)), UiTheme.Sf(2));
		using SolidBrush brush4 = new SolidBrush(UiTheme.WithAlpha(CompletedVisual ? UiTheme.TextFaint : color, CompletedVisual ? 120 : 210));
		e.Graphics.FillPath(brush4, path4);
		DrawCheckbox(e.Graphics);
		if (!editor.Visible)
		{
			Color foreColor = (CompletedVisual ? UiTheme.TextFaint : UiTheme.Text);
			TextRenderer.DrawText(e.Graphics, Task.Text, UiTheme.TaskFont, TextBounds, foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			if (CompletedVisual)
			{
				Size size = TextRenderer.MeasureText(Task.Text, UiTheme.TaskFont, new Size(TextBounds.Width, TextBounds.Height), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
				float num2 = (float)TextBounds.Top + (float)TextBounds.Height / 2f;
				float sweep = 1f - (float)Math.Pow(1f - StrikeProgress, 3);
				float lineLength = Math.Min(TextBounds.Width, size.Width) * sweep;
				if (lineLength > 0.5f)
				{
					using Pen pen4 = new Pen(UiTheme.TextFaint, 1.3f);
					e.Graphics.DrawLine(pen4, TextBounds.Left, num2, (float)TextBounds.Left + lineLength, num2);
				}
			}
			if (Task.CarryCount > 0 && !CompletedVisual)
			{
				Rectangle rectangle2 = new Rectangle(TextBounds.Left, base.Height - UiTheme.S(30), UiTheme.S(92), UiTheme.S(22));
				using GraphicsPath path5 = PaintHelpers.RoundRect(rectangle2, UiTheme.Sf(5));
				using SolidBrush brush5 = new SolidBrush(UiTheme.WithAlpha(color, 24));
				e.Graphics.FillPath(brush5, path5);
				TextRenderer.DrawText(e.Graphics, "昨日延续", UiTheme.SmallFont, rectangle2, UiTheme.WithAlpha(color, 230), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			}
		}
		if (hovered || Focused)
		{
			DrawActionIcon(e.Graphics, MoreBounds, IconKind.More, moreHovered, UiTheme.Sky);
		}
		DrawDragHandle(e.Graphics);
		if (Focused && ShowFocusCues)
		{
			UiTheme.DrawFocusRing(e.Graphics, path2);
		}
		e.Graphics.Restore(gstate);
	}

	private void DrawCheckbox(Graphics graphics)
	{
		Rectangle checkBounds = CheckBounds;
		GraphicsState gstate = graphics.Save();
		float num = ((CheckPulse > 0f) ? (1f - CheckPulse) : 1f);
		float num2 = ((CheckPulse > 0f) ? (1f + (float)Math.Sin((double)num * Math.PI) * 0.22f) : 1f);
		graphics.TranslateTransform((float)checkBounds.Left + (float)checkBounds.Width / 2f, (float)checkBounds.Top + (float)checkBounds.Height / 2f);
		graphics.ScaleTransform(num2, num2);
		graphics.TranslateTransform(0f - ((float)checkBounds.Left + (float)checkBounds.Width / 2f), 0f - ((float)checkBounds.Top + (float)checkBounds.Height / 2f));
		using GraphicsPath path = PaintHelpers.RoundRect(new RectangleF((float)checkBounds.Left + 0.5f, (float)checkBounds.Top + 0.5f, checkBounds.Width - 1, checkBounds.Height - 1), UiTheme.RadiusMedium);
		using SolidBrush brush = new SolidBrush(CompletedVisual ? UiTheme.Mint : (!CompletionEnabled ? UiTheme.SurfaceMuted : (checkHovered ? UiTheme.SurfaceHover : UiTheme.Surface)));
		graphics.FillPath(brush, path);
		using Pen pen = new Pen(CompletedVisual ? UiTheme.Mint : (!CompletionEnabled ? UiTheme.Border : (checkHovered ? UiTheme.Mint : UiTheme.BorderStrong)), checkHovered ? 1.7f : 1.2f);
		graphics.DrawPath(pen, path);
		if (CompletedVisual)
		{
			using Pen pen2 = new Pen(UiTheme.OnAccent(UiTheme.Mint), UiTheme.Sf(2.4f))
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round,
				LineJoin = LineJoin.Round
			};
			var scale = checkBounds.Width / 32f;
			graphics.DrawLines(pen2,
			[
				new PointF(checkBounds.Left + 8f * scale, checkBounds.Top + 16.5f * scale),
				new PointF(checkBounds.Left + 13.5f * scale, checkBounds.Top + 21.5f * scale),
				new PointF(checkBounds.Left + 24.5f * scale, checkBounds.Top + 10f * scale)
			]);
		}
		graphics.Restore(gstate);
	}

	private static void DrawActionIcon(Graphics graphics, Rectangle bounds, IconKind icon, bool hot, Color accent)
	{
		if (hot)
		{
			using GraphicsPath path = PaintHelpers.RoundRect(new RectangleF(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4), UiTheme.RadiusSmall);
			using SolidBrush brush = new SolidBrush(UiTheme.WithAlpha(accent, 26));
			graphics.FillPath(brush, path);
		}
		IconPainter.Draw(graphics, icon, RectangleF.Inflate(bounds, -UiTheme.Sf(9), -UiTheme.Sf(9)), hot ? accent : UiTheme.TextFaint, 1.65f);
	}

	private void DrawDragHandle(Graphics graphics)
	{
		using SolidBrush brush = new SolidBrush(UiTheme.WithAlpha(dragHovered ? UiTheme.Accent(Task.AccentIndex) : UiTheme.TextFaint, dragHovered ? 220 : 118));
		var dot = UiTheme.Sf(2.7f);
		var step = UiTheme.Sf(6);
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				graphics.FillEllipse(brush, DragBounds.Left + UiTheme.Sf(8) + i * step, DragBounds.Top + UiTheme.Sf(8) + j * step, dot, dot);
			}
		}
	}
}
