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
        => string.Equals(_registry.GetValue(RunValueName), RunCommand, StringComparison.OrdinalIgnoreCase);

    public void SetStartWithWindows(bool enabled)
    {
        if (enabled)
        {
            _registry.SetValue(RunValueName, RunCommand);
        }
        else
        {
            _registry.DeleteValue(RunValueName);
        }
    }

    private static string RunCommand
    {
        get
        {
            var executable = Environment.ProcessPath;
            return string.IsNullOrWhiteSpace(executable) ? "ReadAloud.exe" : $"\"{executable}\"";
        }
    }
}
