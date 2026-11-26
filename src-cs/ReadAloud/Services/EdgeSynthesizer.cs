using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ReadAloud.Services;

/// <summary>
/// Network boundary for Edge synthesis. Tests replace this seam so they never
/// open a WebSocket.
/// </summary>
public interface IEdgeSynthesizer
{
    Task<string> SynthesizeAsync(string text, string voice, string rate, CancellationToken token);
}

/// <summary>
/// Low-level Edge TTS file writer. Tests replace this seam so they never open
/// a WebSocket.
/// </summary>
public interface IEdgeTtsClient
{
    Task SaveAsync(string text, string voice, string rate, string target, CancellationToken token);
}

/// <summary>
/// Network boundary for Edge synthesis. Produces a complete MP3 or throws;
/// empty files are never handed to the audio player.
/// </summary>
public sealed class EdgeSynthesizer : IEdgeSynthesizer
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    private readonly IEdgeTtsClient _client;
    private readonly TimeSpan _timeout;
    private readonly string _directory;

    public EdgeSynthesizer()
        : this(new EdgeTtsNetClient(), DefaultTimeout, DefaultDirectory())
    {
    }

    /// <summary>
    /// Testable constructor. The client, timeout and output directory are
    /// injected so failures and hangs can be reproduced without the network.
    /// </summary>
    public EdgeSynthesizer(IEdgeTtsClient client, TimeSpan timeout, string? directory = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _timeout = timeout > TimeSpan.Zero ? timeout : DefaultTimeout;
        _directory = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory() : directory;
    }

    public async Task<string> SynthesizeAsync(string text, string voice, string rate, CancellationToken token)
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".mp3");

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
            linked.CancelAfter(_timeout);

            var save = _client.SaveAsync(text, voice, FormatRate(rate), target, linked.Token);
            var completed = await Task.WhenAny(save, Task.Delay(_timeout)).ConfigureAwait(false);
            if (completed != save)
            {
                linked.Cancel();
                try
                {
                    await save.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The timed-out attempt is superseded by the next voice.
                }

                throw new TimeoutException("Edge synthesis timed out.");
            }

            await save.ConfigureAwait(false);
            if (!File.Exists(target) || new FileInfo(target).Length == 0)
            {
                throw new InvalidDataException("Edge synthesis produced no audio.");
            }

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

    private static string FormatRate(string? rate)
    {
        var value = (rate ?? string.Empty).Trim().TrimEnd('%');
        var percent = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, -100, 100)
            : 0;
        return percent.ToString("+0;-0;+0", CultureInfo.InvariantCulture) + "%";
    }

    private static string DefaultDirectory()
        => Path.Combine(Path.GetTempPath(), "readaloud-edge");

    private sealed class EdgeTtsNetClient : IEdgeTtsClient
    {
        public async Task SaveAsync(
            string text,
            string voice,
            string rate,
            string target,
            CancellationToken token)
        {
            var option = new edge_tts_net.TTSOption(voice, "+0Hz", rate, "+0%");
            await new edge_tts_net.EdgeTTSNet()
                .Save(text, target, option, token)
                .ConfigureAwait(false);
        }
    }
}
