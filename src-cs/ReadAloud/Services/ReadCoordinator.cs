using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class ReadCoordinator
{
    private readonly object _gate = new();
    private readonly SelectionService _selection;
    private readonly PlaybackService _playback;
    private readonly ActivityLog _log;
    private readonly Func<AppConfig> _configProvider;
    private readonly HashSet<long> _active = new();
    private long _generation;

    public ReadCoordinator(
        SelectionService selection,
        PlaybackService playback,
        ActivityLog log,
        Func<AppConfig> configProvider)
    {
        _selection = selection;
        _playback = playback;
        _log = log;
        _configProvider = configProvider;
    }

    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _active.Count;
            }
        }
    }

    public async Task RequestReadAsync(
        string? manualText = null,
        string? source = null,
        CaptureMethod? captureMethod = null)
    {
        var config = _configProvider();
        long generation;

        lock (_gate)
        {
            generation = ++_generation;
            _active.Add(generation);
        }

        try
        {
            string text;
            var readSource = source;

            if (!string.IsNullOrWhiteSpace(manualText))
            {
                text = manualText;
                readSource ??= "manual";
            }
            else
            {
                var capture = await _selection.CaptureAsync(config.CaptureMethod);
                _log.Add($"Capture ({capture.Method}): {capture.Diagnostic}");
                text = capture.Text;
                readSource = capture.Method.ToString();
                captureMethod = config.CaptureMethod;

                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }
            }

            if (!IsCurrent(generation))
            {
                _log.Add($"Read {generation} discarded: superseded.");
                return;
            }

            if (text.Length > config.MaxChars)
            {
                text = text[..config.MaxChars];
                _log.Add($"Text truncated to {config.MaxChars} characters.");
            }

            _log.Add($"Reading {text.Length} characters from {readSource}.");
            await _playback.PlayAsync(
                text,
                VoiceOptions.FromConfig(config),
                readSource,
                captureMethod ?? config.CaptureMethod);
        }
        finally
        {
            lock (_gate)
            {
                _active.Remove(generation);
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _generation++;
            _active.Clear();
        }

        _playback.Stop();
        _log.Add("Stopped speaking and cleared pending reads.");
    }

    private bool IsCurrent(long generation)
    {
        lock (_gate)
        {
            return generation == _generation;
        }
    }
}
