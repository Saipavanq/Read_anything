using System.Collections.Generic;
using ReadAloud.Models;

namespace ReadAloud.Services;

public interface ISpeechEngine
{
    bool IsSpeaking { get; }

    IReadOnlyList<VoiceInfo> GetVoices();

    void Speak(string text, VoiceOptions options);

    void Stop();
}
