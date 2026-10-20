using System.Reflection;
using System.Text.Json;

namespace DailyGoal;

internal static class ReliabilitySelfTests
{
    public static string Run(string rootDirectory, DateOnly today)
    {
        var v3Migration = VerifyV3Migration(rootDirectory, today);
        var snapshots = VerifySnapshots(rootDirectory, today);
        var portable = VerifyPortableBackups(rootDirectory, today);
        var sliderCommit = VerifyDelaySliderCommit();
        var storagePointer = VerifyStoragePointer(rootDirectory);

        if (!v3Migration || !snapshots || !portable || !sliderCommit || !storagePointer)
        {
            throw new InvalidOperationException(
                $"Reliability self-test failed: migration={v3Migration}, snapshots={snapshots}, "
                + $"portable={portable}, sliderCommit={sliderCommit}, storagePointer={storagePointer}.");
        }

        return $"migrationV3={v3Migration}; snapshots={snapshots}; portable={portable}; sliderCommit={sliderCommit}; storagePointer={storagePointer}";
    }

    /// <summary>数据目录指针文件：默认解析、指向自定义目录、目录失效回退、清除恢复默认。</summary>
    private static bool VerifyStoragePointer(string rootDirectory)
    {
        var pointerRoot = Path.Combine(rootDirectory, "pointer-root");
        var custom = Path.Combine(rootDirectory, "pointer-custom");
        Directory.CreateDirectory(pointerRoot);
        Directory.CreateDirectory(custom);

        var defaultResolved = StateStore.ResolveDataDirectory(pointerRoot) == pointerRoot;
        var set = StateStore.TrySetCustomDataDirectory(custom, out _, pointerRoot);
        var customResolved = string.Equals(
            StateStore.ResolveDataDirectory(pointerRoot),
            Path.GetFullPath(custom),
            StringComparison.OrdinalIgnoreCase);

        var missing = Path.Combine(rootDirectory, "pointer-missing");
        var setMissing = StateStore.TrySetCustomDataDirectory(missing, out _, pointerRoot);
        var missingFallsBack = StateStore.ResolveDataDirectory(pointerRoot) == pointerRoot;

        var reset = StateStore.TrySetCustomDataDirectory(null, out _, pointerRoot);
        var backToDefault = StateStore.ResolveDataDirectory(pointerRoot) == pointerRoot
            && !File.Exists(Path.Combine(pointerRoot, "storage-location.txt"));

        return defaultResolved && set && customResolved && setMissing && missingFallsBack && reset && backToDefault;
    }

    private static bool VerifyV3Migration(string rootDirectory, DateOnly today)
    {
        var directory = Path.Combine(rootDirectory, "v3-migration");
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(new
        {
            Version = 3,
            LastOpenedDate = today,
            Tasks = new[]
            {
                new
                {
                    Id = "v2-task",
                    Text = "v2 task",
                    CreatedAt = DateTimeOffset.Now,
                    Day = today.AddDays(-1),
                    SortOrder = 0,
                    AccentIndex = 2,
                    CarryCount = 0,
                    CompletedAt = (DateTimeOffset?)null
                }
            },
            Settings = new
            {
                AutoHideEnabled = false,
                AutoHideDelayMs = 1200,
                ParticleIntensity = ParticleIntensity.Standard,
                ReducedMotion = true
            }
        });
        File.WriteAllText(Path.Combine(directory, "tasks.json"), json);

        var loaded = new StateStore(directory).Load(today);
        return loaded.NeedsSave
            && loaded.State.Version == AppState.CurrentVersion
            && loaded.State.ActiveTasks.Single().Id == "v2-task"
            && loaded.State.ActiveTasks.Single().Day == today
            && loaded.State.Settings.AutoHideDelayMs == 1200
            && loaded.State.Settings.ReducedMotion
            && !loaded.State.Settings.StartWithWindows;
    }

    private static bool VerifySnapshots(string rootDirectory, DateOnly today)
    {
        var directory = Path.Combine(rootDirectory, "snapshots");
        var service = new BackupService(directory);
        var state = CreateState(today, "snapshot-original");

        for (var offset = 31; offset >= 0; offset -= 1)
        {
            if (!service.EnsureDailySnapshot(state, today.AddDays(-offset), out _))
            {
                return false;
            }
        }

        var todayPath = Path.Combine(service.BackupsDirectory, $"tasks-{today:yyyyMMdd}.json");
        var originalTodaySnapshot = File.ReadAllText(todayPath);
        state.Tasks[0].Text = "snapshot-mutated";
        if (!service.EnsureDailySnapshot(state, today, out _))
        {
            return false;
        }

        var snapshots = Directory.GetFiles(service.BackupsDirectory, "tasks-????????.json")
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
        return snapshots.Length == 30
            && Path.GetFileName(snapshots[0]) == $"tasks-{today.AddDays(-29):yyyyMMdd}.json"
            && File.ReadAllText(todayPath) == originalTodaySnapshot
            && originalTodaySnapshot.Contains("snapshot-original", StringComparison.Ordinal);
    }

