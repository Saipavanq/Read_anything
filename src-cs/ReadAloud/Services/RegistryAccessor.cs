using Microsoft.Win32;

namespace ReadAloud.Services;

public sealed class RegistryAccessor : IRegistryAccessor
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? GetValue(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(name) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void SetValue(string name, string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key?.SetValue(name, value);
        }
        catch (Exception)
        {
            // Start-with-Windows is optional; registry denial must not break the app.
        }
    }

    public void DeleteValue(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
        catch (Exception)
        {
            // Start-with-Windows is optional; registry denial must not break the app.
        }
    }
}
