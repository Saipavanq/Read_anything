using System.Collections.Generic;

namespace ReadAloud.Services;

public record SyntheticVoiceDescriptor(
    string Name,
    string Description,
    string Culture,
    string Gender,
    bool IsDefault);

public interface ISpeechSynthesizer
{
    bool IsSpeaking { get; }

    int Rate { get; set; }

    int Volume { get; set; }

    void SelectVoice(string name);

    void Speak(string text);

    void Stop();

    IReadOnlyList<SyntheticVoiceDescriptor> GetVoices();
}
