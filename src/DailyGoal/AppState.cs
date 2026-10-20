using System.Security;
using System.Text.Json;

namespace DailyGoal;

internal enum ParticleIntensity
{
    Light,
    Standard,
    Celebration
}

internal enum RecurrenceRule
{
    Daily,
    Weekdays,
    Weekly
}

internal enum DockSide
{
    Right,
    Left
}

internal enum HotkeyChoice
{
    CtrlAltD,
    CtrlAltSpace,
    CtrlShiftD
}

internal sealed class AppSettings
{
    public const int DefaultWindowTopOffset = 12;
    public static readonly int[] UiScaleChoices = [0, 100, 125, 150];

    public bool AutoHideEnabled { get; set; } = true;
    public int AutoHideDelayMs { get; set; } = 850;
    public ParticleIntensity ParticleIntensity { get; set; } = ParticleIntensity.Celebration;
    public bool ReducedMotion { get; set; }
    public bool StartWithWindows { get; set; }
    public AppThemeMode Theme { get; set; } = AppThemeMode.Dark;
    public bool CompletedSectionCollapsed { get; set; }
    public int WindowTopOffset { get; set; } = DefaultWindowTopOffset;
    public DockSide DockSide { get; set; } = DockSide.Right;
    public string? PreferredScreenDevice { get; set; }
    public bool HotkeyEnabled { get; set; } = true;
    public HotkeyChoice Hotkey { get; set; } = HotkeyChoice.CtrlAltD;
    public bool ReminderEnabled { get; set; }
    public int ReminderHour { get; set; } = 19;
    /// <summary>界面缩放偏好；0 表示按目标显示器工作区自动适配。</summary>
    public int UiScalePercent { get; set; }

    public bool Normalize()
    {
        var changed = false;
        var normalizedDelay = Math.Clamp(AutoHideDelayMs, 500, 3000);
        if (normalizedDelay != AutoHideDelayMs)
        {
            AutoHideDelayMs = normalizedDelay;
            changed = true;
        }

        if (!Enum.IsDefined(Theme))
        {
            Theme = AppThemeMode.Dark;
            changed = true;
        }

        if (!Enum.IsDefined(DockSide))
        {
            DockSide = DockSide.Right;
            changed = true;
        }

        if (!Enum.IsDefined(Hotkey))
        {
            Hotkey = HotkeyChoice.CtrlAltD;
            changed = true;
        }

        var normalizedHour = Math.Clamp(ReminderHour, 17, 21);
        if (normalizedHour != ReminderHour)
        {
            ReminderHour = normalizedHour;
            changed = true;
        }

        var normalizedOffset = Math.Clamp(WindowTopOffset, 0, 4000);
        if (normalizedOffset != WindowTopOffset)
        {
            WindowTopOffset = normalizedOffset;
            changed = true;
        }

        if (!UiScaleChoices.Contains(UiScalePercent))
        {
            UiScalePercent = 0;
            changed = true;
        }

        return changed;
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            AutoHideEnabled = AutoHideEnabled,
            AutoHideDelayMs = AutoHideDelayMs,
            ParticleIntensity = ParticleIntensity,
            ReducedMotion = ReducedMotion,
            StartWithWindows = StartWithWindows,
            Theme = Theme,
            CompletedSectionCollapsed = CompletedSectionCollapsed,
            WindowTopOffset = WindowTopOffset,
            DockSide = DockSide,
            PreferredScreenDevice = PreferredScreenDevice,
            HotkeyEnabled = HotkeyEnabled,
            Hotkey = Hotkey,
            ReminderEnabled = ReminderEnabled,
            ReminderHour = ReminderHour,
            UiScalePercent = UiScalePercent
        };
    }
}

