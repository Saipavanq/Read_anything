using System;
using System.Collections.Generic;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class SelectableSpeechEngine : ISpeechEngine
{
    private readonly ISpeechEngine _sapi;
    private readonly ISpeechEngine _piper;
    private readonly ISpeechEngine _edge;
    private readonly Func<AppConfig> _configProvider;

    // Legacy overload: before Edge existed every selection other than SAPI
    // resolved to Piper, and the same events were forwarded once.
    public SelectableSpeechEngine(
        ISpeechEngine sapi,
        ISpeechEngine piper,
        Func<AppConfig> configProvider)
        : this(sapi, piper, piper, configProvider)
    {
    }

    public SelectableSpeechEngine(
        ISpeechEngine sapi,
        ISpeechEngine piper,
        ISpeechEngine edge,
        Func<AppConfig> configProvider)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _piper = piper ?? throw new ArgumentNullException(nameof(piper));
        _edge = edge ?? throw new ArgumentNullException(nameof(edge));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));

        _sapi.SpeakCompleted += () => ForwardCompleted(_sapi);
        _sapi.SpeakProgress += (position, count) => ForwardProgress(_sapi, position, count);
        _piper.SpeakCompleted += () => ForwardCompleted(_piper);
        _piper.SpeakProgress += (position, count) => ForwardProgress(_piper, position, count);
        if (!ReferenceEquals(edge, piper))
        {
            _edge.SpeakCompleted += () => ForwardCompleted(_edge);
            _edge.SpeakProgress += (position, count) => ForwardProgress(_edge, position, count);
        }
    }

    public event Action? SpeakCompleted;

    public event Action<int, int>? SpeakProgress;

    public bool IsSpeaking
    {
        get
        {
            try
            {
                return Active.IsSpeaking;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public IReadOnlyList<VoiceInfo> GetVoices()
    {
        try
        {
            return Active.GetVoices();
        }
        catch (Exception)
        {
            return Array.Empty<VoiceInfo>();
        }
    }

    public void Speak(string text, VoiceOptions options)
    {
        try
        {
            Active.Speak(text, options);
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }

    public void Stop()
    {
        try
        {
            Active.Stop();
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }

    public void Pause()
    {
        try
        {
            Active.Pause();
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }

    public void Resume()
    {
        try
        {
            Active.Resume();
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }

    private ISpeechEngine Active
    {
        get
        {
            try
            {
                var engine = _configProvider()?.TtsEngine;
                if (string.Equals(engine, "sapi", StringComparison.OrdinalIgnoreCase))
                {
                    return _sapi;
                }

                return string.Equals(engine, "edge", StringComparison.OrdinalIgnoreCase)
                    ? _edge
                    : _piper;
            }
            catch (Exception)
            {
                return _piper;
            }
        }
    }

    private void ForwardCompleted(ISpeechEngine source)
    {
        try
        {
            if (ReferenceEquals(source, Active))
            {
                SpeakCompleted?.Invoke();
            }
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }

    private void ForwardProgress(ISpeechEngine source, int position, int count)
    {
        try
        {
            if (ReferenceEquals(source, Active))
            {
                SpeakProgress?.Invoke(position, count);
            }
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }
}
