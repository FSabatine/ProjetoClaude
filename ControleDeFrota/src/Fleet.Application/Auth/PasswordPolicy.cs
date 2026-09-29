using FluentValidation;

namespace Fleet.Application.Auth;

/// <summary>NIST SP 800-63B oriented: length over complexity (see SECURITY.md).</summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;
    public const int MaxLength = 128;

    public static string? GetViolation(string? password, string? email)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return $"A senha deve ter pelo menos {MinLength} caracteres.";
        if (password.Length > MaxLength)
            return $"A senha deve ter no máximo {MaxLength} caracteres.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "A senha deve conter letras e números.";

        var emailLocalPart = email?.Split('@')[0];
        if (!string.IsNullOrWhiteSpace(emailLocalPart) && emailLocalPart.Length >= 3 &&
            password.Contains(emailLocalPart, StringComparison.OrdinalIgnoreCase))
            return "A senha não pode conter o seu e-mail.";

        return null;
    }

    public static IRuleBuilderOptionsConditions<T, string?> StrongPassword<T>(this IRuleBuilder<T, string?> rule, Func<T, string?> email) =>
        rule.Custom((password, context) =>
        {
            var violation = GetViolation(password, email(context.InstanceToValidate));
            if (violation is not null) context.AddFailure(violation);
        });
}
