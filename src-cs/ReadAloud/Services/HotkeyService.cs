using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class HotkeyService
{
    private readonly IHotkeyRegistrar _registrar;
    private readonly Dictionary<string, HotkeyBinding> _recorded = new(StringComparer.OrdinalIgnoreCase);

    public HotkeyService(IHotkeyRegistrar registrar)
    {
        _registrar = registrar;
    }

    public string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(key));
        return string.Join("+", parts);
    }

    public bool TryRecord(
        string action,
        ModifierKeys modifiers,
        Key key,
        out HotkeyBinding binding,
        out string error)
    {
        binding = new HotkeyBinding(action, string.Empty, modifiers, key);
        error = string.Empty;

        if (key == Key.None || IsModifierKey(key))
        {
            error = "Press one non-modifier key after the modifiers.";
            return false;
        }

        var display = Format(modifiers, key);

        var duplicate = _recorded.FirstOrDefault(pair =>
            !string.Equals(pair.Key, action, StringComparison.OrdinalIgnoreCase)
            && string.Equals(pair.Value.Display, display, StringComparison.OrdinalIgnoreCase));

        if (!duplicate.Equals(default(KeyValuePair<string, HotkeyBinding>)))
        {
            error = $"{display} is already used by {duplicate.Key}.";
            return false;
        }

        binding = new HotkeyBinding(action, display, modifiers, key);
        _recorded[action] = binding;
        return true;
    }

    public bool IsValid(string display) => TryParse(display, out _, out _);

    public void BeginRecording()
    {
        _registrar.UnregisterAll();
    }

    public void EndRecording(AppConfig config)
    {
        RegisterAll(config, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> RegisterAll(AppConfig config, ISet<string> ownedByOthers)
    {
        _registrar.UnregisterAll();
        _recorded.Clear();

        var conflicts = new List<string>();
        Register("read", config.HotkeyRead, ownedByOthers, conflicts);
        Register("voice", config.HotkeyVoice, ownedByOthers, conflicts);
        Register("stop", config.HotkeyStop, ownedByOthers, conflicts);
        return conflicts;
    }

    private void Register(
        string action,
        string display,
        ISet<string> ownedByOthers,
        ICollection<string> conflicts)
    {
        if (!TryParse(display, out var modifiers, out var key))
        {
            conflicts.Add($"{action} hotkey '{display}' is not valid.");
            return;
        }

        var binding = new HotkeyBinding(action, Format(modifiers, key), modifiers, key);
        _recorded[action] = binding;

        if (IsOwnedByOther(ownedByOthers, binding.Display))
        {
            conflicts.Add($"{binding.Display} for {action} is already used by another app.");
            return;
        }

        if (!_registrar.TryRegister(binding))
        {
            conflicts.Add($"{binding.Display} for {action} could not be registered.");
        }
    }

    private static bool IsOwnedByOther(ISet<string> ownedByOthers, string display)
        => ownedByOthers.Any(value => string.Equals(value, display, StringComparison.OrdinalIgnoreCase));

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin;

    private static bool TryParse(string display, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(display))
        {
            return false;
        }

        var tokens = display.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        for (var i = 0; i < tokens.Length - 1; i++)
        {
            if (!TryParseModifier(tokens[i], out var modifier))
            {
                return false;
            }

            modifiers |= modifier;
        }

        return TryParseKey(tokens[^1], out key) && key != Key.None && !IsModifierKey(key);
    }

    private static bool TryParseModifier(string token, out ModifierKeys modifier)
    {
        modifier = token.ToLowerInvariant() switch
        {
            "ctrl" or "control" => ModifierKeys.Control,
            "shift" => ModifierKeys.Shift,
            "alt" => ModifierKeys.Alt,
            "win" or "windows" or "meta" => ModifierKeys.Windows,
            _ => ModifierKeys.None
        };

        return modifier != ModifierKeys.None;
    }

    private static bool TryParseKey(string token, out Key key)
    {
        if (Enum.TryParse(token, ignoreCase: true, out key))
        {
            return true;
        }

        switch (token)
        {
            case "0": key = Key.D0; return true;
            case "1": key = Key.D1; return true;
            case "2": key = Key.D2; return true;
            case "3": key = Key.D3; return true;
            case "4": key = Key.D4; return true;
            case "5": key = Key.D5; return true;
            case "6": key = Key.D6; return true;
            case "7": key = Key.D7; return true;
            case "8": key = Key.D8; return true;
            case "9": key = Key.D9; return true;
            case ";": key = Key.OemSemicolon; return true;
            case "'": key = Key.OemQuotes; return true;
            case ",": key = Key.OemComma; return true;
            case ".": key = Key.OemPeriod; return true;
            case "/": key = Key.OemQuestion; return true;
            case "\\": key = Key.OemBackslash; return true;
            case "[": key = Key.OemOpenBrackets; return true;
            case "]": key = Key.OemCloseBrackets; return true;
            case "-": key = Key.OemMinus; return true;
            case "=": key = Key.OemPlus; return true;
            case "Esc": key = Key.Escape; return true;
            case "Backspace": key = Key.Back; return true;
            default: key = Key.None; return false;
        }
    }

    private static string KeyName(Key key) => key switch
    {
        Key.D0 => "0",
        Key.D1 => "1",
        Key.D2 => "2",
        Key.D3 => "3",
        Key.D4 => "4",
        Key.D5 => "5",
        Key.D6 => "6",
        Key.D7 => "7",
        Key.D8 => "8",
        Key.D9 => "9",
        Key.OemSemicolon => ";",
        Key.OemQuotes => "'",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.OemBackslash => "\\",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.Space => "Space",
        Key.Enter => "Enter",
        Key.Tab => "Tab",
        Key.Escape => "Esc",
        Key.Back => "Backspace",
        _ => key.ToString()
    };
}
