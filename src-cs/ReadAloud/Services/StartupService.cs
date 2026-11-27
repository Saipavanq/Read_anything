using System;

namespace ReadAloud.Services;

public sealed class StartupService
{
    private const string RunValueName = "Read Aloud";

    private readonly IRegistryAccessor _registry;

    public StartupService(IRegistryAccessor registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public bool IsStartWithWindowsEnabled
    {
        get
        {
            var value = _registry.GetValue(RunValueName);
            return string.Equals(value, RunCommand(false), StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, RunCommand(true), StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetStartWithWindows(bool enabled, bool runInBackground = false)
    {
        if (enabled)
        {
            _registry.SetValue(RunValueName, RunCommand(runInBackground));
        }
        else
        {
            _registry.DeleteValue(RunValueName);
        }
    }

    private static string RunCommand(bool runInBackground)
    {
        var executable = Environment.ProcessPath;
        var quoted = string.IsNullOrWhiteSpace(executable) ? "ReadAloud.exe" : $"\"{executable}\"";
        return runInBackground ? $"{quoted} --hidden" : quoted;
    }
}
