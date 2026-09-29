namespace Fleet.Domain.Validation;

/// <summary>
/// CNPJ validation supporting both the numeric and the alphanumeric format
/// (IN RFB 2.229/2024, issued since July 2026) — ADR-013.
/// The first 12 characters are [0-9A-Z]; the 2 check digits are always numeric.
/// Each character's value is its ASCII code minus 48, so digits keep their usual value.
/// </summary>
public static class Cnpj
{
    public const int Length = 14;
    private static readonly int[] FirstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Uppercase without separators, e.g. "12ABC34501DE35".</summary>
    public static string Normalize(string? value) => DocumentText.Normalize(value);

    public static bool IsValid(string? value)
    {
        var cnpj = Normalize(value);
        if (cnpj.Length != Length || DocumentText.IsRepeatedChar(cnpj)) return false;
        if (!cnpj[..12].All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c))) return false;
        if (!DocumentText.IsAllDigits(cnpj[12..])) return false;

        var first = DocumentText.Mod11CheckDigit(cnpj, FirstWeights, CharValue);
        var second = DocumentText.Mod11CheckDigit(cnpj, SecondWeights, CharValue);
        return CharValue(cnpj[12]) == first && CharValue(cnpj[13]) == second;
    }

    private static int CharValue(char c) => c - '0';
}
