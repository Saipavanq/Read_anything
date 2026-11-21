using System.Windows.Input;

namespace ReadAloud.Models;

public record HotkeyBinding(string Action, string Display, ModifierKeys Modifiers, Key Key);