internal sealed class TaskRecord
{
    public string Id { get; set; } = NewId();
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateOnly Day { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public int SortOrder { get; set; }
    public int AccentIndex { get; set; } = -1;
    public int CarryCount { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? RecurrenceId { get; set; }

    public bool IsActive => CompletedAt is null;

    public static string NewId()
    {
        return $"task-{DateTimeOffset.Now.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}";
    }

    public static int AccentForId(string id)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in id)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return (int)(hash % 5);
        }
    }

    public TaskRecord Clone()
    {
        return new TaskRecord
        {
            Id = Id,
            Text = Text,
            CreatedAt = CreatedAt,
            Day = Day,
            SortOrder = SortOrder,
            AccentIndex = AccentIndex,
            CarryCount = CarryCount,
            CompletedAt = CompletedAt,
            RecurrenceId = RecurrenceId
        };
    }
}

internal sealed class TaskTemplate
{
    public string Id { get; set; } = $"template-{Guid.NewGuid():N}";
    public string Text { get; set; } = "";
    public int AccentIndex { get; set; } = -1;
    public int SortOrder { get; set; }

    public TaskTemplate Clone()
    {
        return new TaskTemplate
        {
            Id = Id,
            Text = Text,
            AccentIndex = AccentIndex,
            SortOrder = SortOrder
        };
    }
}

internal sealed class RecurringTask
{
    public string Id { get; set; } = $"recurrence-{Guid.NewGuid():N}";
    public string Text { get; set; } = "";
    public int AccentIndex { get; set; } = -1;
    public RecurrenceRule Rule { get; set; }
    public DayOfWeek WeeklyDay { get; set; }
    /// <summary>每周规则的多选星期；空/缺省时回退到 WeeklyDay（兼容旧数据）。</summary>
    public List<DayOfWeek>? WeeklyDays { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateOnly? LastGeneratedDate { get; set; }
    public bool Enabled { get; set; } = true;

    public IReadOnlyList<DayOfWeek> EffectiveWeeklyDays =>
        WeeklyDays is { Count: > 0 } ? WeeklyDays : [WeeklyDay];

    public RecurringTask Clone()
    {
        return new RecurringTask
        {
            Id = Id,
            Text = Text,
            AccentIndex = AccentIndex,
            Rule = Rule,
            WeeklyDay = WeeklyDay,
            WeeklyDays = WeeklyDays is null ? null : [.. WeeklyDays],
            CreatedAt = CreatedAt,
            LastGeneratedDate = LastGeneratedDate,
            Enabled = Enabled
        };
    }
}

internal sealed class AppState
{
    public const int CurrentVersion = 5;

    public int Version { get; set; } = CurrentVersion;
    public DateOnly LastOpenedDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    /// <summary>傍晚提醒最近一次触发（或判定无需触发）的日期，避免同日重复提醒。</summary>
    public DateOnly? LastReminderDate { get; set; }
    public List<TaskRecord> Tasks { get; set; } = [];
    public List<TaskTemplate> Templates { get; set; } = [];
    public List<RecurringTask> RecurringTasks { get; set; } = [];
    public AppSettings Settings { get; set; } = new();

    public IEnumerable<TaskRecord> ActiveTasks => Tasks
        .Where(task => task.IsActive)
        .OrderBy(task => task.SortOrder)
        .ThenBy(task => task.CreatedAt);

    public IEnumerable<TaskRecord> HistoryTasks => Tasks
        .Where(task => !task.IsActive)
        .OrderByDescending(task => task.CompletedAt);

    public IEnumerable<TaskRecord> TasksForDate(DateOnly date)
    {
        return Tasks
            .Where(task => task.IsActive && task.Day == date)
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt);
    }

