using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Common;

/// <summary>Fields shared by vehicles and implements — both are registered vehicles in Brazil.</summary>
public interface IRegisteredAssetRequest
{
    string? LicensePlate { get; }
    string? Renavam { get; }
    string? Chassis { get; }
    string? Manufacturer { get; }
    string? Model { get; }
    short? ManufacturingYear { get; }
    short? ModelYear { get; }
}

/// <summary>Composed into the vehicle and implement validators with Include(...).</summary>
internal sealed class RegisteredAssetValidator : AbstractValidator<IRegisteredAssetRequest>
{
    public RegisteredAssetValidator(IClock clock)
    {
        RuleFor(x => x.LicensePlate).Required("Placa").ValidLicensePlate();
        RuleFor(x => x.Renavam).Required("RENAVAM").ValidRenavam();
        RuleFor(x => x.Chassis).Required("Chassi").ValidChassis();
        RuleFor(x => x.Manufacturer).Required("Fabricante").MaxLen(Vehicle.ManufacturerMaxLength);
        RuleFor(x => x.Model).Required("Modelo").MaxLen(Vehicle.ModelMaxLength);

        var maxYear = clock.Today.Year + 1;
        RuleFor(x => x.ManufacturingYear)
            .NotNull().WithMessage("Ano de fabricação: campo obrigatório.")
            .InclusiveBetween((short)Vehicle.MinimumYear, (short)maxYear)
            .WithMessage($"Ano de fabricação deve estar entre {Vehicle.MinimumYear} e {maxYear}.");
        RuleFor(x => x.ModelYear)
            .NotNull().WithMessage("Ano do modelo: campo obrigatório.")
            .Must((x, year) => x.ManufacturingYear is null || year == x.ManufacturingYear || year == x.ManufacturingYear + 1)
            .WithMessage("Ano do modelo deve ser igual ao ano de fabricação ou o ano seguinte.");
    }
}

public static class RegisteredAssetRules
{
    public static IRuleBuilderOptions<T, decimal?> NonNegative<T>(this IRuleBuilder<T, decimal?> rule, string label) =>
        rule.GreaterThanOrEqualTo(0).WithMessage($"{label} não pode ser negativo.");

    /// <summary>
    /// A plate identifies a single registered vehicle, so it must be unique across vehicles AND implements
    /// within the company. Each unique index only covers its own table; this covers both.
    /// </summary>
    public static async Task EnsurePlateIsFreeAsync(
        IFleetDbContext db, string plate, Guid? exceptVehicleId, Guid? exceptImplementId, CancellationToken ct)
    {
        if (await db.Vehicles.AnyAsync(v => v.LicensePlate == plate && v.Id != exceptVehicleId, ct))
            throw new ConflictException("Esta placa já está cadastrada em um veículo desta empresa.", "licensePlate");
        if (await db.Implements.AnyAsync(i => i.LicensePlate == plate && i.Id != exceptImplementId, ct))
            throw new ConflictException("Esta placa já está cadastrada em um implemento desta empresa.", "licensePlate");
    }
}
