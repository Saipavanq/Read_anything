using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;

namespace ReadAloud.Services;

internal sealed class SapiSynthesizerAdapter : ISpeechSynthesizer
{
    private readonly SpeechSynthesizer _synthesizer = new();

    public SapiSynthesizerAdapter()
    {
        _synthesizer.SpeakCompleted += (_, _) => SpeakCompleted?.Invoke();
        _synthesizer.SpeakProgress += (_, args) =>
            SpeakProgress?.Invoke(args.CharacterPosition, args.CharacterCount);
    }

    public event Action? SpeakCompleted;

    public event Action<int, int>? SpeakProgress;

    public bool IsSpeaking => _synthesizer.State == SynthesizerState.Speaking;

    public int Rate
    {
        get => _synthesizer.Rate;
        set => _synthesizer.Rate = value;
    }

    public int Volume
    {
        get => _synthesizer.Volume;
        set => _synthesizer.Volume = value;
    }

    public void SelectVoice(string name) => _synthesizer.SelectVoice(name);

    public void Speak(string text) => _synthesizer.SpeakAsync(text);

    public void Stop() => _synthesizer.SpeakAsyncCancelAll();

    public IReadOnlyList<SyntheticVoiceDescriptor> GetVoices()
    {
        var installed = _synthesizer.GetInstalledVoices();
        var current = _synthesizer.Voice;

        return installed
            .Where(voice => voice.Enabled)
            .Select(voice => new SyntheticVoiceDescriptor(
                voice.VoiceInfo.Name,
                voice.VoiceInfo.Description,
                voice.VoiceInfo.Culture.Name,
                voice.VoiceInfo.Gender.ToString(),
                string.Equals(voice.VoiceInfo.Name, current.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
