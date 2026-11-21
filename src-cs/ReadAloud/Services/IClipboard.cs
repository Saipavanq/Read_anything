using System.Collections.Generic;

namespace ReadAloud.Services;

public interface IClipboard
{
    Dictionary<int, object?> Snapshot();

    void Restore(Dictionary<int, object?> snapshot);

    string? GetText();

    void SetText(string text);
}