    public bool Normalize(DateOnly today)
    {
        var changed = false;
        var previousVersion = Version;
        if (Version != CurrentVersion)
        {
            Version = CurrentVersion;
            changed = true;
        }

        if (Settings is null)
        {
            Settings = new AppSettings();
            changed = true;
        }

        if (Tasks is null)
        {
            Tasks = [];
            changed = true;
        }

        if (Templates is null)
        {
            Templates = [];
            changed = true;
        }

        if (RecurringTasks is null)
        {
            RecurringTasks = [];
            changed = true;
        }

        changed |= Settings.Normalize();
        var normalizedTasks = new List<TaskRecord>(Tasks.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in Tasks)
        {
            if (task is null || string.IsNullOrWhiteSpace(task.Text))
            {
                changed = true;
                continue;
            }

            var trimmedText = task.Text.Trim();
            if (!string.Equals(trimmedText, task.Text, StringComparison.Ordinal))
            {
                task.Text = trimmedText;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(task.Id) || !ids.Add(task.Id))
            {
                task.Id = TaskRecord.NewId();
                ids.Add(task.Id);
                changed = true;
            }

            if (task.CreatedAt == default)
            {
                task.CreatedAt = DateTimeOffset.Now;
                changed = true;
            }

            if (task.Day == default)
            {
                task.Day = today;
                changed = true;
            }
            else if (task.IsActive && task.Day > today.AddDays(1))
            {
                task.Day = today.AddDays(1);
                changed = true;
            }

            if (task.AccentIndex is < 0 or > 4)
            {
                task.AccentIndex = TaskRecord.AccentForId(task.Id);
                changed = true;
            }

            if (task.CarryCount < 0)
            {
                task.CarryCount = 0;
                changed = true;
            }

            normalizedTasks.Add(task);
        }

        if (previousVersion is > 0 and < 4)
        {
            foreach (var task in normalizedTasks.Where(task => task.IsActive && task.Day != today))
            {
                task.Day = today;
                changed = true;
            }
        }

        if (normalizedTasks.Count != Tasks.Count)
        {
            Tasks = normalizedTasks;
        }

        changed |= NormalizeTemplates();
        changed |= NormalizeRecurringTasks();
        var recurringIds = RecurringTasks.Select(recurring => recurring.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var task in Tasks.Where(task => task.RecurrenceId is not null && !recurringIds.Contains(task.RecurrenceId)))
        {
            task.RecurrenceId = null;
            changed = true;
        }
        changed |= NormalizeActiveSortOrders();
        return changed;
    }

    public bool ApplyDailyRollover(DateOnly today)
    {
        return new ScheduleService().ApplyRollover(this, today);
    }

    public bool PruneHistory(DateOnly today, int retentionDays = 30)
    {
        var oldestDate = today.AddDays(-(Math.Max(1, retentionDays) - 1));
        var removed = Tasks.RemoveAll(task =>
            task.CompletedAt is not null
            && DateOnly.FromDateTime(task.CompletedAt.Value.LocalDateTime) < oldestDate);
        return removed > 0;
    }

    public bool NormalizeActiveSortOrders()
    {
        var changed = false;
        foreach (var day in Tasks.Where(task => task.IsActive).Select(task => task.Day).Distinct())
        {
            changed |= NormalizeSortOrders(day);
        }

        return changed;
    }

    public bool NormalizeSortOrders(DateOnly day)
    {
        var changed = false;
        var active = TasksForDate(day).ToArray();
        for (var index = 0; index < active.Length; index += 1)
        {
            if (active[index].SortOrder == index)
            {
                continue;
            }

            active[index].SortOrder = index;
            changed = true;
        }

        return changed;
    }

    private bool NormalizeTemplates()
    {
        var changed = false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var texts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<TaskTemplate>();
        foreach (var template in Templates.OrderBy(template => template.SortOrder))
        {
            if (template is null || string.IsNullOrWhiteSpace(template.Text))
            {
                changed = true;
                continue;
            }

            template.Text = template.Text.Trim();
            if (!texts.Add(template.Text))
            {
                changed = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(template.Id) || !ids.Add(template.Id))
            {
                template.Id = $"template-{Guid.NewGuid():N}";
                ids.Add(template.Id);
                changed = true;
            }

            if (template.AccentIndex is < 0 or > 4)
            {
                template.AccentIndex = TaskRecord.AccentForId(template.Id);
                changed = true;
            }

            if (template.SortOrder != normalized.Count)
            {
                template.SortOrder = normalized.Count;
                changed = true;
            }
            normalized.Add(template);
        }

        Templates = normalized;
        return changed;
    }

