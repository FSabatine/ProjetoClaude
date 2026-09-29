using Fleet.Domain.Validation;
using FluentValidation;

namespace Fleet.Application.Common;

/// <summary>
/// Reusable FluentValidation rules backed by Fleet.Domain.Validation — one rule, one place (DRY).
/// Optional fields: combine with .When(x => !string.IsNullOrWhiteSpace(...)).
/// </summary>
public static class ValidationRules
{
    public static IRuleBuilderOptions<T, string?> ValidCpf<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(Cpf.IsValid).WithMessage("CPF inválido. Confira os 11 dígitos.");

    public static IRuleBuilderOptions<T, string?> ValidCnpj<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(Cnpj.IsValid).WithMessage("CNPJ inválido. Confira os 14 caracteres e os dígitos verificadores.");

    public static IRuleBuilderOptions<T, string?> ValidLicensePlate<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(LicensePlate.IsValid).WithMessage("Placa inválida. Use o formato ABC-1234 ou Mercosul ABC-1D23.");

    public static IRuleBuilderOptions<T, string?> ValidRenavam<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(Renavam.IsValid).WithMessage("RENAVAM inválido. Confira os 11 dígitos no documento do veículo (CRLV).");

    public static IRuleBuilderOptions<T, string?> ValidChassis<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(Chassis.IsValid).WithMessage("Chassi inválido. Deve ter 17 caracteres, sem as letras I, O e Q.");

    public static IRuleBuilderOptions<T, string?> ValidZipCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(ZipCode.IsValid).WithMessage("CEP inválido. Informe os 8 dígitos.");

    public static IRuleBuilderOptions<T, string?> ValidPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(Phone.IsValid).WithMessage("Telefone inválido. Informe DDD + número, ex.: (41) 99999-9999.");

    public static IRuleBuilderOptions<T, string?> ValidEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(EmailAddress.IsValid).WithMessage("E-mail inválido. Use o formato nome@empresa.com.br.");

    public static IRuleBuilderOptions<T, string?> ValidState<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(BrazilianStates.IsValid).WithMessage("UF inválida. Selecione um estado da lista.");

    public static IRuleBuilderOptions<T, string?> ValidDriverLicenseNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(DriverLicenseNumber.IsValid).WithMessage("Número da CNH inválido. Informe os 11 dígitos do registro.");

    public static IRuleBuilderOptions<T, string?> Required<T>(this IRuleBuilder<T, string?> rule, string label) =>
        rule.Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage($"{label}: campo obrigatório.");

    public static IRuleBuilderOptions<T, string?> MaxLen<T>(this IRuleBuilder<T, string?> rule, int max) =>
        rule.Must(v => v is null || v.Trim().Length <= max).WithMessage($"Use no máximo {max} caracteres.");

    public static string? TrimToNull(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
