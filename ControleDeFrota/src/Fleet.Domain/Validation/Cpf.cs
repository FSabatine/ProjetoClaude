namespace Fleet.Domain.Validation;

public static class Cpf
{
    public const int Length = 11;
    private static readonly int[] FirstWeights = [10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondWeights = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Digits only, e.g. "52998224725".</summary>
    public static string Normalize(string? value) => DocumentText.Normalize(value);

    public static bool IsValid(string? value)
    {
        var cpf = Normalize(value);
        if (cpf.Length != Length || !DocumentText.IsAllDigits(cpf) || DocumentText.IsRepeatedChar(cpf)) return false;

        var first = DocumentText.Mod11CheckDigit(cpf, FirstWeights, DigitValue);
        var second = DocumentText.Mod11CheckDigit(cpf, SecondWeights, DigitValue);
        return DigitValue(cpf[9]) == first && DigitValue(cpf[10]) == second;
    }

    private static int DigitValue(char c) => c - '0';
}
