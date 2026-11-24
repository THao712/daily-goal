using System.Text.RegularExpressions;

namespace DailyGoal;

internal enum CaptureTarget
{
    CurrentView,
    Today,
    Tomorrow,
    Inbox
}

internal sealed record QuickCaptureResult(
    string Text,
    CaptureTarget Target,
    int? AccentIndex,
    bool IsPinned,
    RecurrenceRule? Recurrence);

/// <summary>
/// 输入框轻量快录语法解析：
/// - 开头 `!` 或 `！` 或 `★` → 设为重点置顶
/// - 开头 `明天 ` / `明日 ` 或结尾 `@明天` → 排入明日
/// - 开头 `今天 ` / `今日 ` 或结尾 `@今天` → 排入今日
/// - 开头 `备忘 ` / `稍后 ` / `待办箱 ` 或结尾 `@备忘` / `@待办箱` → 放入待办箱
/// - 开头 `每天 ` / `每日 ` → 自动建立每日重复；开头 `工作日 ` → 工作日重复
/// - 标签 `#珊瑚` / `#薄荷` / `#晴空` / `#暖阳` / `#紫罗兰` → 指定强调色
/// 若剥离标记后正文为空，则保留原始文本，避免误吞用户的正常文字。
/// </summary>
internal static class QuickCaptureParser
{
    private static readonly (string Token, int Index)[] ColorTokens =
    [
        ("#珊瑚", 0),
        ("#薄荷", 1),
        ("#晴空", 2),
        ("#暖阳", 3),
        ("#紫罗兰", 4)
    ];

    public static QuickCaptureResult Parse(string rawInput)
    {
        var original = (rawInput ?? "").Trim();
        if (string.IsNullOrEmpty(original))
        {
            return new QuickCaptureResult("", CaptureTarget.CurrentView, null, false, null);
        }

        var text = original;
        var target = CaptureTarget.CurrentView;
        int? accentIndex = null;
        var isPinned = false;
        RecurrenceRule? recurrence = null;

        // 1. 颜色标签（支持出现在任意位置，如 "写周报 #薄荷"）
        foreach (var (token, index) in ColorTokens)
        {
            var position = text.IndexOf(token, StringComparison.Ordinal);
            if (position >= 0)
            {
                accentIndex = index;
                text = (text[..position] + " " + text[(position + token.Length)..]).Trim();
                break;
            }
        }

        // 2. 尾部目标标签（@明天 / @今天 / @备忘 / @待办箱 / @稍后）
        text = StripTrailingTarget(text, "@明天", CaptureTarget.Tomorrow, ref target);
        text = StripTrailingTarget(text, "@明日", CaptureTarget.Tomorrow, ref target);
        text = StripTrailingTarget(text, "@今天", CaptureTarget.Today, ref target);
        text = StripTrailingTarget(text, "@今日", CaptureTarget.Today, ref target);
        text = StripTrailingTarget(text, "@备忘", CaptureTarget.Inbox, ref target);
        text = StripTrailingTarget(text, "@待办箱", CaptureTarget.Inbox, ref target);
        text = StripTrailingTarget(text, "@稍后", CaptureTarget.Inbox, ref target);

        // 3. 开头标记可组合（如 "!明天 交报告" 或 "每天 !背单词"）
        var matched = true;
        while (matched && text.Length > 0)
        {
            matched = false;
            if (!isPinned && (text.StartsWith('!') || text.StartsWith('！') || text.StartsWith('★')))
            {
                isPinned = true;
                text = text[1..].TrimStart();
                matched = true;
                continue;
            }

            if (target == CaptureTarget.CurrentView && TryStripPrefix(ref text, ["明天", "明日"]))
            {
                target = CaptureTarget.Tomorrow;
                matched = true;
                continue;
            }

            if (target == CaptureTarget.CurrentView && TryStripPrefix(ref text, ["今天", "今日"]))
            {
                target = CaptureTarget.Today;
                matched = true;
                continue;
            }

            if (target == CaptureTarget.CurrentView && TryStripPrefix(ref text, ["备忘", "待办箱", "稍后"]))
            {
                target = CaptureTarget.Inbox;
                matched = true;
                continue;
            }

            if (recurrence is null && TryStripPrefix(ref text, ["每天", "每日"]))
            {
                recurrence = RecurrenceRule.Daily;
                matched = true;
                continue;
            }

            if (recurrence is null && TryStripPrefix(ref text, ["工作日"]))
            {
                recurrence = RecurrenceRule.Weekdays;
                matched = true;
            }
        }

        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new QuickCaptureResult(original, CaptureTarget.CurrentView, null, false, null);
        }

        // 重复任务只能落在具体日期（非待办箱）
        if (recurrence is not null && target == CaptureTarget.Inbox)
        {
            target = CaptureTarget.Today;
        }

        return new QuickCaptureResult(text, target, accentIndex, isPinned, recurrence);
    }

    private static string StripTrailingTarget(string text, string suffix, CaptureTarget value, ref CaptureTarget target)
    {
        if (target != CaptureTarget.CurrentView || !text.EndsWith(suffix, StringComparison.Ordinal))
        {
            return text;
        }

        var candidate = text[..^suffix.Length].TrimEnd();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return text;
        }

        target = value;
        return candidate;
    }

    private static bool TryStripPrefix(ref string text, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal) || text.Length <= prefix.Length)
            {
                continue;
            }

            var next = text[prefix.Length];
            // 要求前缀后有空格、冒号或星标/感叹号，避免误伤"明天会议室预定"这类完整名词短语
            if (char.IsWhiteSpace(next) || next is ':' or '：' or '!' or '！' or '★')
            {
                var rest = text[prefix.Length..].TrimStart();
                if (rest.StartsWith(':') || rest.StartsWith('：'))
                {
                    rest = rest[1..].TrimStart();
                }

                if (!string.IsNullOrWhiteSpace(rest))
                {
                    text = rest;
                    return true;
                }
            }
        }

        return false;
    }
}
