using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace DailyGoal;

internal sealed class TaskFlowPanel : Panel
{
	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Image? ThemeWatermark { get; set; }
	private readonly List<TaskRowControl> rows = new List<TaskRowControl>();

	private readonly List<Control> footers = new List<Control>();

	private readonly Dictionary<TaskRowControl, int> targetTops = new Dictionary<TaskRowControl, int>();

	private readonly SlimScrollBar scrollBar = new SlimScrollBar();

	private readonly System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer
	{
		Interval = 15
	};

	private int dropIndicatorIndex = -1;

	private bool arranging;

	private TaskRowControl? dragPlaceholderRow;

	public IReadOnlyList<TaskRowControl> Rows => rows;

	/// <summary>底部附加控件数量（已完成分组 = 1 个头 + N 行），自检用。</summary>
	public int FooterCount => footers.Count;

	public bool IsAnimating => animationTimer.Enabled;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int DropIndicatorIndex
	{
		get
		{
			return dropIndicatorIndex;
		}
		set
		{
			int num = Math.Clamp(value, -1, rows.Count);
			if (dropIndicatorIndex != num)
			{
				dropIndicatorIndex = num;
				Invalidate();
			}
		}
	}

	public TaskFlowPanel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		scrollBar.SmallChange = UiTheme.S(34);
		scrollBar.ValueChanged += delegate
		{
			ArrangeRows(immediate: true);
		};
		base.Controls.Add(scrollBar);
		scrollBar.BringToFront();
		animationTimer.Tick += delegate
		{
			TickAnimation();
		};
	}

	public void ClearRows()
	{
		animationTimer.Stop();
		targetTops.Clear();
		arranging = true;
		try
		{
			TaskRowControl[] array = rows.ToArray();
			foreach (TaskRowControl taskRowControl in array)
			{
				base.Controls.Remove(taskRowControl);
				taskRowControl.Dispose();
			}
			foreach (Control footer in footers.ToArray())
			{
				base.Controls.Remove(footer);
				footer.Dispose();
			}
		}
		finally
		{
			arranging = false;
		}
		rows.Clear();
		footers.Clear();
		DropIndicatorIndex = -1;
		UpdateScrollBar(0);
		Invalidate();
	}

	/// <summary>底部附加区（已完成分组）：不参与拖动排序，紧跟在任务行之后排布。</summary>
	public void ClearFooters()
	{
		arranging = true;
		try
		{
			foreach (Control footer in footers.ToArray())
			{
				base.Controls.Remove(footer);
				footer.Dispose();
			}
		}
		finally
		{
			arranging = false;
		}
		footers.Clear();
		ArrangeRows(immediate: true);
	}

	public void AddFooter(Control control)
	{
		footers.Add(control);
		base.Controls.Add(control);
		ArrangeRows(immediate: true);
	}

	public int IndexOf(TaskRowControl row)
	{
		return rows.IndexOf(row);
	}

	public void SetRowIndex(TaskRowControl row, int index, bool animate = true)
	{
		int num = rows.IndexOf(row);
		if (num >= 0)
		{
			int num2 = Math.Clamp(index, 0, rows.Count - 1);
			if (num != num2)
			{
				rows.RemoveAt(num);
				rows.Insert(num2, row);
				ArrangeRows(!animate);
			}
		}
	}

	public bool ApplyOrder(IEnumerable<string> taskIds, bool animate = true)
	{
		Dictionary<string, TaskRowControl> byId = rows.ToDictionary<TaskRowControl, string>((TaskRowControl row) => row.Task.Id, StringComparer.Ordinal);
		TaskRowControl?[] array = taskIds.Select((string id) => byId.GetValueOrDefault(id)).ToArray();
		if (array.Length != rows.Count || array.Any((TaskRowControl? row) => row == null))
		{
			return false;
		}
		rows.Clear();
		rows.AddRange(array.OfType<TaskRowControl>());
		ArrangeRows(!animate);
		return true;
	}

	public int MoveDragPlaceholder(TaskRowControl row, int clientY)
	{
		if (!rows.Contains(row))
		{
			return -1;
		}
		dragPlaceholderRow = row;
		row.SendToBack();
		int num = clientY + scrollBar.Value;
		TaskRowControl[] array = rows.Where((TaskRowControl candidate) => candidate != row).ToArray();
		int value = array.Length;
		int num2 = base.Padding.Top;
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			TaskRowControl taskRowControl = array[num3];
			if (num < num2 + taskRowControl.Height / 2)
			{
				value = num3;
				break;
			}
			num2 += taskRowControl.Height + taskRowControl.Margin.Bottom;
		}
		int num4 = rows.IndexOf(row);
		rows.Remove(row);
		int num5 = Math.Clamp(value, 0, rows.Count);
		rows.Insert(num5, row);
		DropIndicatorIndex = num5;
		if (num4 != num5)
		{
			ArrangeRows(immediate: false);
		}
		return num5;
	}

	public void EndDrag()
	{
		DropIndicatorIndex = -1;
		ArrangeRows(immediate: false);
		dragPlaceholderRow = null;
	}

	public void AutoScrollForDrag(int clientY)
	{
		if (scrollBar.Visible)
		{
			int num = scrollBar.Value;
			if (clientY < UiTheme.S(42))
			{
				num -= UiTheme.S(28);
			}
			else if (clientY > base.ClientSize.Height - UiTheme.S(42))
			{
				num += UiTheme.S(28);
			}
			int max = Math.Max(scrollBar.Minimum, scrollBar.Maximum - scrollBar.LargeChange + 1);
			scrollBar.Value = Math.Clamp(num, scrollBar.Minimum, max);
		}
	}

	public void ScrollByWheel(int delta)
	{
		if (scrollBar.Visible && delta != 0)
		{
			int max = Math.Max(scrollBar.Minimum, scrollBar.Maximum - scrollBar.LargeChange + 1);
			scrollBar.Value = Math.Clamp(scrollBar.Value - Math.Sign(delta) * UiTheme.S(58), scrollBar.Minimum, max);
		}
	}

	public void ArrangeRows(bool immediate)
	{
		if (arranging || base.IsDisposed)
		{
			return;
		}
		arranging = true;
		try
		{
			int num = base.Padding.Top + base.Padding.Bottom;
			foreach (TaskRowControl row in rows)
			{
				num += row.Height + row.Margin.Bottom;
			}
			foreach (Control footer in footers)
			{
				num += footer.Margin.Top + footer.Height + footer.Margin.Bottom;
			}
			UpdateScrollBar(num);
			int num2 = (scrollBar.Visible ? (scrollBar.Width + UiTheme.S(6)) : 0);
			int width = Math.Max(80, base.ClientSize.Width - base.Padding.Horizontal - num2);
			int num3 = base.Padding.Top - scrollBar.Value;
			foreach (TaskRowControl row2 in rows)
			{
				row2.Width = width;
				targetTops[row2] = num3;
				if (immediate || row2 == dragPlaceholderRow || !base.Visible || !base.IsHandleCreated)
				{
					row2.Location = new Point(base.Padding.Left, num3);
				}
				else
				{
					row2.Left = base.Padding.Left;
				}
				num3 += row2.Height + row2.Margin.Bottom;
			}
			foreach (Control footer in footers)
			{
				num3 += footer.Margin.Top;
				footer.Width = width;
				footer.Location = new Point(base.Padding.Left, num3);
				num3 += footer.Height + footer.Margin.Bottom;
			}
			if (!immediate && rows.Count > 0)
			{
				animationTimer.Start();
			}
			scrollBar.BringToFront();
			Invalidate();
		}
		finally
		{
			arranging = false;
		}
	}

	protected override void OnControlAdded(ControlEventArgs e)
	{
		base.OnControlAdded(e);
		TaskRowControl? row = e.Control as TaskRowControl;
		if (row != null && !rows.Contains(row))
		{
			rows.Add(row);
			row.Disposed += delegate
			{
				rows.Remove(row);
				targetTops.Remove(row);
			};
			row.SizeChanged += delegate
			{
				ArrangeRows(immediate: false);
			};
			ArrangeRows(!base.IsHandleCreated);
			scrollBar.BringToFront();
		}
	}

	protected override void OnControlRemoved(ControlEventArgs e)
	{
		if (e.Control is TaskRowControl taskRowControl)
		{
			rows.Remove(taskRowControl);
			targetTops.Remove(taskRowControl);
		}
		base.OnControlRemoved(e);
		if (!arranging)
		{
			ArrangeRows(immediate: false);
		}
	}

	protected override void OnResize(EventArgs eventargs)
	{
		base.OnResize(eventargs);
		scrollBar.Bounds = new Rectangle(Math.Max(0, base.ClientSize.Width - UiTheme.S(10)), 0, UiTheme.S(10), base.ClientSize.Height);
		ArrangeRows(immediate: true);
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		ScrollByWheel(e.Delta);
		if (e is HandledMouseEventArgs handled)
		{
			handled.Handled = true;
		}
		base.OnMouseWheel(e);
	}

	private void UpdateScrollBar(int contentHeight)
	{
		bool flag = contentHeight > base.ClientSize.Height;
		scrollBar.Visible = flag;
		scrollBar.Bounds = new Rectangle(Math.Max(0, base.ClientSize.Width - UiTheme.S(10)), 0, UiTheme.S(10), base.ClientSize.Height);
		if (!flag)
		{
			scrollBar.Value = 0;
			return;
		}
		scrollBar.Minimum = 0;
		scrollBar.LargeChange = Math.Max(1, base.ClientSize.Height);
		scrollBar.Maximum = Math.Max(scrollBar.LargeChange, contentHeight - 1);
		int num = Math.Max(0, scrollBar.Maximum - scrollBar.LargeChange + 1);
		if (scrollBar.Value > num)
		{
			scrollBar.Value = num;
		}
	}

	private void TickAnimation()
	{
		bool flag = true;
		TaskRowControl[] array = rows.ToArray();
		foreach (TaskRowControl taskRowControl in array)
		{
			if (!taskRowControl.IsDisposed && targetTops.TryGetValue(taskRowControl, out var value))
			{
				int value2 = value - taskRowControl.Top;
				if (Math.Abs(value2) <= 1)
				{
					taskRowControl.Top = value;
					continue;
				}
				flag = false;
				int val = Math.Max(2, (int)Math.Ceiling((double)Math.Abs(value2) * 0.32));
				taskRowControl.Top += Math.Sign(value2) * Math.Min(Math.Abs(value2), val);
			}
		}
		Invalidate();
		if (flag)
		{
			animationTimer.Stop();
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		if (DropIndicatorIndex < 0 || rows.Count == 0 || DropIndicatorIndex >= rows.Count)
		{
			return;
		}
		int index = Math.Clamp(DropIndicatorIndex, 0, rows.Count - 1);
		float num = (float)rows[index].Top - 5f;
		int val = base.ClientSize.Width - (scrollBar.Visible ? UiTheme.S(26) : UiTheme.S(16));
		using Pen pen = new Pen(UiTheme.Mint, 3f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		e.Graphics.DrawLine(pen, UiTheme.S(15), num, Math.Max(20, val), num);
		using SolidBrush brush = new SolidBrush(UiTheme.Mint);
		e.Graphics.FillEllipse(brush, UiTheme.S(10), num - 4f, 8f, 8f);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		base.OnPaintBackground(e);
		if (UiTheme.Mode != AppThemeMode.Xuanling || ThemeWatermark is null || rows.Count == 0)
		{
			return;
		}

		e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		var size = Math.Min(UiTheme.S(205), Math.Min(base.ClientSize.Width / 2, base.ClientSize.Height / 2));
		var bounds = new Rectangle(base.ClientSize.Width - size - UiTheme.S(18), base.ClientSize.Height - size - UiTheme.S(12), size, size);
		using var attributes = new ImageAttributes();
		var matrix = new ColorMatrix { Matrix33 = 0.13f };
		attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
		e.Graphics.DrawImage(ThemeWatermark, bounds, 0, 0, ThemeWatermark.Width, ThemeWatermark.Height, GraphicsUnit.Pixel, attributes);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			animationTimer.Dispose();
		}
		base.Dispose(disposing);
	}
}
