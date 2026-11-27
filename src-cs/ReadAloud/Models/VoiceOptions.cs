namespace ReadAloud.Models;

public record VoiceOptions(string VoiceName, int Rate, int Volume, string? VoiceId = null)
{
    public static VoiceOptions FromConfig(AppConfig config)
        => new(config.OfflineVoice, config.SapiRate, config.Volume);
}
