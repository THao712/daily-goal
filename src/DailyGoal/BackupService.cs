using System.Security;
using System.Text.Json;

namespace DailyGoal;

internal sealed class PortableBackup
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;
    public List<TaskRecord> Tasks { get; set; } = [];
    public List<TaskTemplate> Templates { get; set; } = [];
    public List<RecurringTask> RecurringTasks { get; set; } = [];
}

internal sealed record BackupImportResult(
    bool Success,
    List<TaskRecord> Tasks,
    List<TaskTemplate> Templates,
    List<RecurringTask> RecurringTasks,
    string? Error);

internal sealed class BackupService
{
    private const int SnapshotRetention = 30;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string backupsDirectory;

    public string BackupsDirectory => backupsDirectory;

    public BackupService(string dataDirectory)
    {
        backupsDirectory = Path.Combine(dataDirectory, "backups");
    }

    public bool EnsureDailySnapshot(AppState state, DateOnly today, out string? error)
    {
        error = null;
        var path = Path.Combine(backupsDirectory, $"tasks-{today:yyyyMMdd}.json");
        if (File.Exists(path))
        {
            return true;
        }

        if (!WritePortable(path, state, out error))
        {
            return false;
        }

        PruneSnapshots();
        return true;
    }

    public bool ExportPortable(AppState state, string path, out string? error)
    {
        return WritePortable(path, state, out error);
    }

    public bool CreatePreImportBackup(AppState state, out string? path, out string? error)
    {
        path = Path.Combine(
            backupsDirectory,
            $"pre-import-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        if (WritePortable(path, state, out error))
        {
            return true;
        }

        path = null;
        return false;
    }

    public BackupImportResult ReadPortable(string path, DateOnly today)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var backup = JsonSerializer.Deserialize<PortableBackup>(stream, JsonOptions);
            if (backup is null || backup.Version is < 1 or > PortableBackup.CurrentVersion || backup.Tasks is null)
            {
                return new BackupImportResult(false, [], [], [], "备份文件版本无效或内容不完整。");
            }

            var importedState = new AppState
            {
                LastOpenedDate = today,
                Tasks = backup.Tasks.Select(task => task.Clone()).ToList(),
                Templates = (backup.Templates ?? []).Select(template => template.Clone()).ToList(),
                RecurringTasks = (backup.RecurringTasks ?? []).Select(recurring => recurring.Clone()).ToList()
            };
            importedState.Normalize(today);
            importedState.ApplyDailyRollover(today);
            importedState.PruneHistory(today);
            return new BackupImportResult(
                true,
                importedState.Tasks.Select(task => task.Clone()).ToList(),
                importedState.Templates.Select(template => template.Clone()).ToList(),
                importedState.RecurringTasks.Select(recurring => recurring.Clone()).ToList(),
                null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or JsonException or NotSupportedException)
        {
            return new BackupImportResult(false, [], [], [], $"无法读取备份：{ex.Message}");
        }
    }

    private bool WritePortable(string path, AppState state, out string? error)
    {
        error = null;
        var tempPath = $"{path}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new IOException("备份路径无效。");
            }

            Directory.CreateDirectory(directory);
            var backup = new PortableBackup
            {
                ExportedAt = DateTimeOffset.Now,
                Tasks = state.Tasks.Select(task => task.Clone()).ToList(),
                Templates = state.Templates.Select(template => template.Clone()).ToList(),
                RecurringTasks = state.RecurringTasks.Select(recurring => recurring.Clone()).ToList()
            };
            using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, backup, JsonOptions);
                stream.Flush(true);
            }

            File.Move(tempPath, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or JsonException or NotSupportedException)
        {
            TryDelete(tempPath);
            error = $"无法写入备份：{ex.Message}";
            return false;
        }
    }

    private void PruneSnapshots()
    {
        try
        {
            var snapshots = Directory.GetFiles(backupsDirectory, "tasks-????????.json")
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Skip(SnapshotRetention)
                .ToArray();
            foreach (var snapshot in snapshots)
            {
                TryDelete(snapshot);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
        }
    }

    private static void TryDelete(string path)
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
}
