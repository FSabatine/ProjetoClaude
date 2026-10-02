using Fleet.Domain.Common;

namespace Fleet.Domain.Vehicles;

/// <summary>
/// Stored condition of the vehicle — a single axis (ADR-009, ADR-018). "Active" is derived: anything but Inactive.
/// Who drives it is NOT a status: it comes from VehicleAssignment (see VehicleOperationalStatus).
/// OnTrip and UnderMaintenance will be driven by the Trips and Maintenance modules in later phases.
/// </summary>
public enum VehicleStatus
{
    Available,
    OnTrip,
    UnderMaintenance,
    /// <summary>Out of use for a reason other than maintenance (blocked document, accident, waiting for paperwork…).</summary>
    Unavailable,
    Inactive,
}

/// <summary>
/// What the fleet manager sees (ADR-018): the stored condition combined with the current assignment.
/// Never stored — always computed by <see cref="VehicleOperationalState"/>.
/// </summary>
public enum VehicleOperationalStatus
{
    Available,
    /// <summary>Available condition + an active driver assignment.</summary>
    Assigned,
    OnTrip,
    Unavailable,
    UnderMaintenance,
    Inactive,
}

public static class VehicleOperationalState
{
    /// <summary>The stored condition wins; only an otherwise available vehicle is shown as "Assigned".</summary>
    public static VehicleOperationalStatus From(VehicleStatus status, bool hasActiveAssignment) => status switch
    {
        VehicleStatus.Inactive => VehicleOperationalStatus.Inactive,
        VehicleStatus.UnderMaintenance => VehicleOperationalStatus.UnderMaintenance,
        VehicleStatus.Unavailable => VehicleOperationalStatus.Unavailable,
        VehicleStatus.OnTrip => VehicleOperationalStatus.OnTrip,
        _ => hasActiveAssignment ? VehicleOperationalStatus.Assigned : VehicleOperationalStatus.Available,
    };
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

/// <summary>
/// What the vehicle's engine runs on (registry attribute). Not the product bought at the pump: that is the configurable
/// <c>Fleet.Domain.Fuel.FuelType</c> catalog (ADR-031). Flex/Hybrid are vehicle traits, which is why this stays an enum.
/// </summary>
public enum VehicleFuelType
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
/// Kept lean on purpose: assignments, odometer readings, documents, checklists, occurrences (and, later,
/// maintenance, fueling, tires, trips, costs) are separate entities referencing VehicleId.
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
    public VehicleFuelType FuelType { get; set; }
    public decimal? CargoCapacityKg { get; set; }
    public decimal? TareWeightKg { get; set; }

    // Fuel profile (Phase 4). Attributes of the vehicle, not operational records — fuelings are their own entity.
    /// <summary>Main tank capacity in the fuel's unit (L, m³ or kWh). Used to flag fuelings above capacity.</summary>
    public decimal? FuelTankCapacity { get; set; }
    /// <summary>Second tank, when the vehicle has one (common on trucks). The fueling limit is the sum of both.</summary>
    public decimal? SecondaryFuelTankCapacity { get; set; }
    /// <summary>Configured expected consumption (km per unit). When set, it is the consumption baseline (ADR-032).</summary>
    public decimal? ExpectedConsumption { get; set; }

    /// <summary>Axle configuration (Phase 5, ADR-036): which wheel positions the vehicle has. Changed only by TireService.</summary>
    public Guid? TireLayoutId { get; set; }

    /// <summary>Fast read of the latest applied odometer reading. Changed only by MileageService (ADR-019).</summary>
    public int CurrentOdometerKm { get; set; }
    /// <summary>When the current odometer value was read (null = never read since registration).</summary>
    public DateTime? OdometerUpdatedAt { get; set; }
    /// <summary>Fast read of the latest applied hour-meter reading. Changed only by HourMeterService (Fase 3).</summary>
    public decimal? HourMeter { get; set; }
    /// <summary>When the current hour meter value was read (null = never read through HourMeterService).</summary>
    public DateTime? HourMeterUpdatedAt { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    public DateOnly? AcquisitionDate { get; set; }
    public decimal? AcquisitionValue { get; set; }

    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
