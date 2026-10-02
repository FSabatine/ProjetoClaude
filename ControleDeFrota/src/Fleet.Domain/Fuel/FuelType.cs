using Fleet.Domain.Common;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Fuel;

/// <summary>Family of a fuel product. Drives compatibility with the vehicle's engine and consumption grouping.</summary>
public enum FuelCategory
{
    Diesel,
    Gasoline,
    Ethanol,
    Cng,
    Electric,
    Other,
}

/// <summary>Unit the product is sold in. Consumption is "km per unit" (km/L, km/m³, km/kWh).</summary>
public enum FuelUnit
{
    Liter,
    CubicMeter,
    KilowattHour,
}

/// <summary>
/// Per-company catalog of fuel products bought at the pump (ADR-031): "Diesel S10", "Gasolina aditivada"…
/// Configurable, never hard-coded; a type with fuelings is deactivated instead of deleted.
/// </summary>
public class FuelType : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 60;
    public const int CodeMaxLength = 20;
    public const int DescriptionMaxLength = 300;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Short code (S10, GAS, ETA…), unique per company, upper case.</summary>
    public string Code { get; set; } = string.Empty;
    public FuelCategory Category { get; set; }
    public FuelUnit Unit { get; set; } = FuelUnit.Liter;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

public sealed record FuelTypeDefinition(string Name, string Code, FuelCategory Category, FuelUnit Unit);

/// <summary>Starting catalog created for every company the first time the catalog is read (same idea as DocumentTypeDefaults).</summary>
public static class FuelTypeDefaults
{
    public static readonly IReadOnlyList<FuelTypeDefinition> All =
    [
        new("Diesel S10", "S10", FuelCategory.Diesel, FuelUnit.Liter),
        new("Diesel S500", "S500", FuelCategory.Diesel, FuelUnit.Liter),
        new("Gasolina comum", "GAS", FuelCategory.Gasoline, FuelUnit.Liter),
        new("Gasolina aditivada", "GASAD", FuelCategory.Gasoline, FuelUnit.Liter),
        new("Etanol", "ETA", FuelCategory.Ethanol, FuelUnit.Liter),
        new("GNV", "GNV", FuelCategory.Cng, FuelUnit.CubicMeter),
        new("Recarga elétrica", "ELE", FuelCategory.Electric, FuelUnit.KilowattHour),
    ];
}

/// <summary>Whether a product fits the vehicle's engine. A mismatch is a warning for review, never a block.</summary>
public static class FuelCompatibility
{
    public static bool IsCompatible(VehicleFuelType vehicle, FuelCategory product) => product == FuelCategory.Other || vehicle switch
    {
        VehicleFuelType.DieselS10 or VehicleFuelType.DieselS500 => product == FuelCategory.Diesel,
        VehicleFuelType.Gasoline => product == FuelCategory.Gasoline,
        VehicleFuelType.Ethanol => product == FuelCategory.Ethanol,
        VehicleFuelType.Flex => product is FuelCategory.Gasoline or FuelCategory.Ethanol,
        // CNG kits are installed on gasoline/flex cars, which keep running on liquid fuel.
        VehicleFuelType.Cng => product is FuelCategory.Cng or FuelCategory.Gasoline or FuelCategory.Ethanol,
        VehicleFuelType.Electric => product == FuelCategory.Electric,
        VehicleFuelType.Hybrid => product is FuelCategory.Gasoline or FuelCategory.Ethanol or FuelCategory.Electric,
        _ => true,
    };
}

public static class FuelUnits
{
    public static string Symbol(FuelUnit unit) => unit switch
    {
        FuelUnit.CubicMeter => "m³",
        FuelUnit.KilowattHour => "kWh",
        _ => "L",
    };
}
