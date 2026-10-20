using Microsoft.Win32;
using System.Security;

namespace DailyGoal;

internal interface IStartupRegistration
{
    bool IsEnabled(string executablePath);
    bool SetEnabled(bool enabled, string executablePath, out string? error);
}

internal sealed class StartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DailyGoal";

    public bool IsEnabled(string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            var value = key?.GetValue(ValueName) as string;
            return string.Equals(NormalizeCommand(value), NormalizeCommand(BuildCommand(executablePath)), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public bool SetEnabled(bool enabled, string executablePath, out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
                ?? throw new UnauthorizedAccessException("无法打开 Windows 登录启动项。");
            if (enabled)
            {
                key.SetValue(ValueName, BuildCommand(executablePath), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            return true;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            error = $"无法更新开机启动：{ex.Message}";
            return false;
        }
    }

    private static string BuildCommand(string executablePath)
    {
        return $"\"{Path.GetFullPath(executablePath)}\"";
    }

    private static string NormalizeCommand(string? value)
    {
        return (value ?? "").Trim();
    }
}
