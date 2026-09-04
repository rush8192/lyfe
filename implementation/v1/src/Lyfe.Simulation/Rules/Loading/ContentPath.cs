namespace Lyfe.Simulation.Rules.Loading;

internal static class ContentPath
{
    public const int MaximumLength = 240;

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length > MaximumLength ||
            value[0] == '/' ||
            value[^1] == '/' ||
            value.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        var segmentStart = 0;
        for (var index = 0; index <= value.Length; index++)
        {
            if (index < value.Length && value[index] != '/')
            {
                var character = value[index];
                if (!IsAllowedCharacter(character))
                {
                    return false;
                }

                continue;
            }

            var segment = value.AsSpan(segmentStart, index - segmentStart);
            if (segment.IsEmpty || segment.SequenceEqual(".") || segment.SequenceEqual(".."))
            {
                return false;
            }

            segmentStart = index + 1;
        }

        return true;
    }

    private static bool IsAllowedCharacter(char value) =>
        value is >= 'a' and <= 'z' or
        >= '0' and <= '9' or
        '.' or '-' or '_' or '/';
}

