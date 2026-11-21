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
    private readonly Action<string>? _diagnostic;
    private volatile bool _started;
    private int _generation;
    private int _pending;

    public MouseUpAutoReadTrigger(
        Func<bool> isForegroundOwnWindow,
        IMouseHook? hook = null,
        IClock? clock = null,
        TimeSpan? debounce = null,
        Action<string>? diagnostic = null)
    {
        _isForegroundOwnWindow = isForegroundOwnWindow;
        _hook = hook ?? new Win32MouseHook();
        _clock = clock ?? new SystemClock();
        _debounce = debounce ?? TimeSpan.FromMilliseconds(120);
        _diagnostic = diagnostic;
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
        _diagnostic?.Invoke("auto-read hook started");
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
            _diagnostic?.Invoke("auto-read ignored: own window foreground");
            return;
        }

        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
        {
            _diagnostic?.Invoke("auto-read ignored: debounce pending");
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
            _diagnostic?.Invoke("auto-read selection completed");
            SelectionCompleted?.Invoke();
        }
    }
}