    private static bool VerifyPortableBackups(string rootDirectory, DateOnly today)
    {
        var directory = Path.Combine(rootDirectory, "portable");
        var service = new BackupService(directory);
        var state = CreateState(today, "portable-active");
        state.Settings.AutoHideEnabled = false;
        state.Settings.StartWithWindows = true;
        state.Templates.Add(new TaskTemplate
        {
            Id = "portable-template",
            Text = "portable template",
            AccentIndex = 4,
            SortOrder = 0
        });
        state.RecurringTasks.Add(new RecurringTask
        {
            Id = "portable-series",
            Text = "portable series",
            AccentIndex = 2,
            Rule = RecurrenceRule.Weekdays,
            WeeklyDay = DayOfWeek.Monday,
            CreatedAt = DateTimeOffset.Now,
            Enabled = true
        });
        state.Tasks.Add(new TaskRecord
        {
            Id = "portable-history",
            Text = "portable history",
            CreatedAt = DateTimeOffset.Now.AddDays(-1),
            Day = today.AddDays(-1),
            SortOrder = 1,
            AccentIndex = 3,
            CompletedAt = DateTimeOffset.Now.AddHours(-1)
        });

        var exportPath = Path.Combine(directory, "export.dailygoal.json");
        if (!service.ExportPortable(state, exportPath, out _))
        {
            return false;
        }

        var imported = service.ReadPortable(exportPath, today);
        var settingsRemainDeviceLocal = !File.ReadAllText(exportPath).Contains("StartWithWindows", StringComparison.Ordinal);
        var invalidPath = Path.Combine(directory, "invalid.json");
        File.WriteAllText(invalidPath, "{ invalid json");
        var invalid = service.ReadPortable(invalidPath, today);
        var protectedCopyCreated = service.CreatePreImportBackup(state, out var protectedPath, out _)
            && protectedPath is not null
            && File.Exists(protectedPath);
        var legacyPortablePath = Path.Combine(directory, "legacy-v1.json");
        File.WriteAllText(
            legacyPortablePath,
            JsonSerializer.Serialize(new
            {
                Version = 1,
                ExportedAt = DateTimeOffset.Now,
                Tasks = new[] { state.Tasks[0] }
            }));
        var legacyPortable = service.ReadPortable(legacyPortablePath, today);

        return imported.Success
            && imported.Tasks.Count == 2
            && imported.Tasks.Any(task => task.Id == "portable-active" && task.IsActive)
            && imported.Tasks.Any(task => task.Id == "portable-history" && !task.IsActive)
            && imported.Templates.Single().Id == "portable-template"
            && imported.RecurringTasks.Single().Id == "portable-series"
            && !invalid.Success
            && settingsRemainDeviceLocal
            && protectedCopyCreated
            && legacyPortable.Success
            && legacyPortable.Tasks.Count == 1
            && legacyPortable.Templates.Count == 0
            && legacyPortable.RecurringTasks.Count == 0;
    }

    private static bool VerifyDelaySliderCommit()
    {
        using var slider = new DelaySlider { Size = new Size(216, 36) };
        var changes = 0;
        var commits = 0;
        slider.ValueChanged += (_, _) => changes += 1;
        slider.ValueCommitted += (_, _) => commits += 1;

        InvokeMouse(slider, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 20, 18, 0));
        InvokeMouse(slider, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 90, 18, 0));
        InvokeMouse(slider, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 170, 18, 0));
        InvokeMouse(slider, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 195, 18, 0));
        return changes >= 2 && commits == 1;
    }

    private static void InvokeMouse(DelaySlider slider, string methodName, MouseEventArgs args)
    {
        var method = typeof(DelaySlider).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(DelaySlider).FullName, methodName);
        method.Invoke(slider, [args]);
    }

    private static AppState CreateState(DateOnly today, string text)
    {
        return new AppState
        {
            LastOpenedDate = today,
            Tasks =
            [
                new TaskRecord
                {
                    Id = "portable-active",
                    Text = text,
                    CreatedAt = DateTimeOffset.Now,
                    Day = today,
                    SortOrder = 0,
                    AccentIndex = 1
                }
            ]
        };
    }
}
