using System;
using System.Collections.Generic;
using System.Linq;

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

    public static VoiceInfo? Resolve(string configured, IEnumerable<VoiceInfo> voices)
    {
        var list = voices as IReadOnlyList<VoiceInfo> ?? voices.ToList();

        if (!string.IsNullOrWhiteSpace(configured))
        {
            var match = list.FirstOrDefault(voice => Matches(configured, voice));
            if (match is not null)
            {
                return match;
            }
        }

        return list.FirstOrDefault(voice => voice.IsDefault) ?? list.FirstOrDefault();
    }
}
