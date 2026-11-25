using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class PiperSpeechEngine : ISpeechEngine
{
    private readonly Func<AppConfig> _configProvider;
    private readonly object _gate = new();
    private Process? _helperProcess;
    private SoundPlayer? _player;
    private CancellationTokenSource? _cancellation;
    private int _generation;
    private volatile bool _isSpeaking;

    public PiperSpeechEngine(Func<AppConfig> configProvider)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    public event Action? SpeakCompleted;

    public event Action<int, int>? SpeakProgress;

    public bool IsSpeaking => _isSpeaking;

    public static IReadOnlyList<VoiceInfo> ParseVoiceList(string json)
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
                    || !item.TryGetProperty("name", out var nameNode)
                    || !item.TryGetProperty("path", out var pathNode)
                    || nameNode.ValueKind != JsonValueKind.String
                    || pathNode.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var name = nameNode.GetString();
                var path = pathNode.GetString();
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path))
                {
                    voices.Add(new VoiceInfo(name, path, "Not specified", "Not specified", false));
                }
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
            var config = _configProvider();
            var modelDir = config.PiperModelDir?.Trim() ?? string.Empty;
            if (modelDir.Length == 0)
            {
                return Array.Empty<VoiceInfo>();
            }

            var python = ResolvePython(config);
            var helper = ResolveHelper();
            if (python.Length == 0 || !File.Exists(helper))
            {
                return Array.Empty<VoiceInfo>();
            }

            var output = RunHelperAsync(
                python,
                helper,
                new[] { "--list", "--model-dir", modelDir },
                CancellationToken.None,
                generation: -1).GetAwaiter().GetResult();
            return ParseVoiceList(output);
        }
        catch (Exception)
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public void Speak(string text, VoiceOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            // Invalidate the previous read before cancelling it so its worker
            // cannot raise a completion event for the new one.
            Interlocked.Increment(ref _generation);
            Stop();
            var generation = Interlocked.Increment(ref _generation);
            var cancellation = new CancellationTokenSource();
            lock (_gate)
            {
                _cancellation = cancellation;
            }

            _isSpeaking = true;
            _ = Task.Run(() => SpeakAsync(text, generation, cancellation));
        }
        catch (Exception)
        {
            _isSpeaking = false;
        }
    }

    public void Stop()
    {
        try
        {
            Interlocked.Increment(ref _generation);
            Process? process;
            SoundPlayer? player;
            CancellationTokenSource? cancellation;
            lock (_gate)
            {
                process = _helperProcess;
                _helperProcess = null;
                player = _player;
                _player = null;
                cancellation = _cancellation;
                _cancellation = null;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (Exception)
            {
                // The in-flight read may already have released its token source.
            }

            if (process is not null)
            {
                KillProcess(process);
            }

            try
            {
                player?.Stop();
            }
            catch (Exception)
            {
                // Speech failures must never reach the UI layer.
            }
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
        finally
        {
            _isSpeaking = false;
        }
    }

    public void Pause()
    {
        // Piper writes a complete WAV before playing it; pausing mid-file is a no-op.
    }

    public void Resume()
    {
        // Piper writes a complete WAV before playing it; resuming mid-file is a no-op.
    }

    private async Task SpeakAsync(string text, int generation, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        string? textFile = null;
        string? wavFile = null;
        SoundPlayer? player = null;

        try
        {
            var config = _configProvider();
            var python = ResolvePython(config);
            var helper = ResolveHelper();
            var model = ResolveModel(config);
            if (python.Length == 0 || !File.Exists(helper) || model.Length == 0)
            {
                throw new InvalidOperationException("Piper is not configured on this PC.");
            }

            IsCurrent(generation, token);
            var directory = Path.Combine(Path.GetTempPath(), "readaloud-piper");
            Directory.CreateDirectory(directory);
            var id = Guid.NewGuid().ToString("N");
            textFile = Path.Combine(directory, id + ".txt");
            wavFile = Path.Combine(directory, id + ".wav");
            await File.WriteAllTextAsync(textFile, text, new UTF8Encoding(false), token).ConfigureAwait(false);

            var scale = Math.Clamp(config.PiperLengthScale, 0.1, 10)
                .ToString("0.###", CultureInfo.InvariantCulture);
            var arguments = new[]
            {
                "--synthesize",
                "--model", model,
                "--config", ResolveConfig(model),
                "--text-file", textFile,
                "--wav", wavFile,
                "--speaker", config.PiperSpeaker.ToString(CultureInfo.InvariantCulture),
                "--length-scale", scale
            };

            var output = await RunHelperAsync(python, helper, arguments, token, generation).ConfigureAwait(false);
            IsCurrent(generation, token);

            var duration = ParseDuration(output);
            SpeakProgress?.Invoke(0, 1);
            var started = Stopwatch.StartNew();
            player = PlayWav(wavFile, generation);
            var remaining = duration - started.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(remaining, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            IsCurrent(generation, token);
            SpeakProgress?.Invoke(text.Length, 0);
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
        finally
        {
            // The player stays live until the utterance ends (or Stop cancels it).
            ReleasePlayer(player);
            if (generation == Volatile.Read(ref _generation))
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_cancellation, cancellation))
                    {
                        _cancellation = null;
                    }
                }

                _isSpeaking = false;
                SpeakCompleted?.Invoke();
            }

            cancellation.Dispose();
            DeleteTemp(textFile);
            DeleteTemp(wavFile);
        }
    }

    private async Task<string> RunHelperAsync(
        string python,
        string helper,
        IEnumerable<string> arguments,
        CancellationToken token,
        int generation)
    {
        var start = new ProcessStartInfo
        {
            FileName = python,
            WorkingDirectory = FindRepoRoot() ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start the Piper helper.");
        }

        try
        {
            lock (_gate)
            {
                // A Stop between Start and this assignment must not leave an
                // untracked helper process running.
                if (generation >= 0
                    && generation == Volatile.Read(ref _generation)
                    && !token.IsCancellationRequested)
                {
                    _helperProcess = process;
                }
            }

            if (token.IsCancellationRequested
                || (generation >= 0 && generation != Volatile.Read(ref _generation)))
            {
                KillProcess(process);
                throw new OperationCanceledException();
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            var output = await standardOutput.ConfigureAwait(false);
            var error = await standardError.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error) ? "Piper helper failed." : error.Trim());
            }

            return output;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_helperProcess, process))
                {
                    _helperProcess = null;
                }
            }
        }
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // The helper may have exited between the check and the kill.
        }
    }

    private SoundPlayer? PlayWav(string wavFile, int generation)
    {
        SoundPlayer? player = null;
        try
        {
            if (!File.Exists(wavFile))
            {
                return null;
            }

            player = new SoundPlayer(wavFile);
            lock (_gate)
            {
                if (generation != Volatile.Read(ref _generation))
                {
                    return player;
                }

                _player = player;
            }

            player.Load();
            player.Play();
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }

        // Ownership passes to SpeakAsync, which releases it after the utterance.
        return player;
    }

    private void ReleasePlayer(SoundPlayer? player)
    {
        if (player is null)
        {
            return;
        }

        try
        {
            player.Stop();
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }

        lock (_gate)
        {
            if (ReferenceEquals(_player, player))
            {
                _player = null;
            }
        }

        try
        {
            player.Dispose();
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }

    private static TimeSpan ParseDuration(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            const string prefix = "DURATION=";
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = trimmed[prefix.Length..].Trim();
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                && seconds > 0
                && seconds < 3600)
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }

        return TimeSpan.Zero;
    }

    private static string ResolvePython(AppConfig config)
    {
        var configured = config.PiperPython?.Trim() ?? string.Empty;
        if (configured.Length > 0 && File.Exists(configured))
        {
            return configured;
        }

        var repository = FindRepoRoot();
        if (repository is not null)
        {
            var bundled = Path.Combine(repository, ".venv", "Scripts", "python.exe");
            if (File.Exists(bundled))
            {
                return bundled;
            }
        }

        return "python";
    }

    private static string ResolveHelper()
    {
        var adjacent = Path.Combine(AppContext.BaseDirectory, "piper_helper.py");
        if (File.Exists(adjacent))
        {
            return adjacent;
        }

        var repository = FindRepoRoot();
        if (repository is not null)
        {
            var source = Path.Combine(repository, "piper_helper.py");
            if (File.Exists(source))
            {
                return source;
            }
        }

        return adjacent;
    }

    private static string ResolveModel(AppConfig config)
    {
        var model = config.PiperModel?.Trim() ?? string.Empty;
        var directory = config.PiperModelDir?.Trim() ?? string.Empty;
        if (model.Length > 0)
        {
            return Path.IsPathRooted(model) || directory.Length == 0
                ? model
                : Path.Combine(directory, model);
        }

        if (directory.Length > 0 && Directory.Exists(directory))
        {
            var first = Directory.EnumerateFiles(directory, "*.onnx")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (first is not null)
            {
                return first;
            }
        }

        return string.Empty;
    }

    private static string ResolveConfig(string model)
    {
        var sibling = model + ".json";
        if (File.Exists(sibling))
        {
            return sibling;
        }

        var changed = Path.ChangeExtension(model, ".json");
        return File.Exists(changed) ? changed : sibling;
    }

    private static string? FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "piper_helper.py"))
                || Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private void IsCurrent(int generation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (generation != Volatile.Read(ref _generation))
        {
            throw new OperationCanceledException();
        }
    }

    private static void DeleteTemp(string? path)
    {
        try
        {
            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Temporary files are best-effort.
        }
    }
}
