using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace ReadAloud.Services;

internal sealed class UiaSelectionReader : IUiaSelectionReader
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private const int MaxAncestors = 4;

    public async Task<string?> TryReadSelectionAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        do
        {
            try
            {
                var text = TryReadFocusedSelection();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            catch (Exception)
            {
                // UIA providers can throw while an application is repainting or closing.
            }

            await Task.Delay(PollInterval);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    private static string? TryReadFocusedSelection()
    {
        var element = AutomationElement.FocusedElement;
        var depth = 0;

        while (element is not null && depth <= MaxAncestors)
        {
            var text = TryReadSelection(element);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            element = TreeWalker.ControlViewWalker.GetParent(element);
            depth++;
        }

        return null;
    }

    private static string? TryReadSelection(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)
            || pattern is not TextPattern textPattern)
        {
            return null;
        }

        TextPatternRange[] ranges;
        try
        {
            ranges = textPattern.GetSelection();
        }
        catch (Exception)
        {
            return null;
        }

        if (ranges.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var range in ranges)
        {
            var selected = range.GetText(-1);
            if (!string.IsNullOrEmpty(selected))
            {
                builder.Append(selected);
            }
        }

        var text = builder.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
