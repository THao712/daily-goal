using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DailyGoal;

/// <summary>Detects borderless and exclusive fullscreen windows without adding a worker thread.</summary>
internal static class FullscreenDetector
{
    private const int TolerancePixels = 4;
    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000L;

    public static bool IsForegroundFullscreen(IntPtr ownWindowHandle, Rectangle appBounds)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero
            || foreground == ownWindowHandle
            || foreground == GetShellWindow()
            || IsDesktopWindow(foreground))
        {
            return false;
        }

        if (GetWindowThreadProcessId(foreground, out var processId) != 0
            && processId == (uint)Environment.ProcessId)
        {
            return false;
        }

        if (IsIconic(foreground) || !GetWindowRect(foreground, out var nativeBounds))
        {
            return false;
        }

        // Captioned windows are regular maximized apps, even when an auto-hidden taskbar
        // makes their rectangle nearly identical to the monitor bounds.
        if ((GetWindowStyle(foreground) & WsCaption) != 0)
        {
            return false;
        }

        try
        {
            var foregroundScreen = Screen.FromHandle(foreground);
            var appScreen = Screen.FromRectangle(appBounds);
            if (!string.Equals(foregroundScreen.DeviceName, appScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var foregroundBounds = Rectangle.FromLTRB(
                nativeBounds.Left,
                nativeBounds.Top,
                nativeBounds.Right,
                nativeBounds.Bottom);
            return CoversMonitor(foregroundBounds, foregroundScreen.Bounds);
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static bool CoversMonitor(Rectangle windowBounds, Rectangle monitorBounds)
    {
        return windowBounds.Left <= monitorBounds.Left + TolerancePixels
            && windowBounds.Top <= monitorBounds.Top + TolerancePixels
            && windowBounds.Right >= monitorBounds.Right - TolerancePixels
            && windowBounds.Bottom >= monitorBounds.Bottom - TolerancePixels;
    }

    private static bool IsDesktopWindow(IntPtr windowHandle)
    {
        var className = new StringBuilder(64);
        _ = GetClassName(windowHandle, className, className.Capacity);
        return className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd";
    }

    private static long GetWindowStyle(IntPtr windowHandle)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, GwlStyle).ToInt64()
            : GetWindowLong32(windowHandle, GwlStyle);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr windowHandle, StringBuilder className, int maxCount);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect bounds);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr windowHandle, int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
