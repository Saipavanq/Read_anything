using System;
using System.Threading.Tasks;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class AutoReadService
{
    private readonly IAutoReadTrigger _trigger;
    private readonly ReadCoordinator _coordinator;
    private readonly Func<AppConfig> _configProvider;
    private readonly Action<Action> _dispatcher;

    public AutoReadService(
        IAutoReadTrigger trigger,
        ReadCoordinator coordinator,
        Func<AppConfig> configProvider,
        Action<Action>? dispatcher = null)
    {
        _trigger = trigger;
        _coordinator = coordinator;
        _configProvider = configProvider;
        _dispatcher = dispatcher ?? (action => action());
    }

    public bool IsRunning { get; private set; }

    public Task? LastReadTask { get; private set; }

    public void ApplyConfig()
    {
        var enabled = _configProvider().AutoReadSelection;
        if (enabled && !IsRunning)
        {
            _trigger.SelectionCompleted += OnSelectionCompleted;
            _trigger.Start();
            IsRunning = true;
            return;
        }

        if (!enabled && IsRunning)
        {
            Stop();
        }
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        _trigger.SelectionCompleted -= OnSelectionCompleted;
        _trigger.Stop();
        IsRunning = false;
        LastReadTask = null;
    }

    private void OnSelectionCompleted() => _dispatcher(StartRead);

    private void StartRead() => LastReadTask = ReadAsync();

    private async Task ReadAsync()
    {
        try
        {
            await _coordinator.RequestReadAsync();
        }
        catch (Exception)
        {
            // Auto-read is best-effort; failures surface in the activity log through the coordinator.
        }
    }
}
