using Fleet.Application.Common;
using Fleet.Domain.Implements;
using FluentValidation;

namespace Fleet.Application.Implements;

public sealed class ImplementListRequest : ListRequest
{
    public ImplementStatus? Status { get; set; }
    public ImplementType? Type { get; set; }
}

public sealed record ImplementRequest : IRegisteredAssetRequest
{
    public string? LicensePlate { get; init; }
    public string? Renavam { get; init; }
    public string? Chassis { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public short? ManufacturingYear { get; init; }
    public short? ModelYear { get; init; }

    public ImplementType? Type { get; init; }
    public decimal? Capacity { get; init; }
    public CapacityUnit? CapacityUnit { get; init; }
    public decimal? TareWeightKg { get; init; }

    public ImplementStatus Status { get; init; } = ImplementStatus.Available;
    public string? Notes { get; init; }
}

public sealed record ImplementListItemResponse(
    Guid Id,
    string LicensePlate,
    string Manufacturer,
    string Model,
    short ModelYear,
    ImplementType Type,
    decimal? Capacity,
    CapacityUnit? CapacityUnit,
    ImplementStatus Status);

public sealed record ImplementResponse(
    Guid Id,
    string LicensePlate,
    string Renavam,
    string Chassis,
    string Manufacturer,
    string Model,
    short ManufacturingYear,
    short ModelYear,
    ImplementType Type,
    decimal? Capacity,
    CapacityUnit? CapacityUnit,
    decimal? TareWeightKg,
    ImplementStatus Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class ImplementRequestValidator : AbstractValidator<ImplementRequest>
{
    public ImplementRequestValidator(IClock clock)
    {
        Include(new RegisteredAssetValidator(clock));
        RuleFor(x => x.Type).NotNull().WithMessage("Tipo: campo obrigatório.").IsInEnum().WithMessage("Tipo inválido.");
        RuleFor(x => x.Capacity).NonNegative("Capacidade");
        RuleFor(x => x.CapacityUnit)
            .NotNull().When(x => x.Capacity is not null).WithMessage("Informe a unidade da capacidade (kg, litros ou m³).")
            .IsInEnum().WithMessage("Unidade de capacidade inválida.");
        RuleFor(x => x.TareWeightKg).NonNegative("Tara");
        RuleFor(x => x.Status).IsInEnum().WithMessage("Situação inválida.");
        RuleFor(x => x.Notes).MaxLen(Implement.NotesMaxLength);
    }
}
