using Fleet.Application.Common;
using Fleet.Domain.Common;
using Fleet.Domain.Mileage;
using Fleet.Domain.Vehicles;
using FluentValidation;

namespace Fleet.Application.Vehicles;

public sealed class VehicleListRequest : ListRequest
{
    public VehicleStatus? Status { get; set; }
    /// <summary>Derived status (ADR-018): condition + current assignment.</summary>
    public VehicleOperationalStatus? OperationalStatus { get; set; }
    public VehicleType? Type { get; set; }
    public Guid? DriverId { get; set; }
    public int? MinOdometerKm { get; set; }
    public int? MaxOdometerKm { get; set; }
    /// <summary>Active vehicles whose odometer was not updated in the last <see cref="Fleet.Domain.Mileage.OdometerPolicy.StaleAfterDays"/> days.</summary>
    public bool? StaleMileage { get; set; }
}

public sealed record CurrentAssignmentResponse(Guid Id, Guid DriverId, string DriverName, DateTime StartedAt);

public sealed record VehicleRequest : IRegisteredAssetRequest
{
    public string? LicensePlate { get; init; }
    public string? Renavam { get; init; }
    public string? Chassis { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public short? ManufacturingYear { get; init; }
    public short? ModelYear { get; init; }
    public string? Color { get; init; }

    public VehicleType? Type { get; init; }
    public VehicleCategory? Category { get; init; }
    public FuelType? FuelType { get; init; }
    public decimal? CargoCapacityKg { get; init; }
    public decimal? TareWeightKg { get; init; }

    /// <summary>Initial odometer on registration. On update it must be omitted or unchanged: mileage changes go through readings (ADR-019).</summary>
    public int? CurrentOdometerKm { get; init; }
    public decimal? HourMeter { get; init; }
    public VehicleStatus Status { get; init; } = VehicleStatus.Available;

    public DateOnly? AcquisitionDate { get; init; }
    public decimal? AcquisitionValue { get; init; }
    public string? Notes { get; init; }
}

public sealed record VehicleListItemResponse(
    Guid Id,
    string LicensePlate,
    string Manufacturer,
    string Model,
    short ModelYear,
    VehicleType Type,
    int CurrentOdometerKm,
    DateTime? OdometerUpdatedAt,
    VehicleStatus Status,
    VehicleOperationalStatus OperationalStatus,
    Guid? CurrentDriverId,
    string? CurrentDriverName);

public sealed record VehicleResponse(
    Guid Id,
    string LicensePlate,
    string Renavam,
    string Chassis,
    string Manufacturer,
    string Model,
    short ManufacturingYear,
    short ModelYear,
    string? Color,
    VehicleType Type,
    VehicleCategory? Category,
    FuelType FuelType,
    decimal? CargoCapacityKg,
    decimal? TareWeightKg,
    int CurrentOdometerKm,
    DateTime? OdometerUpdatedAt,
    decimal? HourMeter,
    VehicleStatus Status,
    VehicleOperationalStatus OperationalStatus,
    CurrentAssignmentResponse? CurrentAssignment,
    DateOnly? AcquisitionDate,
    decimal? AcquisitionValue,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class VehicleRequestValidator : AbstractValidator<VehicleRequest>
{
    public const int MaxOdometerKm = OdometerPolicy.MaxOdometerKm;

    public VehicleRequestValidator(IClock clock)
    {
        Include(new RegisteredAssetValidator(clock));
        RuleFor(x => x.Color).MaxLen(Vehicle.ColorMaxLength);
        RuleFor(x => x.Type).NotNull().WithMessage("Tipo: campo obrigatório.").IsInEnum().WithMessage("Tipo inválido.");
        RuleFor(x => x.Category).IsInEnum().WithMessage("Categoria inválida.");
        RuleFor(x => x.FuelType).NotNull().WithMessage("Combustível: campo obrigatório.").IsInEnum().WithMessage("Combustível inválido.");
        RuleFor(x => x.CargoCapacityKg).NonNegative("Capacidade de carga");
        RuleFor(x => x.TareWeightKg).NonNegative("Tara");
        RuleFor(x => x.CurrentOdometerKm)
            .InclusiveBetween(0, MaxOdometerKm).WithMessage($"Hodômetro deve estar entre 0 e {BrazilianFormat.Number(MaxOdometerKm)} km.");
        RuleFor(x => x.HourMeter).NonNegative("Horímetro");
        RuleFor(x => x.Status).IsInEnum().WithMessage("Situação inválida.");
        RuleFor(x => x.AcquisitionDate)
            .Must(d => d is null || (d.Value.Year >= Vehicle.MinimumYear && d.Value <= clock.Today))
            .WithMessage("Data de aquisição inválida: não pode ser futura.");
        RuleFor(x => x.AcquisitionValue).NonNegative("Valor de aquisição");
        RuleFor(x => x.Notes).MaxLen(Vehicle.NotesMaxLength);
    }
}
