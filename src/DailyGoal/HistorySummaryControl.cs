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
		var cell = UiTheme.Sf(12f);
		var gap = UiTheme.Sf(3.4f);
		var gridWidth = 5 * cell + 4 * gap;
		var gridHeight = 7 * cell + 6 * gap;
		var gridLeft = base.Width - UiTheme.S(18) - gridWidth;
		var gridTop = Math.Max(UiTheme.Sf(40), ((float)base.Height + UiTheme.Sf(28) - gridHeight) / 2f);
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
				continue;
			}

			var alpha = value switch
			{
				1 => 92,
				2 or 3 => 158,
				_ => 225
			};
			using var heatBrush = new SolidBrush(UiTheme.WithAlpha(UiTheme.Mint, alpha));
			e.Graphics.FillPath(heatBrush, cellPath);
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
			using GraphicsPath path2 = PaintHelpers.RoundRect(
				new RectangleF(chartLeft + (float)i * num5 + num5 / 2f - UiTheme.Sf(5), chartTop + chartHeight - num6, UiTheme.Sf(10), num6),
				UiTheme.Sf(4));
			using SolidBrush brush2 = new SolidBrush(UiTheme.WithAlpha(UiTheme.Accent(i), (DailyCounts[i] == 0) ? 54 : 205));
			e.Graphics.FillPath(brush2, path2);
		}
	}
}
