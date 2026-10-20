namespace DailyGoal;

internal static class PlanningSelfTests
{
    public static string Run(DateOnly today)
    {
        var rollover = VerifyRolloverOrder(today);
        var recurrence = VerifyRecurrenceRules(today);
        var multiWeekly = VerifyMultiWeekly(today);
        var missedDates = VerifyMissedDates(today);
        var isolation = VerifyDateIsolation(today);
        if (!rollover || !recurrence || !multiWeekly || !missedDates || !isolation)
        {
            throw new InvalidOperationException(
                $"Planning self-test failed: rollover={rollover}, recurrence={recurrence}, multiWeekly={multiWeekly}, "
                + $"missedDates={missedDates}, isolation={isolation}.");
        }

        return $"rolloverOrder={rollover}; recurrence={recurrence}; multiWeekly={multiWeekly}; missedDates={missedDates}; dateIsolation={isolation}";
    }

    /// <summary>每周多选（周一 + 周六）应在两个选中日都生成，未选中的周日不生成；旧数据无 WeeklyDays 时回退单日。</summary>
    private static bool VerifyMultiWeekly(DateOnly today)
    {
        var monday = today.AddDays(((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7);
        var saturday = monday.AddDays(5);
        var sunday = monday.AddDays(6);
        var series = new RecurringTask
        {
            Id = "multi-weekly",
            Text = "multi weekly",
            AccentIndex = 0,
            Rule = RecurrenceRule.Weekly,
            WeeklyDay = DayOfWeek.Monday,
            WeeklyDays = [DayOfWeek.Monday, DayOfWeek.Saturday],
            CreatedAt = DateTimeOffset.Now.AddDays(-10),
            Enabled = true
        };
        var state = new AppState
        {
            LastOpenedDate = monday,
            RecurringTasks = [series]
        };
        var service = new RecurrenceService();
        var mondayDue = service.EnsureTasksForDate(state, monday, DateTimeOffset.Now);
        series.LastGeneratedDate = null;
        var saturdayDue = service.EnsureTasksForDate(state, saturday, DateTimeOffset.Now);
        series.LastGeneratedDate = null;
        var sundayCount = state.TasksForDate(sunday).Count();
        service.EnsureTasksForDate(state, sunday, DateTimeOffset.Now);
        var sundaySkipped = state.TasksForDate(sunday).Count() == sundayCount;

        var legacy = new RecurringTask
        {
            Id = "legacy-weekly",
            Text = "legacy weekly",
            Rule = RecurrenceRule.Weekly,
            WeeklyDay = DayOfWeek.Saturday,
            WeeklyDays = null,
            Enabled = true
        };
        var legacyFallback = RecurrenceService.IsDue(legacy, saturday) && !RecurrenceService.IsDue(legacy, monday);

        var normalizeState = new AppState
        {
            LastOpenedDate = today,
            RecurringTasks =
            [
                new RecurringTask
                {
                    Id = "normalize-weekly",
                    Text = "normalize weekly",
                    Rule = RecurrenceRule.Weekly,
                    WeeklyDay = DayOfWeek.Friday,
                    WeeklyDays = [DayOfWeek.Sunday, DayOfWeek.Wednesday, DayOfWeek.Wednesday],
                    Enabled = true
                }
            ]
        };
        normalizeState.Normalize(today);
        var normalized = normalizeState.RecurringTasks.Single();
        var normalizeValid = normalized.WeeklyDays is not null
            && normalized.WeeklyDays.SequenceEqual([DayOfWeek.Wednesday, DayOfWeek.Sunday])
            && normalized.WeeklyDay == DayOfWeek.Wednesday;

        return mondayDue && saturdayDue && sundaySkipped && legacyFallback && normalizeValid;
    }

    private static bool VerifyRolloverOrder(DateOnly today)
    {
        var state = new AppState
        {
            LastOpenedDate = today.AddDays(-1),
            Tasks =
            [
                CreateTask("carry-1", today.AddDays(-1), 0),
                CreateTask("carry-2", today.AddDays(-1), 1),
                CreateTask("planned", today, 0),
                CreateTask("tomorrow", today.AddDays(1), 0)
            ],
            RecurringTasks =
            [
                new RecurringTask
                {
                    Id = "daily-series",
                    Text = "daily",
                    AccentIndex = 2,
                    Rule = RecurrenceRule.Daily,
                    WeeklyDay = today.DayOfWeek,
                    CreatedAt = DateTimeOffset.Now.AddDays(-5),
                    Enabled = true
                }
            ]
        };
        var schedule = new ScheduleService();
        var recurrence = new RecurrenceService();
        var changed = schedule.ApplyRollover(state, today);
        changed |= recurrence.EnsureTasksForDate(state, today, DateTimeOffset.Now);
        var firstOrder = state.TasksForDate(today).Select(task => task.Id).ToArray();
        var secondPass = schedule.ApplyRollover(state, today)
            | recurrence.EnsureTasksForDate(state, today, DateTimeOffset.Now);
        return changed
            && !secondPass
            && firstOrder.SequenceEqual(["carry-1", "carry-2", "planned", state.TasksForDate(today).Last().Id])
            && state.TasksForDate(today).Last().RecurrenceId == "daily-series"
            && state.TasksForDate(today).Take(2).All(task => task.CarryCount == 1)
            && state.TasksForDate(today.AddDays(1)).Single().Id == "tomorrow";
    }

    private static bool VerifyRecurrenceRules(DateOnly today)
    {
        var monday = today.AddDays(((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7);
        var saturday = monday.AddDays(5);
        var state = new AppState
        {
            LastOpenedDate = monday,
            RecurringTasks =
            [
                Series("daily", RecurrenceRule.Daily, monday.DayOfWeek),
                Series("weekdays", RecurrenceRule.Weekdays, monday.DayOfWeek),
                Series("weekly", RecurrenceRule.Weekly, monday.DayOfWeek)
            ]
        };
        var service = new RecurrenceService();
        var mondayChanged = service.EnsureTasksForDate(state, monday, DateTimeOffset.Now);
        var mondayIds = state.TasksForDate(monday).Select(task => task.RecurrenceId).ToHashSet();

        foreach (var recurring in state.RecurringTasks)
        {
            recurring.LastGeneratedDate = null;
        }
        var saturdayChanged = service.EnsureTasksForDate(state, saturday, DateTimeOffset.Now);
        var saturdayIds = state.TasksForDate(saturday).Select(task => task.RecurrenceId).ToArray();
        return mondayChanged
            && saturdayChanged
            && mondayIds.SetEquals(["daily", "weekdays", "weekly"])
            && saturdayIds.SequenceEqual(["daily"]);
    }

    private static bool VerifyMissedDates(DateOnly today)
    {
        var state = new AppState
        {
            LastOpenedDate = today.AddDays(-5),
            RecurringTasks =
            [
                new RecurringTask
                {
                    Id = "missed-daily",
                    Text = "missed daily",
                    AccentIndex = 1,
                    Rule = RecurrenceRule.Daily,
                    LastGeneratedDate = today.AddDays(-5),
                    Enabled = true
                }
            ]
        };
        new ScheduleService().ApplyRollover(state, today);
        var service = new RecurrenceService();
        var first = service.EnsureTasksForDate(state, today, DateTimeOffset.Now);
        var second = service.EnsureTasksForDate(state, today, DateTimeOffset.Now);
        return first
            && !second
            && state.Tasks.Count == 1
            && state.Tasks[0].Day == today;
    }

    private static bool VerifyDateIsolation(DateOnly today)
    {
        var state = new AppState
        {
            LastOpenedDate = today,
            Tasks =
            [
                CreateTask("today", today, 0),
                CreateTask("tomorrow-1", today.AddDays(1), 0),
                CreateTask("tomorrow-2", today.AddDays(1), 1)
            ]
        };
        state.Normalize(today);
        return state.TasksForDate(today).Select(task => task.Id).SequenceEqual(["today"])
            && state.TasksForDate(today.AddDays(1)).Select(task => task.Id).SequenceEqual(["tomorrow-1", "tomorrow-2"]);
    }

    private static TaskRecord CreateTask(string id, DateOnly day, int order)
    {
        return new TaskRecord
        {
            Id = id,
            Text = id,
            CreatedAt = DateTimeOffset.Now.AddMinutes(order),
            Day = day,
            SortOrder = order,
            AccentIndex = order % 5
        };
    }

    private static RecurringTask Series(string id, RecurrenceRule rule, DayOfWeek weeklyDay)
    {
        return new RecurringTask
        {
            Id = id,
            Text = id,
            AccentIndex = 0,
            Rule = rule,
            WeeklyDay = weeklyDay,
            CreatedAt = DateTimeOffset.Now.AddDays(-10),
            Enabled = true
        };
    }
}
