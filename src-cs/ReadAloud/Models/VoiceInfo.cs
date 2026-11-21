namespace ReadAloud.Models;

public record VoiceInfo(
    string Name,
    string Description,
    string Language,
    string Gender,
    bool IsDefault);
