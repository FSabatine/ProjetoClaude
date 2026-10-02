using Fleet.Domain.Common;

namespace Fleet.Domain.Implements;

public enum ImplementType
{
    Trailer,
    SemiTrailer,
    Tanker,
    BoxBody,
    Sider,
    Dolly,
    Other,
}

/// <summary>Same availability axis as vehicles (ADR-009); InUse = coupled/operating.</summary>
public enum ImplementStatus
{
    Available,
    InUse,
    UnderMaintenance,
    Inactive,
}

/// <summary>A tanker is measured in liters, a box body in kg or m³ — the unit is explicit.</summary>
public enum CapacityUnit
{
    Kg,
    Liters,
    CubicMeters,
}

/// <summary>
/// Trailer/semi-trailer/etc. The vehicle ↔ implement link will be its own entity with validity
/// (VehicleImplementCoupling, Phase 2) — not a foreign key here — to keep composition history.
/// </summary>
public class Implement : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NotesMaxLength = 2000;

    public Guid CompanyId { get; set; }

    public string LicensePlate { get; set; } = string.Empty;
    public string Renavam { get; set; } = string.Empty;
    public string Chassis { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public short ManufacturingYear { get; set; }
    public short ModelYear { get; set; }

    public ImplementType Type { get; set; }
    public decimal? Capacity { get; set; }
    public CapacityUnit? CapacityUnit { get; set; }
    public decimal? TareWeightKg { get; set; }

    public ImplementStatus Status { get; set; } = ImplementStatus.Available;
    /// <summary>Axle configuration (Phase 5) — same concept as the vehicle's. Changed only by TireService.</summary>
    public Guid? TireLayoutId { get; set; }
    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
