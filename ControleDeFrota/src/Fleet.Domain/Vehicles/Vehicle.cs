using Fleet.Domain.Common;

namespace Fleet.Domain.Vehicles;

/// <summary>
/// Single availability axis (ADR-009). "Active" is derived: any status other than Inactive.
/// From Phase 2/3 on, OnTrip and UnderMaintenance will be driven by the Trips and Maintenance modules.
/// </summary>
public enum VehicleStatus
{
    Available,
    OnTrip,
    UnderMaintenance,
    Inactive,
}

public enum VehicleType
{
    Truck,
    TruckTractor,
    Van,
    Pickup,
    Car,
    Motorcycle,
    Bus,
    Other,
}

/// <summary>Weight class (provisional interpretation — see DECISIONS, open points).</summary>
public enum VehicleCategory
{
    Light,
    Medium,
    SemiHeavy,
    Heavy,
}

public enum FuelType
{
    DieselS10,
    DieselS500,
    Gasoline,
    Ethanol,
    Flex,
    Cng,
    Electric,
    Hybrid,
    Other,
}

/// <summary>
/// Kept lean on purpose: maintenance, fueling, tires, documents, trips, fines, accidents and costs
/// will be separate entities referencing VehicleId.
/// </summary>
public class Vehicle : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int ManufacturerMaxLength = 60;
    public const int ModelMaxLength = 80;
    public const int ColorMaxLength = 30;
    public const int NotesMaxLength = 2000;
    public const int MinimumYear = 1950;

    public Guid CompanyId { get; set; }

    public string LicensePlate { get; set; } = string.Empty;
    public string Renavam { get; set; } = string.Empty;
    public string Chassis { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public short ManufacturingYear { get; set; }
    public short ModelYear { get; set; }
    public string? Color { get; set; }

    public VehicleType Type { get; set; }
    public VehicleCategory? Category { get; set; }
    public FuelType FuelType { get; set; }
    public decimal? CargoCapacityKg { get; set; }
    public decimal? TareWeightKg { get; set; }

    public int CurrentOdometerKm { get; set; }
    public decimal? HourMeter { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    public DateOnly? AcquisitionDate { get; set; }
    public decimal? AcquisitionValue { get; set; }

    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
