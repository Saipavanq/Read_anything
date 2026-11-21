using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReadAloud.Services;

public sealed class MouseUpAutoReadTrigger : IAutoReadTrigger
{
    private readonly Func<bool> _isForegroundOwnWindow;
    private readonly IMouseHook _hook;
    private readonly IClock _clock;
    private readonly TimeSpan _debounce;
    private volatile bool _started;
    private int _generation;
    private int _pending;

    public MouseUpAutoReadTrigger(
        Func<bool> isForegroundOwnWindow,
        IMouseHook? hook = null,
        IClock? clock = null,
        TimeSpan? debounce = null)
    {
        _isForegroundOwnWindow = isForegroundOwnWindow;
        _hook = hook ?? new Win32MouseHook();
        _clock = clock ?? new SystemClock();
        _debounce = debounce ?? TimeSpan.FromMilliseconds(120);
    }

    public event Action? SelectionCompleted;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _hook.MouseUp += OnMouseUp;
        _hook.Start();
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        Interlocked.Increment(ref _generation);
        Interlocked.Exchange(ref _pending, 0);
        _hook.MouseUp -= OnMouseUp;
        _hook.Stop();
        _started = false;
    }

    private void OnMouseUp()
    {
        if (_isForegroundOwnWindow())
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
        {
            return;
        }

        var generation = Interlocked.Increment(ref _generation);
        _ = DebounceAsync(generation);
    }

    private async Task DebounceAsync(int generation)
    {
        await _clock.Delay(_debounce);
        if (generation != Volatile.Read(ref _generation))
        {
            return;
        }

        Interlocked.Exchange(ref _pending, 0);
        if (_started)
        {
            SelectionCompleted?.Invoke();
        }
    }
}
