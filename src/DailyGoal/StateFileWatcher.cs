using System.Security;

namespace DailyGoal;

/// <summary>
/// 监听数据目录中 tasks.json 的外部变更（例如云盘多机同步），
/// 经过防抖并忽略本进程刚刚触发的写入窗口后，通知 UI 线程安全热重载。
/// </summary>
internal sealed class StateFileWatcher : IDisposable
{
    private readonly string dataFile;
    private readonly Func<bool> isOwnWriteRecent;
    private readonly Action onExternalChange;
    private readonly object gate = new();
    private FileSystemWatcher? watcher;
    private System.Threading.Timer? debounceTimer;
    private bool disposed;

    public StateFileWatcher(string dataDirectory, string dataFile, Func<bool> isOwnWriteRecent, Action onExternalChange)
    {
        this.dataFile = dataFile;
        this.isOwnWriteRecent = isOwnWriteRecent;
        this.onExternalChange = onExternalChange;
        TryStart(dataDirectory);
    }

    private void TryStart(string dataDirectory)
    {
        try
        {
            if (!Directory.Exists(dataDirectory))
            {
                return;
            }

            var fileName = Path.GetFileName(dataFile);
            watcher = new FileSystemWatcher(dataDirectory)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false
            };
            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Renamed += OnRenamedEvent;
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            watcher?.Dispose();
            watcher = null;
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        ScheduleCheck();
    }

    private void OnRenamedEvent(object sender, RenamedEventArgs e)
    {
        if (string.Equals(Path.GetFileName(e.FullPath), Path.GetFileName(dataFile), StringComparison.OrdinalIgnoreCase))
        {
            ScheduleCheck();
        }
    }

    private void ScheduleCheck()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            if (isOwnWriteRecent())
            {
                return;
            }

            debounceTimer ??= new System.Threading.Timer(_ => FireCallback(), null, Timeout.Infinite, Timeout.Infinite);
            debounceTimer.Change(480, Timeout.Infinite);
        }
    }

    private void FireCallback()
    {
        lock (gate)
        {
            if (disposed || isOwnWriteRecent())
            {
                return;
            }
        }

        if (File.Exists(dataFile))
        {
            onExternalChange();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            debounceTimer?.Dispose();
            debounceTimer = null;
            if (watcher is not null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnFileEvent;
                watcher.Created -= OnFileEvent;
                watcher.Renamed -= OnRenamedEvent;
                watcher.Dispose();
                watcher = null;
            }
        }
    }
}
