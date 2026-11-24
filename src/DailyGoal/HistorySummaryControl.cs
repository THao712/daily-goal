using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DailyGoal;

/// <summary>
/// 历史页顶部摘要卡：近 7 天柱状图（左）+ 连续完成天数 + 近 5 周热力图（右）。
/// HeatCells 为 35 格（5 列周 × 7 行周一→周日，最后一列为本周），-1 表示超出保留期或未来。
/// </summary>
internal sealed class HistorySummaryControl : Control
{
	private int[] dailyCounts = new int[7];

	private int[] heatCells = Enumerable.Repeat(-1, 35).ToArray();

	private int streakDays;

	private DateOnly referenceDate = DateOnly.FromDateTime(DateTime.Now);

	private readonly ToolTip hoverTip = new() { InitialDelay = 120, ReshowDelay = 60, AutoPopDelay = 4000 };

	private int hoveredBarIndex = -1;

	private int hoveredHeatIndex = -1;

	private string? lastTipText;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public DateOnly ReferenceDate
	{
		get => referenceDate;
		set
		{
			if (referenceDate != value)
			{
				referenceDate = value;
				Invalidate();
			}
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int[] DailyCounts
	{
		get
		{
			return dailyCounts;
		}
		set
		{
			dailyCounts = value?.Take(7).Concat(Enumerable.Repeat(0, 7)).Take(7)
				.ToArray() ?? new int[7];
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int[] HeatCells
	{
		get
		{
			return heatCells;
		}
		set
		{
			heatCells = value?.Take(35).Concat(Enumerable.Repeat(-1, 35)).Take(35)
				.ToArray() ?? Enumerable.Repeat(-1, 35).ToArray();
			Invalidate();
		}
	}

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public int StreakDays
	{
		get
		{
			return streakDays;
		}
		set
		{
			streakDays = Math.Max(0, value);
			Invalidate();
		}
	}

	public int Total => DailyCounts.Sum();

	public HistorySummaryControl()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = UiTheme.Canvas;
	}

	protected override void OnPaintBackground(PaintEventArgs pevent)
	{
		pevent.Graphics.Clear(BackColor);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		var barHit = HitTestBar(e.Location);
		var heatHit = barHit >= 0 ? -1 : HitTestHeat(e.Location);
		if (barHit != hoveredBarIndex || heatHit != hoveredHeatIndex)
		{
			hoveredBarIndex = barHit;
			hoveredHeatIndex = heatHit;
			UpdateHoverTip();
			Invalidate();
		}

		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		if (hoveredBarIndex != -1 || hoveredHeatIndex != -1)
		{
			hoveredBarIndex = -1;
			hoveredHeatIndex = -1;
			UpdateHoverTip();
			Invalidate();
		}

		base.OnMouseLeave(e);
	}

	private void UpdateHoverTip()
	{
		string? text = null;
		if (hoveredBarIndex >= 0 && hoveredBarIndex < 7)
		{
			var day = referenceDate.AddDays(hoveredBarIndex - 6);
			text = $"{day:M 月 d 日} · 完成 {dailyCounts[hoveredBarIndex]} 项";
		}
		else if (hoveredHeatIndex >= 0 && hoveredHeatIndex < 35 && heatCells[hoveredHeatIndex] >= 0)
		{
			var day = DateForHeatCell(hoveredHeatIndex);
			text = $"{day:M 月 d 日} · 完成 {heatCells[hoveredHeatIndex]} 项";
		}

		if (!string.Equals(text, lastTipText, StringComparison.Ordinal))
		{
			lastTipText = text;
			hoverTip.SetToolTip(this, text);
		}
	}

	private DateOnly DateForHeatCell(int index)
	{
		static int MondayIndex(DayOfWeek day) => day == DayOfWeek.Sunday ? 6 : (int)day - 1;
		var currentWeekMonday = referenceDate.AddDays(-MondayIndex(referenceDate.DayOfWeek));
		var week = index / 7;
		var dayIndex = index % 7;
		return currentWeekMonday.AddDays(-7 * (4 - week) + dayIndex);
	}

	private (float GridLeft, float GridTop, float Cell, float Gap) GetHeatMetrics()
	{
		var cell = UiTheme.Sf(12f);
		var gap = UiTheme.Sf(3.4f);
		var gridWidth = 5 * cell + 4 * gap;
		var gridHeight = 7 * cell + 6 * gap;
		var gridLeft = base.Width - UiTheme.S(18) - gridWidth;
		var gridTop = Math.Max(UiTheme.Sf(40), ((float)base.Height + UiTheme.Sf(28) - gridHeight) / 2f);
		return (gridLeft, gridTop, cell, gap);
	}

	private int HitTestHeat(Point location)
	{
		var (gridLeft, gridTop, cell, gap) = GetHeatMetrics();
		for (var index = 0; index < 35; index += 1)
		{
			if (heatCells[index] < 0)
			{
				continue;
			}

			var column = index / 7;
			var rowIndex = index % 7;
			var rect = new RectangleF(
				gridLeft + column * (cell + gap) - 1.5f,
				gridTop + rowIndex * (cell + gap) - 1.5f,
				cell + 3f,
				cell + 3f);
			if (rect.Contains(location))
			{
				return index;
			}
		}

		return -1;
	}

	private int HitTestBar(Point location)
	{
		var (gridLeft, _, _, _) = GetHeatMetrics();
		var chartLeft = UiTheme.Sf(16);
		var chartTop = UiTheme.Sf(44);
		var chartHeight = (float)base.Height - chartTop - UiTheme.Sf(16);
		var chartWidth = gridLeft - UiTheme.Sf(34) - chartLeft;
		if (chartWidth <= 0 || location.X < chartLeft || location.X > chartLeft + chartWidth
			|| location.Y < chartTop || location.Y > chartTop + chartHeight)
		{
			return -1;
		}

		var step = chartWidth / 7f;
		return Math.Clamp((int)((location.X - chartLeft) / step), 0, 6);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		RectangleF cardRect = new RectangleF(1f, 1f, base.Width - 2, base.Height - 3);
		using GraphicsPath path = PaintHelpers.RoundRect(cardRect, UiTheme.RadiusMedium);
		using SolidBrush brush = new SolidBrush(UiTheme.Surface);
		e.Graphics.FillPath(brush, path);
		using Pen pen = new Pen(UiTheme.Border, 1f);
		e.Graphics.DrawPath(pen, path);
		PaintHelpers.DrawTopHighlight(e.Graphics, cardRect, UiTheme.RadiusMedium, 12);

		TextRenderer.DrawText(
			e.Graphics,
			$"近 7 天完成 {Total} 件",
			UiTheme.BodyBoldFont,
			new Rectangle(UiTheme.S(16), UiTheme.S(9), base.Width - UiTheme.S(32), UiTheme.S(26)),
			UiTheme.Text,
			TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

		var streakText = StreakDays > 0
			? StreakDays >= 30 ? "连续 30+ 天" : $"连续 {StreakDays} 天"
			: "";
		if (streakText.Length > 0)
		{
			TextRenderer.DrawText(
				e.Graphics,
				streakText,
				UiTheme.SmallBoldFont,
				new Rectangle(base.Width - UiTheme.S(150), UiTheme.S(11), UiTheme.S(134), UiTheme.S(22)),
				UiTheme.AccentText,
				TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}

		// 右侧热力图（5 列 × 7 行）。
		var (gridLeft, gridTop, cell, gap) = GetHeatMetrics();
		for (var index = 0; index < 35; index += 1)
		{
			var column = index / 7;
			var rowIndex = index % 7;
			var value = heatCells[index];
			if (value < 0)
			{
				continue;
			}

			var rect = new RectangleF(
				gridLeft + column * (cell + gap),
				gridTop + rowIndex * (cell + gap),
				cell,
				cell);
			using var cellPath = PaintHelpers.RoundRect(rect, UiTheme.Sf(3));
			if (value == 0)
			{
				using var emptyBrush = new SolidBrush(UiTheme.WithAlpha(UiTheme.Border, 110));
				e.Graphics.FillPath(emptyBrush, cellPath);
			}
			else
			{
				var alpha = value switch
				{
					1 => 92,
					2 or 3 => 158,
					_ => 225
				};
				using var heatBrush = new SolidBrush(UiTheme.WithAlpha(UiTheme.Mint, alpha));
				e.Graphics.FillPath(heatBrush, cellPath);
			}

			if (index == hoveredHeatIndex)
			{
				using var hoverPen = new Pen(UiTheme.Text, 1.4f);
				e.Graphics.DrawPath(hoverPen, cellPath);
			}
		}

		// 左侧近 7 天柱状图（避开右侧热力图区域）。
		int num = Math.Max(1, DailyCounts.Max());
		float chartLeft = UiTheme.Sf(16);
		float chartTop = UiTheme.Sf(44);
		float chartHeight = (float)base.Height - chartTop - UiTheme.Sf(16);
		float chartWidth = gridLeft - UiTheme.Sf(34) - chartLeft;
		float num5 = chartWidth / 7f;
		for (int i = 0; i < 7; i++)
		{
			float num6 = Math.Max(UiTheme.Sf(3), chartHeight * (float)DailyCounts[i] / (float)num);
			var barWidth = i == hoveredBarIndex ? UiTheme.Sf(12) : UiTheme.Sf(10);
			using GraphicsPath path2 = PaintHelpers.RoundRect(
				new RectangleF(chartLeft + (float)i * num5 + num5 / 2f - barWidth / 2f, chartTop + chartHeight - num6, barWidth, num6),
				UiTheme.Sf(4));
			using SolidBrush brush2 = new SolidBrush(UiTheme.WithAlpha(UiTheme.Accent(i), (DailyCounts[i] == 0) ? (i == hoveredBarIndex ? 120 : 54) : (i == hoveredBarIndex ? 245 : 205)));
			e.Graphics.FillPath(brush2, path2);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			hoverTip.Dispose();
		}

		base.Dispose(disposing);
	}
}
