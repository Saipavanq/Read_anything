using System;

namespace ReadAloud.Services;

public interface IMouseHook
{
    event Action? MouseUp;

    void Start();

    void Stop();
}
