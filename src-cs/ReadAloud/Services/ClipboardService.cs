using System;
using System.Collections.Generic;
using System.Threading;
using WinFormsClipboard = System.Windows.Forms.Clipboard;
using WinFormsDataObject = System.Windows.Forms.DataObject;

namespace ReadAloud.Services;

internal sealed class ClipboardService : IClipboard
{
    private const int Attempts = 4;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(40);

    public Dictionary<int, object?> Snapshot()
    {
        var snapshot = new Dictionary<int, object?>();

        WithRetry(() =>
        {
            var data = WinFormsClipboard.GetDataObject();
            if (data is null)
            {
                return;
            }

            foreach (var format in data.GetFormats())
            {
                if (string.IsNullOrWhiteSpace(format))
                {
                    continue;
                }

                try
                {
                    var id = System.Windows.Forms.DataFormats.GetFormat(format).Id;
                    snapshot[id] = data.GetData(format);
                }
                catch (Exception)
                {
                    // A format that cannot be read cannot be restored either.
                }
            }
        });

        return snapshot;
    }

    public void Restore(Dictionary<int, object?> snapshot)
    {
        WithRetry(() =>
        {
            var data = new WinFormsDataObject();
            foreach (var pair in snapshot)
            {
                try
                {
                    var format = System.Windows.Forms.DataFormats.GetFormat(pair.Key).Name;
                    if (pair.Value is not null)
                    {
                        data.SetData(format, pair.Value);
                    }
                }
                catch (Exception)
                {
                    // Restore as many clipboard formats as the OS permits.
                }
            }

            WinFormsClipboard.SetDataObject(data, true);
        });
    }

    public string? GetText()
    {
        string? text = null;
        WithRetry(() => text = WinFormsClipboard.GetText());
        return text;
    }

    public void SetText(string text)
        => WithRetry(() => WinFormsClipboard.SetText(text));

    private static void WithRetry(Action action)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception) when (attempt < Attempts - 1)
            {
                Thread.Sleep(RetryDelay);
            }
        }
    }
}
