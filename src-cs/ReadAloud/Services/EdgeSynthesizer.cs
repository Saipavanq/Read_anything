using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EdgeTtsSharp;

namespace ReadAloud.Services;

/// <summary>
/// Network boundary for Edge synthesis. Tests replace this seam so they never
/// open a WebSocket.
/// </summary>
public interface IEdgeSynthesizer
{
    Task<string> SynthesizeAsync(string text, string voice, string rate, CancellationToken token);
}

public sealed class EdgeSynthesizer : IEdgeSynthesizer
{
    public async Task<string> SynthesizeAsync(string text, string voice, string rate, CancellationToken token)
    {
        var directory = Path.Combine(Path.GetTempPath(), "readaloud-edge");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".mp3");

        try
        {
            var edgeVoice = await EdgeTts.GetVoice(voice).ConfigureAwait(false);
            var settings = new EdgeTtsSharp.Structures.PlaybackSettings
            {
                Rate = ParseRate(rate),
                Volume = 100f
            };

            await EdgeTts.SaveAudioToFile(edgeVoice, text, target, settings, token).ConfigureAwait(false);
            return target;
        }
        catch (Exception)
        {
            DeleteTemp(target);
            throw;
        }
    }

    private static void DeleteTemp(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Temporary files are best-effort.
        }
    }

    private static int ParseRate(string? rate)
    {
        var value = (rate ?? string.Empty).Trim().TrimEnd('%');
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent)
            ? Math.Clamp(percent, -100, 100)
            : 0;
    }
}
