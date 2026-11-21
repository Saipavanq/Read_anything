namespace ReadAloud.Models;

public static class VoiceIdentity
{
    public static bool Matches(string configured, VoiceInfo voice)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        return string.Equals(configured, voice.Description, StringComparison.OrdinalIgnoreCase)
            || string.Equals(configured, voice.Name, StringComparison.OrdinalIgnoreCase);
    }
}
