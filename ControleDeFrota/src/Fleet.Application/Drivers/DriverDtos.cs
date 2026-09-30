using Fleet.Application.Common;
using Fleet.Domain.Drivers;
using FluentValidation;

namespace Fleet.Application.Drivers;

public enum LicenseAlertFilter
{
    Expired,
    ExpiringSoon,
}

public enum DriverAssignmentFilter
{
    WithVehicle,
    WithoutVehicle,
}

public sealed class DriverListRequest : ListRequest
{
    public DriverStatus? Status { get; set; }
    public LicenseAlertFilter? LicenseAlert { get; set; }
    public LicenseCategory? LicenseCategory { get; set; }
    public DriverAssignmentFilter? Assignment { get; set; }
    public Guid? VehicleId { get; set; }
}

public sealed record DriverCurrentVehicleResponse(Guid AssignmentId, Guid VehicleId, string LicensePlate, string VehicleDescription, DateTime StartedAt);

public sealed record DriverRequest
{
    public string? FullName { get; init; }
    public string? Cpf { get; init; }
    public string? Rg { get; init; }
    public DateOnly? BirthDate { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public AddressDto Address { get; init; } = new();
    public string? LicenseNumber { get; init; }
    public LicenseCategory? LicenseCategory { get; init; }
    public DateOnly? LicenseExpiresOn { get; init; }
    public bool PerformsPaidActivity { get; init; }
    public DriverStatus Status { get; init; } = DriverStatus.Active;
    public string? Notes { get; init; }
}

public enum LicenseState
{
    Valid,
    ExpiringSoon,
    Expired,
}

public sealed record DriverListItemResponse(
    Guid Id,
    string FullName,
    string Cpf,
    string? Phone,
    LicenseCategory LicenseCategory,
    DateOnly LicenseExpiresOn,
    LicenseState LicenseState,
    DriverStatus Status,
    Guid? CurrentVehicleId,
    string? CurrentVehiclePlate);

public sealed record DriverResponse(
    Guid Id,
    string FullName,
    string Cpf,
    string? Rg,
    DateOnly BirthDate,
    string? Phone,
    string? Email,
    AddressDto Address,
    string LicenseNumber,
    LicenseCategory LicenseCategory,
    DateOnly LicenseExpiresOn,
    LicenseState LicenseState,
    bool PerformsPaidActivity,
    DriverStatus Status,
    DriverCurrentVehicleResponse? CurrentVehicle,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class DriverRequestValidator : AbstractValidator<DriverRequest>
{
    public DriverRequestValidator(IClock clock)
    {
        RuleFor(x => x.FullName).Required("Nome completo").MaxLen(Driver.FullNameMaxLength);
        RuleFor(x => x.Cpf).Required("CPF").ValidCpf();
        RuleFor(x => x.Rg).MaxLen(Driver.RgMaxLength);
        RuleFor(x => x.BirthDate)
            .NotNull().WithMessage("Data de nascimento é obrigatória.")
            .Must(d => d is null || IsAgeInRange(d.Value, clock.Today))
            .WithMessage($"Data de nascimento inválida. O motorista deve ter entre {Driver.MinimumAge} e {Driver.MaximumAge} anos.");
        RuleFor(x => x.Phone).ValidPhone().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email).ValidEmail().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).NotNull().SetValidator(new AddressValidator(required: false));
        RuleFor(x => x.LicenseNumber).Required("Número da CNH").ValidDriverLicenseNumber();
        RuleFor(x => x.LicenseCategory)
            .NotNull().WithMessage("Categoria da CNH é obrigatória.")
            .IsInEnum().WithMessage("Categoria da CNH inválida.");
        RuleFor(x => x.LicenseExpiresOn)
            .NotNull().WithMessage("Validade da CNH é obrigatória.")
            .Must(d => d is null || (d.Value.Year >= 1990 && d.Value <= clock.Today.AddYears(15)))
            .WithMessage("Validade da CNH inválida. Confira a data impressa na CNH.");
        RuleFor(x => x.Status).IsInEnum().WithMessage("Situação inválida.");
        RuleFor(x => x.Notes).MaxLen(Driver.NotesMaxLength);
    }

    private static bool IsAgeInRange(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return age is >= Driver.MinimumAge and <= Driver.MaximumAge;
    }
}
