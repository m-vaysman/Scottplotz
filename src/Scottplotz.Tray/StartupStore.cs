using Microsoft.Win32;
using Scottplotz.Hosting;

namespace Scottplotz.Tray;

public static class StartupStore
{
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistration.KeyPath, writable: false);
        return key?.GetValue(StartupRegistration.ValueName) is string;
    }

    public static void SetEnabled(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupRegistration.KeyPath);
        if (enabled)
            key.SetValue(StartupRegistration.ValueName, StartupRegistration.QuotedCommand(exePath));
        else
            key.DeleteValue(StartupRegistration.ValueName, throwOnMissingValue: false);
    }
}
