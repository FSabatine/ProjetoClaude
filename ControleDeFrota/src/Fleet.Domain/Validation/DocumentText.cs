namespace Fleet.Domain.Validation;

/// <summary>Normalization shared by every Brazilian document validator.</summary>
public static class DocumentText
{
    private static readonly char[] Separators = ['.', '-', '/', ' ', '(', ')'];

    /// <summary>Removes the usual mask separators, trims and uppercases. Returns "" for null.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.Trim().Where(c => !Separators.Contains(c)).Select(char.ToUpperInvariant);
        return new string(chars.ToArray());
    }

    public static bool IsAllDigits(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    public static bool IsRepeatedChar(string value) => value.Length > 0 && value.All(c => c == value[0]);

    /// <summary>Mod-11 check digit used by CPF and CNPJ: remainder &lt; 2 → 0, otherwise 11 − remainder.</summary>
    internal static int Mod11CheckDigit(string value, IReadOnlyList<int> weights, Func<char, int> charValue)
    {
        var sum = 0;
        for (var i = 0; i < weights.Count; i++) sum += charValue(value[i]) * weights[i];
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
