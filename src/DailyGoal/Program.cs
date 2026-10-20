namespace DailyGoal;

using System.Diagnostics;
using System.Globalization;

internal static class Program
{
    private const string InstanceMutexName = @"Local\DailyGoal-5A1E6DA9-70D3-49E7-9D78-3CBB9F35C2C1";
    private const string InstanceActivationEventName = @"Local\DailyGoal-Activate-5A1E6DA9-70D3-49E7-9D78-3CBB9F35C2C1";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        ToolStripManager.Renderer = new DarkMenuRenderer();

        var selfTest = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);
        var particleSelfTest = args.Contains("--particle-self-test", StringComparer.OrdinalIgnoreCase);
        var uiSelfTest = args.Contains("--ui-self-test", StringComparer.OrdinalIgnoreCase);
        var testMode = selfTest || particleSelfTest || uiSelfTest;

        ApplyUiScale(testMode);

        if (!testMode)
        {
            bool restartRequested;
            using (var instanceCoordinator = SingleInstanceCoordinator.Acquire(InstanceMutexName, InstanceActivationEventName))
            {
                if (!instanceCoordinator.IsOwner)
                {
                    instanceCoordinator.SignalExisting();
                    return;
                }

                using var form = new DailyGoalForm();
                form.Shown += (_, _) => instanceCoordinator.RegisterActivation(form.HandleExternalActivation);
                Application.Run(form);
                restartRequested = form.RestartRequested;
            }

            if (restartRequested && Environment.ProcessPath is { } executable)
            {
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = true
                });
            }

            return;
        }

        try
        {
            RunTestMode(selfTest, particleSelfTest, uiSelfTest);
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selftest-failure.txt"), exception.ToString());
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// 界面缩放必须在创建任何控件之前敲定：优先环境变量（自检验证用），
    /// 其次按用户偏好与目标显示器工作区共同计算，确保低分辨率下不会超出屏幕。
    /// </summary>
    private static void ApplyUiScale(bool testMode)
    {
        var env = Environment.GetEnvironmentVariable("DAILYGOAL_UI_SCALE");
        if (float.TryParse(env, NumberStyles.Float, CultureInfo.InvariantCulture, out var envScale) && envScale > 0)
        {
            UiTheme.ApplyScale(envScale);
            return;
        }

        if (testMode)
        {
            return;
        }

        var display = StateStore.PeekStartupDisplaySettings();
        var screens = Screen.AllScreens;
        var screen = string.IsNullOrWhiteSpace(display.PreferredScreenDevice)
            ? Screen.PrimaryScreen
            : screens.FirstOrDefault(candidate =>
                string.Equals(candidate.DeviceName, display.PreferredScreenDevice, StringComparison.OrdinalIgnoreCase))
                ?? Screen.PrimaryScreen;
        var workArea = screen?.WorkingArea ?? Screen.GetWorkingArea(Point.Empty);
        UiTheme.ApplyScale(UiScaleResolver.Resolve(display.UiScalePercent, workArea));
    }

    private static void RunTestMode(bool selfTest, bool particleSelfTest, bool uiSelfTest)
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), $"DailyGoalSelfTest-{Guid.NewGuid():N}");
        string reportName;
        string details;

        using (var form = new DailyGoalForm(dataDirectory, enableTrayIcon: false))
        {
            if (selfTest)
            {
                reportName = "selftest.txt";
                details = $"{form.RunSelfTest()}; singleInstance={SingleInstanceCoordinator.RunProbe()}";
            }
            else if (particleSelfTest)
            {
                reportName = "particle-selftest.txt";
                details = form.RunParticleSelfTest();
            }
            else
            {
                reportName = "ui-selftest.txt";
                details = form.RunUiSelfTest(Path.Combine(AppContext.BaseDirectory, "ui-selftest"));
            }
        }

        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, true);
        }

        File.WriteAllText(
            Path.Combine(AppContext.BaseDirectory, reportName),
            $"DailyGoal {reportName.Replace(".txt", "", StringComparison.Ordinal)} OK; {details}");
    }
}

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activationEvent;
    private RegisteredWaitHandle? activationWait;
    private bool ownsMutex;

    public bool IsOwner => ownsMutex;

    private SingleInstanceCoordinator(Mutex mutex, EventWaitHandle activationEvent, bool ownsMutex)
    {
        this.mutex = mutex;
        this.activationEvent = activationEvent;
        this.ownsMutex = ownsMutex;
    }

    public static SingleInstanceCoordinator Acquire(string mutexName, string activationEventName)
    {
        var mutex = new Mutex(true, mutexName, out var createdNew);
        var activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, activationEventName);
        return new SingleInstanceCoordinator(mutex, activationEvent, createdNew);
    }

    public void RegisterActivation(Action callback)
    {
        if (!IsOwner || activationWait is not null)
        {
            return;
        }

        activationWait = ThreadPool.RegisterWaitForSingleObject(
            activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut)
                {
                    callback();
                }
            },
            null,
            Timeout.Infinite,
            false);
    }

    public void SignalExisting()
    {
        activationEvent.Set();
    }

    public static bool RunProbe()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var first = Acquire($@"Local\DailyGoal-SelfTest-{suffix}", $@"Local\DailyGoal-Activate-SelfTest-{suffix}");
        using var second = Acquire($@"Local\DailyGoal-SelfTest-{suffix}", $@"Local\DailyGoal-Activate-SelfTest-{suffix}");
        using var activated = new ManualResetEventSlim(false);
        first.RegisterActivation(() => activated.Set());
        second.SignalExisting();
        return first.IsOwner && !second.IsOwner && activated.Wait(1000);
    }

    public void Dispose()
    {
        activationWait?.Unregister(null);
        activationWait = null;
        activationEvent.Dispose();
        if (ownsMutex)
        {
            mutex.ReleaseMutex();
            ownsMutex = false;
        }

        mutex.Dispose();
    }
}
