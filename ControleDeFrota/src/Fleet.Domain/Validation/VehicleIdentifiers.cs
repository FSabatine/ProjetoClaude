using System.Text.RegularExpressions;

namespace Fleet.Domain.Validation;

/// <summary>Brazilian license plate: legacy "ABC1234" or Mercosul "ABC1D23".</summary>
public static partial class LicensePlate
{
    public const int Length = 7;

    /// <summary>Uppercase without hyphen, e.g. "ABC1D23".</summary>
    public static string Normalize(string? value) => DocumentText.Normalize(value);

    public static bool IsValid(string? value) => PlatePattern().IsMatch(Normalize(value));

    /// <summary>For messages: legacy "ABC-1234"; Mercosul stays "ABC1D23", as printed on the plate (mirrors formatPlate in the UI).</summary>
    public static string Format(string plate)
    {
        var normalized = Normalize(plate);
        return LegacyPattern().IsMatch(normalized) ? $"{normalized[..3]}-{normalized[3..]}" : normalized;
    }

    [GeneratedRegex("^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$")]
    private static partial Regex PlatePattern();

    [GeneratedRegex("^[A-Z]{3}[0-9]{4}$")]
    private static partial Regex LegacyPattern();
}

/// <summary>RENAVAM: 11 digits, last one is a check digit (weights 3,2,9,8,7,6,5,4,3,2).</summary>
public static class Renavam
{
    public const int Length = 11;
    private static readonly int[] Weights = [3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Digits only, left-padded with zeros (legacy 9-digit RENAVAMs are still valid).</summary>
    public static string Normalize(string? value)
    {
        var digits = DocumentText.Normalize(value);
        return digits.Length is >= 9 and < Length && DocumentText.IsAllDigits(digits) ? digits.PadLeft(Length, '0') : digits;
    }

    public static bool IsValid(string? value)
    {
        var renavam = Normalize(value);
        if (renavam.Length != Length || !DocumentText.IsAllDigits(renavam) || DocumentText.IsRepeatedChar(renavam)) return false;

        var sum = 0;
        for (var i = 0; i < Weights.Length; i++) sum += (renavam[i] - '0') * Weights[i];
        var checkDigit = sum * 10 % 11;
        if (checkDigit == 10) checkDigit = 0;
        return renavam[10] - '0' == checkDigit;
    }
}

/// <summary>Chassis number (VIN): 17 characters, letters I, O and Q are not allowed (ISO 3779).</summary>
public static partial class Chassis
{
    public const int Length = 17;

    public static string Normalize(string? value) => DocumentText.Normalize(value);

    public static bool IsValid(string? value) => VinPattern().IsMatch(Normalize(value));

    [GeneratedRegex("^[A-HJ-NPR-Z0-9]{17}$")]
    private static partial Regex VinPattern();
}
