using System.Globalization;
using System.Text;

namespace DailyGoal;

/// <summary>生成干净的 Markdown 日报与近 7 天周报，方便复制到笔记或复盘文档。</summary>
internal static class ReportExporter
{
    public static string BuildDailyMarkdown(AppState state, DateOnly today)
    {
        var culture = new CultureInfo("zh-CN");
        var completedToday = state.HistoryTasks
            .Where(task => task.CompletedAt is not null
                && DateOnly.FromDateTime(task.CompletedAt.Value.LocalDateTime) == today)
            .OrderBy(task => task.CompletedAt)
            .ToArray();
        var remainingToday = state.TasksForDate(today).ToArray();
        var totalFocusMinutes = completedToday.Sum(task => task.FocusMinutes)
            + remainingToday.Sum(task => task.FocusMinutes);

        var builder = new StringBuilder();
        builder.AppendLine($"# Daily Goal 日报 · {today.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd dddd", culture)}");
        builder.AppendLine();
        builder.AppendLine($"- 已完成：**{completedToday.Length}** 项  |  待办剩余：**{remainingToday.Length}** 项"
            + (totalFocusMinutes > 0 ? $"  |  专注累计：**{totalFocusMinutes} 分钟**" : ""));
        builder.AppendLine();

        builder.AppendLine("## 今日已完成");
        if (completedToday.Length == 0)
        {
            builder.AppendLine("- （暂无）");
        }
        else
        {
            foreach (var task in completedToday)
            {
                var time = task.CompletedAt?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";
                var focusSuffix = task.FocusMinutes > 0 ? $"（专注 {task.FocusMinutes} 分钟）" : "";
                builder.AppendLine($"- [x] {task.Text} `{time}`{focusSuffix}");
                AppendNotesAndSubTasks(builder, task);
            }
        }

        if (remainingToday.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## 尚未完成");
            foreach (var task in remainingToday)
            {
                var pin = task.IsPinned ? "★ " : "";
                var focusSuffix = task.FocusMinutes > 0 ? $"（已专注 {task.FocusMinutes} 分钟）" : "";
                builder.AppendLine($"- [ ] {pin}{task.Text}{focusSuffix}");
                AppendNotesAndSubTasks(builder, task);
            }
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    public static string BuildWeeklyMarkdown(AppState state, DateOnly today, int days = 7)
    {
        var culture = new CultureInfo("zh-CN");
        var span = Math.Max(1, days);
        var start = today.AddDays(-(span - 1));
        var completed = state.HistoryTasks
            .Where(task => task.CompletedAt is not null)
            .Select(task => (Task: task, Day: DateOnly.FromDateTime(task.CompletedAt!.Value.LocalDateTime)))
            .Where(entry => entry.Day >= start && entry.Day <= today)
            .OrderByDescending(entry => entry.Day)
            .ThenBy(entry => entry.Task.CompletedAt)
            .ToArray();
        var totalFocus = completed.Sum(entry => entry.Task.FocusMinutes);

        var builder = new StringBuilder();
        builder.AppendLine($"# Daily Goal 近 {span} 天周报（{start:yyyy-MM-dd} ~ {today:yyyy-MM-dd}）");
        builder.AppendLine();
        builder.AppendLine($"- 累计完成：**{completed.Length}** 项"
            + (totalFocus > 0 ? $"  |  专注累计：**{totalFocus} 分钟**" : ""));
        builder.AppendLine();

        if (completed.Length == 0)
        {
            builder.AppendLine("（近 7 天暂无完成记录）");
            return builder.ToString();
        }

        foreach (var group in completed.GroupBy(entry => entry.Day))
        {
            var header = group.Key.ToDateTime(TimeOnly.MinValue).ToString("M 月 d 日 dddd", culture);
            builder.AppendLine($"## {header}（{group.Count()} 项）");
            foreach (var (task, _) in group)
            {
                var time = task.CompletedAt?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";
                var focusSuffix = task.FocusMinutes > 0 ? $"（专注 {task.FocusMinutes} 分钟）" : "";
                builder.AppendLine($"- [x] {task.Text} `{time}`{focusSuffix}");
                AppendNotesAndSubTasks(builder, task);
            }
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendNotesAndSubTasks(StringBuilder builder, TaskRecord task)
    {
        if (!string.IsNullOrWhiteSpace(task.Notes))
        {
            builder.AppendLine($"  - 备注：{task.Notes.ReplaceLineEndings(" / ")}");
        }

        if (task.SubTasks is { Count: > 0 })
        {
            foreach (var sub in task.SubTasks)
            {
                builder.AppendLine($"  - [{(sub.Completed ? "x" : " ")}] {sub.Text}");
            }
        }
    }
}
