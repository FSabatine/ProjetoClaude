using System.Text.RegularExpressions;

namespace Fleet.Domain.Validation;

/// <summary>CEP: 8 digits.</summary>
public static class ZipCode
{
    public const int Length = 8;

    public static string Normalize(string? value) => DocumentText.Normalize(value);

    public static bool IsValid(string? value)
    {
        var zip = Normalize(value);
        return zip.Length == Length && DocumentText.IsAllDigits(zip) && zip != "00000000";
    }
}

/// <summary>Brazilian phone with area code: landline (10 digits) or mobile (11 digits starting with 9).</summary>
public static partial class Phone
{
    public const int MaxLength = 11;

    public static string Normalize(string? value) => DocumentText.Normalize(value);

    public static bool IsValid(string? value) => PhonePattern().IsMatch(Normalize(value));

    [GeneratedRegex("^[1-9]{2}(9[0-9]{8}|[2-8][0-9]{7})$")]
    private static partial Regex PhonePattern();
}

public static partial class EmailAddress
{
    public const int MaxLength = 254;

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Pragmatic check (local@domain.tld, no spaces). Deliverability is not verified.</summary>
    public static bool IsValid(string? value)
    {
        var email = Normalize(value);
        return email.Length <= MaxLength && EmailPattern().IsMatch(email);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$")]
    private static partial Regex EmailPattern();
}

public static class BrazilianStates
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    };

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? value) => All.Contains(Normalize(value));
}

/// <summary>
/// CNH registration number: 11 digits. Only the format is validated — public check-digit
/// algorithms diverge and a wrong one would block real drivers (see DECISIONS, open points).
/// </summary>
public static class DriverLicenseNumber
{
    public const int Length = 11;

    public static string Normalize(string? value) => DocumentText.Normalize(value);

    public static bool IsValid(string? value)
    {
        var number = Normalize(value);
        return number.Length == Length && DocumentText.IsAllDigits(number) && !DocumentText.IsRepeatedChar(number);
    }
}
