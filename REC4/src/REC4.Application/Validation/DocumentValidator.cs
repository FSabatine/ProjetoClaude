using System.Text.RegularExpressions;
using REC4.Domain.Enums;

namespace REC4.Application.Validation;

public static class DocumentValidator
{
    public static string OnlyDigits(string value) => Regex.Replace(value ?? string.Empty, "[^0-9]", "");

    public static bool IsValid(string document, TipoPessoa tipoPessoa)
    {
        var digits = OnlyDigits(document);
        return tipoPessoa == TipoPessoa.Fisica ? IsValidCpf(digits) : IsValidCnpj(digits);
    }

    public static bool IsValidCpf(string cpf)
    {
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1)
            return false;

        var digits = cpf.Select(c => c - '0').ToArray();

        var firstCheck = CalculateCheckDigit(digits[..9], 10);
        if (firstCheck != digits[9])
            return false;

        var secondCheck = CalculateCheckDigit(digits[..10], 11);
        return secondCheck == digits[10];
    }

    public static bool IsValidCnpj(string cnpj)
    {
        if (cnpj.Length != 14 || cnpj.Distinct().Count() == 1)
            return false;

        var digits = cnpj.Select(c => c - '0').ToArray();
        int[] firstWeights = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] secondWeights = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        var firstCheck = CalculateCheckDigit(digits[..12], firstWeights);
        if (firstCheck != digits[12])
            return false;

        var secondCheck = CalculateCheckDigit(digits[..13], secondWeights);
        return secondCheck == digits[13];
    }

    private static int CalculateCheckDigit(int[] digits, int startWeight)
    {
        var weights = Enumerable.Range(0, digits.Length)
            .Select(i => startWeight - i)
            .ToArray();
        return CalculateCheckDigit(digits, weights);
    }

    private static int CalculateCheckDigit(int[] digits, int[] weights)
    {
        var sum = digits.Zip(weights, (d, w) => d * w).Sum();
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