    private bool NormalizeRecurringTasks()
    {
        var changed = false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<RecurringTask>();
        foreach (var recurring in RecurringTasks)
        {
            if (recurring is null || string.IsNullOrWhiteSpace(recurring.Text))
            {
                changed = true;
                continue;
            }

            recurring.Text = recurring.Text.Trim();
            if (string.IsNullOrWhiteSpace(recurring.Id) || !ids.Add(recurring.Id))
            {
                recurring.Id = $"recurrence-{Guid.NewGuid():N}";
                ids.Add(recurring.Id);
                changed = true;
            }
            if (recurring.AccentIndex is < 0 or > 4)
            {
                recurring.AccentIndex = TaskRecord.AccentForId(recurring.Id);
                changed = true;
            }
            if (!Enum.IsDefined(recurring.Rule))
            {
                recurring.Rule = RecurrenceRule.Daily;
                changed = true;
            }
            if (recurring.CreatedAt == default)
            {
                recurring.CreatedAt = DateTimeOffset.Now;
                changed = true;
            }
            if (!Enum.IsDefined(recurring.WeeklyDay))
            {
                recurring.WeeklyDay = recurring.CreatedAt.LocalDateTime.DayOfWeek;
                changed = true;
            }
            if (recurring.WeeklyDays is not null)
            {
                var cleaned = recurring.WeeklyDays
                    .Where(Enum.IsDefined)
                    .Distinct()
                    .OrderBy(day => day == DayOfWeek.Sunday ? 7 : (int)day)
                    .ToList();
                if (cleaned.Count == 0)
                {
                    recurring.WeeklyDays = null;
                    changed = true;
                }
                else
                {
                    if (!cleaned.SequenceEqual(recurring.WeeklyDays))
                    {
                        recurring.WeeklyDays = cleaned;
                        changed = true;
                    }
                    if (recurring.WeeklyDay != cleaned[0])
                    {
                        recurring.WeeklyDay = cleaned[0];
                        changed = true;
                    }
                }
            }
            normalized.Add(recurring);
        }

        RecurringTasks = normalized;
        return changed;
    }

    public AppState Clone()
    {
        return new AppState
        {
            Version = Version,
            LastOpenedDate = LastOpenedDate,
            LastReminderDate = LastReminderDate,
            Settings = Settings.Clone(),
            Tasks = Tasks.Select(task => task.Clone()).ToList(),
            Templates = Templates.Select(template => template.Clone()).ToList(),
            RecurringTasks = RecurringTasks.Select(recurring => recurring.Clone()).ToList()
        };
    }
}

internal sealed record StateLoadResult(AppState State, bool NeedsSave, bool MigratedLegacy, string? Warning);

internal readonly record struct StartupDisplaySettings(int UiScalePercent, string? PreferredScreenDevice);

