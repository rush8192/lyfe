namespace Lyfe.Simulation.Rules.Loading;

internal static class StableKey
{
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsLowerAlpha(value[0]))
        {
            return false;
        }

        var previousWasSeparator = false;
        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            var isSeparator = character is '.' or '-';
            if (!IsLowerAlpha(character) && !IsDigit(character) && !isSeparator)
            {
                return false;
            }

            if (isSeparator && previousWasSeparator)
            {
                return false;
            }

            previousWasSeparator = isSeparator;
        }

        return !previousWasSeparator;
    }

    private static bool IsLowerAlpha(char value) => value is >= 'a' and <= 'z';

    private static bool IsDigit(char value) => value is >= '0' and <= '9';
}

