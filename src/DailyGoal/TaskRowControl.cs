using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class TaskRowControl : Panel
{
	private enum PressTarget
	{
		None,
		Check,
		More,
		Drag,
		DetailsToggle,
		SubTask,
		Notes
	}

	private readonly TextBox editor = new TextBox();

	private bool hovered;

	private bool checkHovered;

	private bool moreHovered;

	private bool dragHovered;

	private bool detailsHovered;

	private int hoveredSubTaskIndex = -1;

	private bool dragCandidate;

	private bool customDragging;

	private Point dragStart;

	private PressTarget pressedTarget;

	private int pressedSubTaskIndex = -1;

	private bool completedVisual;

	private float pulse;

	private float checkPulse;

	private bool isDragPlaceholder;

	private float strikeProgress = 1f;

	private bool detailsExpanded;

	private Rectangle detailsToggleBounds = Rectangle.Empty;

	private Rectangle notesDrawerBounds = Rectangle.Empty;

	private readonly List<(Rectangle Bounds, string Id)> subTaskHitRects = [];

	public TaskRecord Task { get; }

	public bool Editing => editor.Visible;

	public static int BaseHeight => UiTheme.S(82);

	public bool HasDetails => !string.IsNullOrWhiteSpace(Task.Notes) || Task.SubTasks is { Count: > 0 };

	private bool HasMetaBadges => !CompletedVisual && (
		Task.IsPinned
		|| Task.CarryCount > 0
		|| Task.RecurrenceId is not null
		|| Task.SubTasks is { Count: > 0 }
		|| !string.IsNullOrWhiteSpace(Task.Notes)
		|| Task.FocusMinutes > 0);

	public int PreferredHeight
	{
		get
		{
			if (!detailsExpanded || !HasDetails || CompletedVisual)
			{
				return BaseHeight;
			}

			var extra = UiTheme.S(18);
			if (!string.IsNullOrWhiteSpace(Task.Notes))
			{
				extra += UiTheme.S(26);
			}

			if (Task.SubTasks is { Count: > 0 })
			{
				extra += Task.SubTasks.Count * UiTheme.S(28);
			}

			return BaseHeight + extra;
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool DetailsExpanded
	{
		get => detailsExpanded;
		set
		{
			var next = value && HasDetails && !CompletedVisual;
			if (detailsExpanded != next)
			{
				detailsExpanded = next;
				Height = PreferredHeight;
				Invalidate();
			}
		}
	}

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
		get => completedVisual;
		set
		{
			completedVisual = value;
			if (completedVisual && detailsExpanded)
			{
				detailsExpanded = false;
				Height = BaseHeight;
			}
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public float Pulse
	{
		get => pulse;
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
		get => checkPulse;
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
		get => strikeProgress;
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
		get => isDragPlaceholder;
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
		HasMetaBadges ? UiTheme.S(9) : UiTheme.S(10),
		Math.Max(60, base.Width - UiTheme.S((hovered || Focused) ? 170 : 116)),
		HasMetaBadges ? UiTheme.S(35) : (BaseHeight - UiTheme.S(20)));

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

	public event EventHandler<Point>? ContextMenuRequested;

	public event EventHandler? DeleteRequested;

	public event EventHandler<string>? SubTaskToggled;

	public event EventHandler? DetailsEditRequested;

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

	public void RefreshDetailsLayout()
	{
		if (!HasDetails && detailsExpanded)
		{
			detailsExpanded = false;
		}
		Height = PreferredHeight;
		Invalidate();
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

	private int HitTestSubTask(Point location)
	{
		if (!detailsExpanded)
		{
			return -1;
		}

		for (var index = 0; index < subTaskHitRects.Count; index += 1)
		{
			if (subTaskHitRects[index].Bounds.Contains(location))
			{
				return index;
			}
		}

		return -1;
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
		detailsHovered = HasDetails && !detailsToggleBounds.IsEmpty && detailsToggleBounds.Contains(e.Location);
		hoveredSubTaskIndex = HitTestSubTask(e.Location);
		var overNotes = detailsExpanded && !notesDrawerBounds.IsEmpty && notesDrawerBounds.Contains(e.Location);
		Cursor = dragHovered
			? Cursors.SizeAll
			: (checkHovered || moreHovered || detailsHovered || hoveredSubTaskIndex >= 0 || overNotes)
				? Cursors.Hand
				: Cursors.Default;
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
		detailsHovered = false;
		hoveredSubTaskIndex = -1;
		if (!dragCandidate)
		{
			pressedTarget = PressTarget.None;
			pressedSubTaskIndex = -1;
		}
		Cursor = Cursors.Default;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Right)
		{
			Focus();
			if (!CompletedVisual && !IsRemoving)
			{
				ContextMenuRequested?.Invoke(this, PointToScreen(e.Location));
			}
			base.OnMouseDown(e);
			return;
		}

		if (e.Button == MouseButtons.Left)
		{
			Focus();
			pressedSubTaskIndex = -1;
			var subIndex = HitTestSubTask(e.Location);
			if (subIndex >= 0)
			{
				pressedTarget = PressTarget.SubTask;
				pressedSubTaskIndex = subIndex;
			}
			else if (detailsExpanded && !notesDrawerBounds.IsEmpty && notesDrawerBounds.Contains(e.Location))
			{
				pressedTarget = PressTarget.Notes;
			}
			else if (HasDetails && !detailsToggleBounds.IsEmpty && detailsToggleBounds.Contains(e.Location))
			{
				pressedTarget = PressTarget.DetailsToggle;
			}
			else if (CheckBounds.Contains(e.Location) && !CompletedVisual && CompletionEnabled)
			{
				pressedTarget = PressTarget.Check;
			}
			else if (MoreBounds.Contains(e.Location))
			{
				pressedTarget = PressTarget.More;
			}
			else if (DragBounds.Contains(e.Location) && !CompletedVisual)
			{
				pressedTarget = PressTarget.Drag;
				dragCandidate = true;
				dragStart = e.Location;
			}
			else
			{
				pressedTarget = PressTarget.None;
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
				pressedSubTaskIndex = -1;
				base.Capture = false;
				this.TaskDragEnded?.Invoke(this, new TaskDragEventArgs(PointToScreen(e.Location)));
				Invalidate();
				base.OnMouseUp(e);
				return;
			}
			PressTarget pressTarget = pressedTarget;
			int subIndex = pressedSubTaskIndex;
			pressedTarget = PressTarget.None;
			pressedSubTaskIndex = -1;
			if (dragCandidate)
			{
				dragCandidate = false;
			}
			if (pressTarget == PressTarget.SubTask && subIndex >= 0 && subIndex < subTaskHitRects.Count && subTaskHitRects[subIndex].Bounds.Contains(e.Location))
			{
				SubTaskToggled?.Invoke(this, subTaskHitRects[subIndex].Id);
			}
			else if (pressTarget == PressTarget.Notes && !notesDrawerBounds.IsEmpty && notesDrawerBounds.Contains(e.Location))
			{
				DetailsEditRequested?.Invoke(this, EventArgs.Empty);
			}
			else if (pressTarget == PressTarget.DetailsToggle && !detailsToggleBounds.IsEmpty && detailsToggleBounds.Contains(e.Location))
			{
				DetailsExpanded = !DetailsExpanded;
			}
			else if (pressTarget == PressTarget.Check && CheckBounds.Contains(e.Location) && !CompletedVisual)
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
		else if (e.KeyCode == Keys.Delete && !CompletedVisual)
		{
			if (DeleteRequested is not null)
			{
				DeleteRequested.Invoke(this, EventArgs.Empty);
			}
			else
			{
				MoreRequested?.Invoke(this, EventArgs.Empty);
			}
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
		detailsToggleBounds = Rectangle.Empty;
		notesDrawerBounds = Rectangle.Empty;
		subTaskHitRects.Clear();

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
		Color borderColor = hovered
			? Color.FromArgb(176, color)
			: (Task.IsPinned && !CompletedVisual)
				? UiTheme.WithAlpha(UiTheme.Sun, 155)
				: UiTheme.Border;
		using Pen pen2 = new Pen(borderColor, hovered ? 1.4f : (Task.IsPinned && !CompletedVisual ? 1.25f : 1f));
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
		using SolidBrush brush4 = new SolidBrush(UiTheme.WithAlpha(CompletedVisual ? UiTheme.TextFaint : (Task.IsPinned ? UiTheme.Sun : color), CompletedVisual ? 120 : 210));
		e.Graphics.FillPath(brush4, path4);
		DrawCheckbox(e.Graphics);

		if (!editor.Visible)
		{
			Color foreColor = (CompletedVisual ? UiTheme.TextFaint : UiTheme.Text);
			var textFlags = HasMetaBadges
				? TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter
				: TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
			TextRenderer.DrawText(e.Graphics, Task.Text, UiTheme.TaskFont, TextBounds, foreColor, textFlags);
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

			if (HasMetaBadges)
			{
				DrawMetaBadges(e.Graphics, color);
			}
		}

		if (detailsExpanded && HasDetails && !CompletedVisual)
		{
			DrawDetailsDrawer(e.Graphics, rectangle);
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

	private void DrawMetaBadges(Graphics graphics, Color accentColor)
	{
		var x = TextBounds.Left;
		var y = BaseHeight - UiTheme.S(31);
		var h = UiTheme.S(22);
		var maxRight = MoreBounds.Left - UiTheme.S(8);
		var gap = UiTheme.S(6);

		void DrawBadge(string text, Color tint, bool interactive = false, bool drawRepeatIcon = false)
		{
			var textWidth = TextRenderer.MeasureText(text, UiTheme.SmallFont, Size.Empty, TextFormatFlags.NoPadding).Width;
			var iconExtra = drawRepeatIcon ? UiTheme.S(16) : 0;
			var pillWidth = textWidth + UiTheme.S(14) + iconExtra;
			if (x + pillWidth > maxRight)
			{
				return;
			}

			var rect = new Rectangle(x, y, pillWidth, h);
			using var pillPath = PaintHelpers.RoundRect(rect, UiTheme.Sf(5));
			var alpha = interactive && detailsHovered ? 48 : 26;
			using var pillBrush = new SolidBrush(UiTheme.WithAlpha(tint, alpha));
			graphics.FillPath(pillBrush, pillPath);
			if (interactive && detailsExpanded)
			{
				using var pillPen = new Pen(UiTheme.WithAlpha(tint, 130), 1f);
				graphics.DrawPath(pillPen, pillPath);
			}

			if (drawRepeatIcon)
			{
				IconPainter.Draw(graphics, IconKind.Repeat, new RectangleF(rect.Left + UiTheme.Sf(5), rect.Top + UiTheme.Sf(4), UiTheme.Sf(13), UiTheme.Sf(13)), UiTheme.WithAlpha(tint, 230), 1.5f);
				TextRenderer.DrawText(graphics, text, UiTheme.SmallFont, new Rectangle(rect.Left + UiTheme.S(19), rect.Top, rect.Width - UiTheme.S(22), rect.Height), UiTheme.WithAlpha(tint, 230), TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			}
			else
			{
				TextRenderer.DrawText(graphics, text, UiTheme.SmallFont, rect, UiTheme.WithAlpha(tint, 235), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			}

			if (interactive)
			{
				detailsToggleBounds = detailsToggleBounds.IsEmpty ? rect : Rectangle.Union(detailsToggleBounds, rect);
			}

			x += pillWidth + gap;
		}

		if (Task.IsPinned)
		{
			DrawBadge("★ 重点", UiTheme.Sun);
		}

		if (Task.CarryCount > 0)
		{
			var carryText = Task.CarryCount == 1 ? "昨日延续" : $"延续 {Task.CarryCount} 天";
			DrawBadge(carryText, accentColor);
		}

		if (Task.RecurrenceId is not null)
		{
			DrawBadge("重复", UiTheme.Sky, drawRepeatIcon: true);
		}

		if (Task.SubTasks is { Count: > 0 } subs)
		{
			var done = subs.Count(item => item.Completed);
			var arrow = detailsExpanded ? "▴" : "▾";
			DrawBadge($"步骤 {done}/{subs.Count} {arrow}", UiTheme.Mint, interactive: true);
		}
		else if (!string.IsNullOrWhiteSpace(Task.Notes))
		{
			var arrow = detailsExpanded ? "▴" : "▾";
			DrawBadge($"备注 {arrow}", UiTheme.Violet, interactive: true);
		}

		if (Task.FocusMinutes > 0)
		{
			DrawBadge($"专注 {Task.FocusMinutes}m", UiTheme.Sun);
		}
	}

	private void DrawDetailsDrawer(Graphics graphics, RectangleF cardRect)
	{
		var left = TextBounds.Left;
		var right = (int)cardRect.Right - UiTheme.S(16);
		var width = Math.Max(UiTheme.S(100), right - left);
		var y = BaseHeight - UiTheme.S(2);

		using (var sep = new Pen(UiTheme.WithAlpha(UiTheme.Border, 160), 1f))
		{
			graphics.DrawLine(sep, left, y, right, y);
		}

		y += UiTheme.S(6);
		if (!string.IsNullOrWhiteSpace(Task.Notes))
		{
			notesDrawerBounds = new Rectangle(left, y, width, UiTheme.S(24));
			TextRenderer.DrawText(
				graphics,
				$"备注：{Task.Notes.ReplaceLineEndings(" ")}",
				UiTheme.SmallFont,
				notesDrawerBounds,
				UiTheme.TextMuted,
				TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
			y += UiTheme.S(26);
		}

		if (Task.SubTasks is { Count: > 0 } subs)
		{
			for (var index = 0; index < subs.Count; index += 1)
			{
				var sub = subs[index];
				var rowRect = new Rectangle(left, y, width, UiTheme.S(26));
				subTaskHitRects.Add((rowRect, sub.Id));
				if (index == hoveredSubTaskIndex)
				{
					using var hoverPath = PaintHelpers.RoundRect(rowRect, UiTheme.Sf(5));
					using var hoverBrush = new SolidBrush(UiTheme.WithAlpha(UiTheme.Mint, 24));
					graphics.FillPath(hoverBrush, hoverPath);
				}

				var boxSize = UiTheme.S(15);
				var boxRect = new Rectangle(rowRect.Left + UiTheme.S(4), rowRect.Top + (rowRect.Height - boxSize) / 2, boxSize, boxSize);
				using (var boxPath = PaintHelpers.RoundRect(boxRect, UiTheme.Sf(4)))
				{
					using var fill = new SolidBrush(sub.Completed ? UiTheme.Mint : UiTheme.SurfaceMuted);
					graphics.FillPath(fill, boxPath);
					using var border = new Pen(sub.Completed ? UiTheme.Mint : UiTheme.BorderStrong, 1.1f);
					graphics.DrawPath(border, boxPath);
				}

				if (sub.Completed)
				{
					using var checkPen = new Pen(UiTheme.OnAccent(UiTheme.Mint), UiTheme.Sf(1.7f))
					{
						StartCap = LineCap.Round,
						EndCap = LineCap.Round,
						LineJoin = LineJoin.Round
					};
					graphics.DrawLines(checkPen,
					[
						new PointF(boxRect.Left + boxSize * 0.24f, boxRect.Top + boxSize * 0.52f),
						new PointF(boxRect.Left + boxSize * 0.44f, boxRect.Top + boxSize * 0.72f),
						new PointF(boxRect.Left + boxSize * 0.78f, boxRect.Top + boxSize * 0.30f)
					]);
				}

				var textRect = new Rectangle(boxRect.Right + UiTheme.S(8), rowRect.Top, Math.Max(40, rowRect.Width - boxSize - UiTheme.S(16)), rowRect.Height);
				TextRenderer.DrawText(
					graphics,
					sub.Text,
					UiTheme.SmallFont,
					textRect,
					sub.Completed ? UiTheme.TextFaint : UiTheme.Text,
					TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
				if (sub.Completed)
				{
					var measured = TextRenderer.MeasureText(sub.Text, UiTheme.SmallFont, textRect.Size, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
					var strikeY = textRect.Top + textRect.Height / 2f;
					using var strikePen = new Pen(UiTheme.TextFaint, 1f);
					graphics.DrawLine(strikePen, textRect.Left, strikeY, textRect.Left + Math.Min(textRect.Width, measured.Width), strikeY);
				}

				y += UiTheme.S(28);
			}
		}
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
