using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class SelectionService
{
    private const string Sentinel = "__readaloud_capture_sentinel__";
    private const int ClipboardPollAttempts = 12;

    private static readonly TimeSpan ModifierReleaseDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ClipboardPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan UiaTimeout = TimeSpan.FromMilliseconds(500);

    private readonly IClipboard _clipboard;
    private readonly IKeyboardSender _keyboard;
    private readonly IUiaSelectionReader _uia;
    private readonly IClock _clock;
    private readonly Func<bool> _restoreClipboard;

    public SelectionService(
        IClipboard clipboard,
        IKeyboardSender keyboard,
        IUiaSelectionReader uia,
        IClock clock,
        Func<bool>? restoreClipboard = null)
    {
        _clipboard = clipboard;
        _keyboard = keyboard;
        _uia = uia;
        _clock = clock;
        _restoreClipboard = restoreClipboard ?? (() => true);
    }

    public async Task<CaptureResult> CaptureAsync(CaptureMethod method)
    {
        return method switch
        {
            CaptureMethod.UiaOnly => await CaptureUiaOnlyAsync(method),
            CaptureMethod.UiaFirst => await CaptureUiaFirstAsync(method),
            CaptureMethod.ClipboardOnly => await CaptureClipboardAsync(method, allowUiaFallback: false),
            _ => await CaptureClipboardAsync(method, allowUiaFallback: true)
        };
    }

    private async Task<CaptureResult> CaptureUiaOnlyAsync(CaptureMethod method)
    {
        var notes = new List<string>();
        var text = await TryUiaAsync(notes);

        return string.IsNullOrWhiteSpace(text)
            ? new CaptureResult(string.Empty, method, Diagnostic("UIA no selection", notes))
            : new CaptureResult(text, method, Diagnostic("uia", notes));
    }

    private async Task<CaptureResult> CaptureUiaFirstAsync(CaptureMethod method)
    {
        var notes = new List<string>();
        var text = await TryUiaAsync(notes);

        if (!string.IsNullOrWhiteSpace(text))
        {
            return new CaptureResult(text, method, Diagnostic("uia", notes));
        }

        var clipboardResult = await CaptureClipboardAsync(method, allowUiaFallback: false);
        return clipboardResult with { Diagnostic = "UIA no selection; " + clipboardResult.Diagnostic };
    }

    private async Task<CaptureResult> CaptureClipboardAsync(CaptureMethod method, bool allowUiaFallback)
    {
        var notes = new List<string>();
        var snapshot = _clipboard.Snapshot();

        try
        {
            _clipboard.SetText(Sentinel);
        }
        catch (Exception ex)
        {
            notes.Add($"clipboard set failed: {ex.Message}");
        }

        await _clock.Delay(ModifierReleaseDelay);

        try
        {
            _keyboard.SendCtrlC();
        }
        catch (Exception ex)
        {
            notes.Add($"Ctrl+C failed: {ex.Message}");
        }

        string? text = null;
        for (var attempt = 0; attempt < ClipboardPollAttempts && text is null; attempt++)
        {
            await _clock.Delay(ClipboardPollInterval);

            string? candidate;
            try
            {
                candidate = _clipboard.GetText();
            }
            catch (Exception ex)
            {
                notes.Add($"clipboard read failed: {ex.Message}");
                break;
            }

            if (!string.IsNullOrWhiteSpace(candidate)
                && !string.Equals(candidate, Sentinel, StringComparison.Ordinal))
            {
                text = candidate;
            }
        }

        if (_restoreClipboard())
        {
            try
            {
                _clipboard.Restore(snapshot);
            }
            catch (Exception ex)
            {
                notes.Add($"restore failed: {ex.Message}");
            }
        }

        if (text is not null)
        {
            return new CaptureResult(text, method, Diagnostic("clipboard", notes));
        }

        if (!allowUiaFallback)
        {
            return new CaptureResult(string.Empty, method, Diagnostic("clipboard unchanged", notes));
        }

        var uiaText = await TryUiaAsync(notes);
        return string.IsNullOrWhiteSpace(uiaText)
            ? new CaptureResult(string.Empty, method, Diagnostic("clipboard unchanged; UIA no selection", notes))
            : new CaptureResult(uiaText, method, Diagnostic("uia", notes));
    }

    private async Task<string?> TryUiaAsync(ICollection<string> notes)
    {
        try
        {
            return await _uia.TryReadSelectionAsync(UiaTimeout);
        }
        catch (Exception ex)
        {
            notes.Add($"UIA error: {ex.Message}");
            return null;
        }
    }

    private static string Diagnostic(string source, IEnumerable<string> notes)
    {
        var parts = new List<string> { source };
        parts.AddRange(notes);
        return string.Join("; ", parts);
    }
}
