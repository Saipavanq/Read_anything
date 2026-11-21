using System;
using System.Collections.Generic;
using System.Linq;
using ReadAloud.Models;

namespace ReadAloud.Services;

public sealed class SapiSpeechEngine : ISpeechEngine
{
    private readonly ISpeechSynthesizer _synthesizer;

    public SapiSpeechEngine()
        : this(new SapiSynthesizerAdapter())
    {
    }

    public SapiSpeechEngine(ISpeechSynthesizer synthesizer)
    {
        _synthesizer = synthesizer;
    }

    public bool IsSpeaking => _synthesizer.IsSpeaking;

    public IReadOnlyList<VoiceInfo> GetVoices()
    {
        try
        {
            return _synthesizer.GetVoices().Select(MapVoice).ToList();
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
            var voice = VoiceIdentity.Resolve(options.VoiceName, GetVoices());
            if (voice is not null && !string.IsNullOrWhiteSpace(voice.Name))
            {
                _synthesizer.SelectVoice(voice.Name);
            }

            _synthesizer.Rate = Math.Clamp(options.Rate, -10, 10);
            _synthesizer.Volume = Math.Clamp(options.Volume, 0, 100);
            _synthesizer.Speak(text);
        }
        catch (Exception)
        {
            try
            {
                _synthesizer.Stop();
            }
            catch (Exception)
            {
                // Speech failures must never reach the UI layer.
            }
        }
    }

    public void Stop()
    {
        try
        {
            _synthesizer.Stop();
        }
        catch (Exception)
        {
            // Speech failures must never reach the UI layer.
        }
    }

    private static VoiceInfo MapVoice(SyntheticVoiceDescriptor descriptor)
        => new(
            descriptor.Name,
            descriptor.Description,
            string.IsNullOrWhiteSpace(descriptor.Culture) ? "Not specified" : descriptor.Culture,
            NormalizeGender(descriptor.Gender),
            descriptor.IsDefault);

    private static string NormalizeGender(string gender)
        => string.IsNullOrWhiteSpace(gender)
            || string.Equals(gender, "NotSet", StringComparison.OrdinalIgnoreCase)
                ? "Not specified"
                : gender;
}
