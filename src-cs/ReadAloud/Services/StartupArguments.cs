using System;
using System.Collections.Generic;
using System.Linq;

namespace ReadAloud.Services;

public static class StartupArguments
{
    public static bool ShouldStartHidden(IEnumerable<string> args)
        => args is not null && args.Any(arg => string.Equals(arg, "--hidden", StringComparison.OrdinalIgnoreCase));

    public static bool ShouldShowMainWindow(bool startHidden)
        => !startHidden;
}
