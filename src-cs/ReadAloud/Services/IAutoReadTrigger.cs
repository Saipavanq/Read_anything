using System;

namespace ReadAloud.Services;

public interface IAutoReadTrigger
{
    event Action? SelectionCompleted;

    void Start();

    void Stop();
}