internal sealed class StateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string dataDirectory;
    private readonly string dataFile;
    private readonly string backupFile;
    private bool skipBackupOnce;

    public string DataDirectory => dataDirectory;
    public string DataFile => dataFile;
    public bool WritesBlocked { get; private set; }

    private const string PointerFileName = "storage-location.txt";

    /// <summary>默认根目录（%APPDATA%\DailyGoal）；自定义数据位置的指针文件也固定放在这里。</summary>
    public static string DefaultRootDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DailyGoal");

    /// <summary>解析实际数据目录：指针文件存在且内容指向可用目录时使用自定义位置，否则用默认目录。</summary>
    public static string ResolveDataDirectory(string? rootDirectory = null)
    {
        var root = rootDirectory ?? DefaultRootDirectory;
        try
        {
            var pointerPath = Path.Combine(root, PointerFileName);
            if (!File.Exists(pointerPath))
            {
                return root;
            }

            var custom = File.ReadAllLines(pointerPath).FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(custom) || !Path.IsPathRooted(custom))
            {
                return root;
            }

            return Directory.Exists(custom) ? custom : root;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return root;
        }
    }

    /// <summary>写入或清除（path 为 null）自定义数据目录指针；重启后生效。</summary>
    public static bool TrySetCustomDataDirectory(string? path, out string? error, string? rootDirectory = null)
    {
        error = null;
        var root = rootDirectory ?? DefaultRootDirectory;
        try
        {
            Directory.CreateDirectory(root);
            var pointerPath = Path.Combine(root, PointerFileName);
            if (string.IsNullOrWhiteSpace(path))
            {
                if (File.Exists(pointerPath))
                {
                    File.Delete(pointerPath);
                }
                return true;
            }

            File.WriteAllText(pointerPath, Path.GetFullPath(path));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            error = $"无法更新数据位置：{ex.Message}";
            return false;
        }
    }

    /// <summary>在创建控件前读取显示偏好，供启动阶段选择目标屏幕与安全缩放。</summary>
    public static StartupDisplaySettings PeekStartupDisplaySettings()
    {
        try
        {
            var file = Path.Combine(ResolveDataDirectory(), "tasks.json");
            if (!File.Exists(file))
            {
                return new StartupDisplaySettings(0, null);
            }

            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var scalePercent = 0;
            string? preferredScreen = null;
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("Settings", out var settings)
                && settings.ValueKind == JsonValueKind.Object)
            {
                if (settings.TryGetProperty("UiScalePercent", out var percent)
                    && percent.TryGetInt32(out var value)
                    && AppSettings.UiScaleChoices.Contains(value))
                {
                    scalePercent = value;
                }

                if (settings.TryGetProperty("PreferredScreenDevice", out var screen)
                    && screen.ValueKind == JsonValueKind.String)
                {
                    preferredScreen = screen.GetString();
                }
            }

            return new StartupDisplaySettings(scalePercent, preferredScreen);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            return new StartupDisplaySettings(0, null);
        }
    }

    public StateStore(string? dataDirectoryOverride = null)
    {
        dataDirectory = dataDirectoryOverride ?? ResolveDataDirectory();
        dataFile = Path.Combine(dataDirectory, "tasks.json");
        backupFile = $"{dataFile}.bak";
    }

    public StateLoadResult Load(DateOnly today)
    {
        var warnings = new List<string>();
        var primaryExists = File.Exists(dataFile);
        if (TryReadState(dataFile, today, out var state, out var migratedLegacy, out _))
        {
            if (migratedLegacy)
            {
                var migrationBackup = TryPreserveLegacyFile();
                if (migrationBackup is null)
                {
                    warnings.Add("旧版任务已迁移，但无法创建独立的 v1 备份。");
                }
            }

            var changed = state.Normalize(today);
            changed |= state.ApplyDailyRollover(today);
            changed |= state.PruneHistory(today);
            return new StateLoadResult(state, changed || migratedLegacy, migratedLegacy, JoinWarnings(warnings));
        }

        if (!primaryExists)
        {
            if (TryReadState(backupFile, today, out state, out migratedLegacy, out _))
            {
                warnings.Add("任务主文件缺失，已从备份恢复。");
                state.Normalize(today);
                state.ApplyDailyRollover(today);
                state.PruneHistory(today);
                return new StateLoadResult(state, true, migratedLegacy, JoinWarnings(warnings));
            }

            return new StateLoadResult(new AppState { LastOpenedDate = today }, false, false, null);
        }

        var preservedPath = TryPreserveCorruptFile();
        if (preservedPath is null)
        {
            WritesBlocked = true;
            warnings.Add("任务文件无法读取且无法创建保护副本，本次运行已停止写入。");
        }
        else
        {
            skipBackupOnce = true;
            warnings.Add($"检测到损坏的任务文件，原文件已保存在：{preservedPath}");
        }

        if (TryReadState(backupFile, today, out state, out migratedLegacy, out _))
        {
            warnings.Add(WritesBlocked ? "已从备份临时恢复任务，但当前无法保存。" : "任务已从备份恢复。");
            state.Normalize(today);
            state.ApplyDailyRollover(today);
            state.PruneHistory(today);
            return new StateLoadResult(state, !WritesBlocked, migratedLegacy, JoinWarnings(warnings));
        }

        return new StateLoadResult(
            new AppState { LastOpenedDate = today },
            false,
            false,
            JoinWarnings(warnings));
    }

    public bool Save(AppState state, out string? error)
    {
        error = null;
        if (WritesBlocked)
        {
            error = "任务数据当前处于只读保护状态，未覆盖原文件。";
            return false;
        }

        var tempFile = $"{dataFile}.tmp";
        try
        {
            Directory.CreateDirectory(dataDirectory);
            using (var stream = new FileStream(
                tempFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, state, JsonOptions);
                stream.Flush(true);
            }

            if (File.Exists(dataFile))
            {
                File.Replace(tempFile, dataFile, skipBackupOnce ? null : backupFile, true);
            }
            else
            {
                File.Move(tempFile, dataFile);
            }

            skipBackupOnce = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or JsonException)
        {
            TryDeleteFile(tempFile);
            error = $"任务暂时无法保存：{ex.Message}";
            return false;
        }
    }

    private static bool TryReadState(
        string path,
        DateOnly today,
        out AppState state,
        out bool migratedLegacy,
        out Exception? error)
    {
        state = new AppState { LastOpenedDate = today };
        migratedLegacy = false;
        error = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var legacyTasks = JsonSerializer.Deserialize<List<LegacyTask?>>(json, JsonOptions) ?? [];
                state = MigrateLegacyTasks(legacyTasks, today);
                migratedLegacy = true;
                return true;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("任务文件根节点不是对象或数组。");
            }

            state = JsonSerializer.Deserialize<AppState>(json, JsonOptions)
                ?? new AppState { LastOpenedDate = today };
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or JsonException or NotSupportedException)
        {
            error = ex;
            return false;
        }
    }

    private static AppState MigrateLegacyTasks(IEnumerable<LegacyTask?> legacyTasks, DateOnly today)
    {
        var state = new AppState { LastOpenedDate = today };
        var order = 0;
        foreach (var legacy in legacyTasks)
        {
            if (legacy is null || string.IsNullOrWhiteSpace(legacy.Text))
            {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(legacy.Id) ? TaskRecord.NewId() : legacy.Id;
            state.Tasks.Add(new TaskRecord
            {
                Id = id,
                Text = legacy.Text.Trim(),
                CreatedAt = DateTimeOffset.Now,
                Day = today,
                SortOrder = order,
                AccentIndex = TaskRecord.AccentForId(id),
                CompletedAt = legacy.Completed ? DateTimeOffset.Now : null
            });
            order += 1;
        }

        return state;
    }

    private string? TryPreserveLegacyFile()
    {
        try
        {
            Directory.CreateDirectory(dataDirectory);
            var path = Path.Combine(
                dataDirectory,
                $"tasks.v1-backup-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            File.Copy(dataFile, path, false);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    private string? TryPreserveCorruptFile()
    {
        try
        {
            Directory.CreateDirectory(dataDirectory);
            var path = Path.Combine(
                dataDirectory,
                $"tasks.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            File.Copy(dataFile, path, false);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
        }
    }

    private static string? JoinWarnings(IEnumerable<string> warnings)
    {
        var values = warnings.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return values.Length == 0 ? null : string.Join(Environment.NewLine, values);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812",
        Justification = "Instances are created by System.Text.Json during legacy migration.")]
    private sealed class LegacyTask
    {
        public string? Id { get; set; }
        public string? Text { get; set; }
        public bool Completed { get; set; }
    }
}
