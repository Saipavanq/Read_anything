namespace ReadAloud.Models;

public sealed record VoiceCard(
    string Name,
    string Description,
    string Language,
    string Gender,
    string QualityLabel,
    bool IsDefault);
