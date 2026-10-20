namespace DailyGoal;

internal interface IAppClock
{
    DateTimeOffset Now { get; }
    DateOnly Today { get; }
}

internal sealed class SystemAppClock : IAppClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}

internal sealed class ScheduleService
{
    public bool ApplyRollover(AppState state, DateOnly today)
    {
        var changed = state.LastOpenedDate != today;
        state.LastOpenedDate = today;

        var carried = state.Tasks
            .Where(task => task.IsActive && task.Day < today)
            .OrderBy(task => task.Day)
            .ThenBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .ToArray();
        var planned = state.TasksForDate(today).ToArray();
        var order = 0;
        foreach (var task in carried)
        {
            task.Day = today;
            task.CarryCount += 1;
            task.SortOrder = order++;
            changed = true;
        }
        foreach (var task in planned)
        {
            if (task.SortOrder != order)
            {
                task.SortOrder = order;
                changed = true;
            }
            order += 1;
        }

        return changed;
    }
}

internal sealed class RecurrenceService
{
    public bool EnsureTasksForDate(AppState state, DateOnly date, DateTimeOffset now)
    {
        var changed = false;
        var nextOrder = state.TasksForDate(date).Count();
        foreach (var recurring in state.RecurringTasks.Where(recurring => recurring.Enabled))
        {
            if (!IsDue(recurring, date))
            {
                continue;
            }

            var exists = state.Tasks.Any(task => task.RecurrenceId == recurring.Id && task.Day == date);
            if (!exists && recurring.LastGeneratedDate != date)
            {
                state.Tasks.Add(new TaskRecord
                {
                    Id = TaskRecord.NewId(),
                    Text = recurring.Text,
                    CreatedAt = now,
                    Day = date,
                    SortOrder = nextOrder++,
                    AccentIndex = recurring.AccentIndex,
                    RecurrenceId = recurring.Id
                });
                changed = true;
            }

            if (recurring.LastGeneratedDate != date)
            {
                recurring.LastGeneratedDate = date;
                changed = true;
            }
        }

        return changed;
    }

    public static bool IsDue(RecurringTask recurring, DateOnly date)
    {
        return recurring.Rule switch
        {
            RecurrenceRule.Daily => true,
            RecurrenceRule.Weekdays => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
            RecurrenceRule.Weekly => recurring.EffectiveWeeklyDays.Contains(date.DayOfWeek),
            _ => false
        };
    }
}
