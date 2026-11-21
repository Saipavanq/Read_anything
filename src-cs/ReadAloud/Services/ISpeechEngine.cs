using System;
using System.Collections.Generic;
using ReadAloud.Models;

namespace ReadAloud.Services;

public interface ISpeechEngine
{
    event Action? SpeakCompleted;

    event Action<int, int>? SpeakProgress;

    bool IsSpeaking { get; }

    IReadOnlyList<VoiceInfo> GetVoices();

    void Speak(string text, VoiceOptions options);

    void Stop();
}
