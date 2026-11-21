using System;
using ReadAloud.Models;

namespace ReadAloud.Services;

public interface IHotkeyRegistrar
{
    event Action<string>? HotkeyPressed;

    bool TryRegister(HotkeyBinding binding);

    void UnregisterAll();
}
