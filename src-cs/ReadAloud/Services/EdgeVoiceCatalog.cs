using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ReadAloud.Models;

namespace ReadAloud.Services;

/// <summary>
/// Cached Microsoft Edge voice catalog. Reads are synchronous and bounded by
/// local cache access; the HTTP refresh runs in the background so startup is
/// never blocked by the network.
/// </summary>
public sealed class EdgeVoiceCatalog
{
    private const string VoicesUrl =
        "https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/voices/list?trustedclienttoken=6A5AA1D4EAFF4E9FB37E23D68491D6F4";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly IReadOnlyList<VoiceInfo> FallbackVoices = new[]
    {
        new VoiceInfo("en-US-AriaNeural", "en-US-AriaNeural", "en-US", "Female", false),
        new VoiceInfo("en-US-JennyNeural", "en-US-JennyNeural", "en-US", "Female", false),
        new VoiceInfo("en-US-GuyNeural", "en-US-GuyNeural", "en-US", "Male", false)
    };

    private readonly string _cachePath;
    private readonly Func<CancellationToken, Task<string?>> _fetch;
    private IReadOnlyList<VoiceInfo> _cached = FallbackVoices;
    private int _refreshRunning;

    public EdgeVoiceCatalog()
        : this(DefaultCachePath())
    {
    }

    /// <summary>
    /// Creates a catalog with an explicit cache path and an optional fetcher.
    /// Tests inject a fetcher so no HTTP request is ever made.
    /// </summary>
    public EdgeVoiceCatalog(string cachePath, Func<CancellationToken, Task<string?>>? fetcher = null)
    {
        _cachePath = string.IsNullOrWhiteSpace(cachePath) ? DefaultCachePath() : cachePath;
        _fetch = fetcher ?? FetchFromNetworkAsync;
    }

    public static IReadOnlyList<VoiceInfo> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<VoiceInfo>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<VoiceInfo>();
            }

            var voices = new List<VoiceInfo>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("ShortName", out var shortName)
                    || shortName.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var name = shortName.GetString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                voices.Add(new VoiceInfo(
                    name,
                    name,
                    ReadString(item, "Locale"),
                    ReadString(item, "Gender"),
                    false));
            }

            return voices;
        }
        catch (Exception)
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public IReadOnlyList<VoiceInfo> GetVoices()
    {
        try
        {
            var cached = ReadCache();
            if (cached is not null)
            {
                _cached = cached;
            }

            if (Interlocked.Exchange(ref _refreshRunning, 1) == 0)
            {
                _ = Task.Run(RefreshAsync);
            }

            return cached ?? _cached;
        }
        catch (Exception)
        {
            return _cached.Count > 0 ? _cached : FallbackVoices;
        }
    }

    private IReadOnlyList<VoiceInfo>? ReadCache()
    {
        if (!File.Exists(_cachePath))
        {
            return null;
        }

        var info = new FileInfo(_cachePath);
        if (DateTime.UtcNow - info.LastWriteTimeUtc > CacheTtl)
        {
            return null;
        }

        var voices = Parse(File.ReadAllText(_cachePath));
        return voices.Count > 0 ? voices : null;
    }

    private async Task RefreshAsync()
    {
        try
        {
            var json = await _fetch(CancellationToken.None).ConfigureAwait(false);
            var voices = Parse(json ?? string.Empty);
            if (voices.Count == 0)
            {
                return;
            }

            var directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_cachePath, json, CancellationToken.None).ConfigureAwait(false);
            _cached = voices;
        }
        catch (Exception)
        {
            // Catalog refresh is best-effort; bundled voices remain available.
        }
        finally
        {
            Volatile.Write(ref _refreshRunning, 0);
        }
    }

    private static async Task<string?> FetchFromNetworkAsync(CancellationToken token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var response = await client.GetAsync(VoicesUrl, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
    }

    private static string ReadString(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string DefaultCachePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrEmpty(localAppData) ? Path.GetTempPath() : localAppData;
        return Path.Combine(root, "Read Aloud", "edge_voices.json");
    }
}
