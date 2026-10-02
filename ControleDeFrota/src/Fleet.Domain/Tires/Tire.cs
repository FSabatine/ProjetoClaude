using Fleet.Domain.Common;
using Fleet.Domain.Implements;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Tires;

/// <summary>
/// Where the tire is in its lifecycle (ADR-036). One axis, transitions only through <see cref="TireWorkflow"/>.
/// Sold, lost, scrapped… are <see cref="TireDisposalReason"/>s of the single final state, not states of their own.
/// "Retreaded" is not a state either: it is the life number (<see cref="Tire.RetreadCount"/>) of a tire back in stock.
/// </summary>
public enum TireStatus
{
    /// <summary>Em estoque: available for installation.</summary>
    InStock,
    Installed,
    /// <summary>Em avaliação: removed (or returned by a provider) and waiting for a decision.</summary>
    UnderInspection,
    UnderRepair,
    UnderRetread,
    /// <summary>Baixado (final): scrapped, sold, lost, transferred. Never installed again.</summary>
    Disposed,
}

public enum TireDisposalReason
{
    EndOfLife,
    IrreparableDamage,
    SafetyConcern,
    Lost,
    Sold,
    /// <summary>Transferred to another company/branch (outside this tenant).</summary>
    Transferred,
    Other,
}

/// <summary>
/// One physical tire (seção 4): its own identity, independent of the vehicle. Lifecycle facts live in their own records
/// (installations, inspections, service orders, costs, events); the columns below the identity block are fast reads kept
/// by TireService only — like Vehicle.CurrentOdometerKm is kept by MileageService.
/// </summary>
public class Tire : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int CodeMaxLength = 20;
    public const int SerialMaxLength = 40;
    public const int DotMaxLength = 20;
    public const int SupplierMaxLength = 120;
    public const int StorageMaxLength = 120;
    public const int NotesMaxLength = 2000;
    public const int DisposalTextMaxLength = 500;
    public const string CodePrefix = "PN-";

    public Guid CompanyId { get; set; }
    /// <summary>Drives the generated code (PN-000001) when the company does not use its own numbering.</summary>
    public int Sequence { get; set; }
    /// <summary>Número de fogo: the internal identifier branded on the sidewall. Unique per company.</summary>
    public string Code { get; set; } = string.Empty;
    public Guid TireModelId { get; set; }
    public TireModel Model { get; set; } = null!;
    public string? SerialNumber { get; set; }
    /// <summary>DOT as printed (normalized); its last four digits give the manufacturing week/year.</summary>
    public string? Dot { get; set; }
    public DateOnly? ManufacturedOn { get; set; }
    public DateOnly? PurchasedOn { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? Supplier { get; set; }
    /// <summary>Tread depth when new (mm); defaults to the model's.</summary>
    public decimal? OriginalTreadDepthMm { get; set; }
    /// <summary>Where a tire that is not installed is kept ("Almoxarifado A · Prateleira 3"). Not a warehouse module.</summary>
    public string? StorageLocation { get; set; }
    public string? Notes { get; set; }

    public TireStatus Status { get; set; } = TireStatus.InStock;

    // ---- fast reads (maintained by TireService) ----
    /// <summary>km of the closed installations. Current km = this + the open installation (see TireMileage).</summary>
    public int AccumulatedKm { get; set; }
    /// <summary>Some of the tire's life was run where km is unknown (implement, no odometer) — cost/km is then not shown.</summary>
    public bool HasUnmeasuredDistance { get; set; }
    /// <summary>Completed retreads (0 = original tread).</summary>
    public int RetreadCount { get; set; }
    public int RepairCount { get; set; }
    public decimal? CurrentTreadDepthMm { get; set; }
    public DateTime? TreadMeasuredAt { get; set; }
    public DateTime? LastInspectedAt { get; set; }
    public TireWearPattern? LastWearPattern { get; set; }
    public bool LastInspectionHasDamage { get; set; }
    public TirePressureCheck? LastPressureCheck { get; set; }
    /// <summary>Start of the inspection interval: the later of the last inspection and the installation from stock.</summary>
    public DateTime? InspectionReferenceAt { get; set; }
    /// <summary>Instant of the latest lifecycle movement — a new one may not be dated before it (history is append-only).</summary>
    public DateTime? LastMovementAt { get; set; }

    // ---- disposal (seção 42) ----
    public DateTime? DisposedAt { get; set; }
    public TireDisposalReason? DisposalReason { get; set; }
    public string? DisposalDestination { get; set; }
    public string? DisposalNotes { get; set; }
    public Guid? DisposedBy { get; set; }

    /// <summary>
    /// Optimistic concurrency token (seção 52): every lifecycle operation changes the tire, so two users acting on the same
    /// tire at once cannot both win — the second save fails and gets a 409.
    /// </summary>
    public int Version { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public static string FormatCode(int sequence) => CodePrefix + sequence.ToString("D6");
}

/// <summary>Why a tire went into a position.</summary>
public enum TireInstallReason
{
    /// <summary>First use or installation from stock.</summary>
    Installation,
    /// <summary>Replaced another tire in the position.</summary>
    Replacement,
    Rotation,
    /// <summary>Moved from another vehicle/implement.</summary>
    Transfer,
}

public enum TireRemovalReason
{
    Rotation,
    Replacement,
    Repair,
    Retread,
    Inspection,
    VehicleSale,
    VehicleDecommission,
    Damage,
    EndOfLife,
    /// <summary>Moved to another vehicle/implement.</summary>
    Transfer,
    Other,
}

/// <summary>Where a removed tire goes — decides its next status.</summary>
public enum TireRemovalDestination
{
    Stock,
    Evaluation,
    Repair,
    Retread,
    Disposal,
    /// <summary>Another position (rotation) or another asset (transfer): the tire stays installed.</summary>
    Installed,
}

/// <summary>
/// One stint of a tire in one position of one vehicle or implement (seções 11/13): installation and removal are the two ends
/// of the same record. A tire has many over its life; at most one is open (RemovedAt null) per tire and per position —
/// enforced by filtered unique indexes, not only by the service (seção 52).
/// </summary>
public class TireInstallation : AuditableEntity, ITenantScoped, IAuditable
{
    public const int NotesMaxLength = 1000;
    public const int LabelMaxLength = 60;

    public Guid CompanyId { get; set; }
    public Guid TireId { get; set; }
    public Tire Tire { get; set; } = null!;
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid? ImplementId { get; set; }
    public Implement? Implement { get; set; }

    // Position snapshot: the code is the identity on the asset; the label/axle survive a later layout change.
    public string PositionCode { get; set; } = string.Empty;
    public string PositionLabel { get; set; } = string.Empty;
    public int AxleNumber { get; set; }
    /// <summary>A spare does not roll: its stint adds no km.</summary>
    public bool IsSpare { get; set; }

    public DateTime InstalledAt { get; set; }
    /// <summary>Vehicle odometer at installation (from the mileage history). Null on implements (no odometer).</summary>
    public int? InstalledOdometerKm { get; set; }
    public decimal? InstalledHourMeter { get; set; }
    public TireInstallReason InstallReason { get; set; }
    public Guid? RotationId { get; set; }
    public string? Notes { get; set; }

    public DateTime? RemovedAt { get; set; }
    public int? RemovedOdometerKm { get; set; }
    public decimal? RemovedHourMeter { get; set; }
    public TireRemovalReason? RemovalReason { get; set; }
    public TireRemovalDestination? RemovalDestination { get; set; }
    public Guid? RemovedBy { get; set; }
    public Guid? RemovalRotationId { get; set; }
    public string? RemovalNotes { get; set; }
    /// <summary>Snapshot at removal: km run in this stint (0 for a spare; null when unknown).</summary>
    public int? DistanceKm { get; set; }

    public bool IsOpen => RemovedAt is null;
}

/// <summary>Header of an atomic rotation (seções 14/15). The moves are the stints it closed and opened.</summary>
public class TireRotation : AuditableEntity, ITenantScoped, IAuditable
{
    public const int ReasonMaxLength = 300;

    public Guid CompanyId { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid? ImplementId { get; set; }
    public Implement? Implement { get; set; }
    public DateTime PerformedAt { get; set; }
    public int? OdometerKm { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public int TireCount { get; set; }
}

/// <summary>Observation categories (seção 20) — recorded, never diagnosed.</summary>
public enum TireWearPattern
{
    Normal,
    CenterWear,
    ShoulderWear,
    OneSidedWear,
    IrregularWear,
    Cupping,
    Unknown,
}

public enum TireDamageType
{
    Cut,
    Crack,
    Bulge,
    Puncture,
    SidewallDamage,
    BeadDamage,
    Other,
}

/// <summary>Overall visual condition informed by whoever inspected.</summary>
public enum TireCondition
{
    Good,
    /// <summary>Usable, but needs follow-up.</summary>
    Attention,
    /// <summary>Not fit for use until checked/repaired.</summary>
    Unfit,
}

public enum TirePressureUnit
{
    Psi,
    Bar,
    Kpa,
}

/// <summary>Reading compared with the axle's reference pressure (snapshot at the inspection).</summary>
public enum TirePressureCheck
{
    WithinRange,
    Low,
    High,
    /// <summary>No reference pressure configured for the position.</summary>
    NotEvaluated,
}

public enum TireInspectionSource
{
    Inspection,
    /// <summary>Measured when the tire was removed.</summary>
    Removal,
    /// <summary>New tread measured when a retread came back.</summary>
    Retread,
    /// <summary>Tread informed when a used tire was registered (fleet migration).</summary>
    Registration,
}

/// <summary>
/// An inspection/measurement (seções 16–21). Append-only: the tread history is the list of these, never an overwritten
/// field. Position and km are snapshots of where the tire was at that moment.
/// </summary>
public class TireInspection : AuditableEntity, ITenantScoped, IAuditable
{
    public const int NotesMaxLength = 2000;

    public Guid CompanyId { get; set; }
    public Guid TireId { get; set; }
    public Tire Tire { get; set; } = null!;
    public Guid? InstallationId { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid? ImplementId { get; set; }
    public Implement? Implement { get; set; }
    public string? PositionCode { get; set; }
    public string? PositionLabel { get; set; }

    public DateTime InspectedAt { get; set; }
    public TireInspectionSource Source { get; set; } = TireInspectionSource.Inspection;
    public int? OdometerKm { get; set; }
    /// <summary>Tire km at the moment (for wear per 1,000 km). Null when unknown.</summary>
    public int? TireKm { get; set; }
    public decimal? TreadDepthMm { get; set; }
    public decimal? Pressure { get; set; }
    public TirePressureUnit? PressureUnit { get; set; }
    public TirePressureCheck? PressureCheck { get; set; }
    public TireCondition Condition { get; set; } = TireCondition.Good;
    public TireWearPattern WearPattern { get; set; } = TireWearPattern.Normal;
    public List<TireInspectionDamage> Damages { get; set; } = [];
    public string? Notes { get; set; }
    /// <summary>The occurrence (e.g. failed checklist item "pneu") this inspection follows up, when started from it.</summary>
    public Guid? OccurrenceId { get; set; }
    /// <summary>Maintenance request opened from this inspection (configurable rule or button).</summary>
    public Guid? MaintenanceRequestId { get; set; }

    /// <summary>Something to act on: condition not good, damage or abnormal wear (seção 34).</summary>
    public bool RequiresAction => Condition != TireCondition.Good || Damages.Count > 0 ||
                                  WearPattern is not (TireWearPattern.Normal or TireWearPattern.Unknown);
}

public class TireInspectionDamage : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid InspectionId { get; set; }
    public TireDamageType Type { get; set; }
}

public enum TireServiceKind
{
    Repair,
    Retread,
}

public enum TireServiceStatus
{
    /// <summary>Sent to the provider.</summary>
    Open,
    Completed,
    Cancelled,
}

public enum TireServiceResult
{
    /// <summary>Done — the tire is back in stock.</summary>
    Approved,
    /// <summary>The provider refused it (e.g. carcass not fit for retread) — the tire waits for a decision.</summary>
    Rejected,
}

public enum TireRepairType
{
    Puncture,
    Vulcanization,
    BeadRepair,
    SidewallRepair,
    Other,
}

/// <summary>
/// A repair or a retread (seções 22/23): one lifecycle Open → Completed (approved/rejected) | Cancelled. Eligibility for
/// retread is the provider's/manager's decision, recorded in the result — never decided by the system. A repair on an
/// installed tire (puncture fixed in place) is recorded already completed (<see cref="InPlace"/>).
/// </summary>
public class TireServiceOrder : AuditableEntity, ITenantScoped, IAuditable
{
    public const int TextMaxLength = 1000;
    public const int ProviderMaxLength = 120;
    public const int TreadPatternMaxLength = 80;

    public Guid CompanyId { get; set; }
    public Guid TireId { get; set; }
    public Tire Tire { get; set; } = null!;
    public TireServiceKind Kind { get; set; }
    public TireServiceStatus Status { get; set; } = TireServiceStatus.Open;
    public TireServiceResult? Result { get; set; }
    public bool InPlace { get; set; }

    /// <summary>Provider from the workshop registry (Phase 3), or a free name when it is not registered.</summary>
    public Guid? WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }
    public string? ProviderName { get; set; }

    public DateTime SentAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TireRepairType? RepairType { get; set; }
    /// <summary>Which retread this is (1 = first) — the tire's retread count + 1 when sent.</summary>
    public int? RetreadNumber { get; set; }
    /// <summary>New tread (banda) applied by the retreader.</summary>
    public string? TreadPattern { get; set; }
    public decimal? NewTreadDepthMm { get; set; }
    public decimal? Cost { get; set; }
    public DateOnly? WarrantyUntil { get; set; }
    public string? Description { get; set; }
    public string? ResultNotes { get; set; }
    public string? CancellationReason { get; set; }
}

public enum TireCostType
{
    Repair,
    Retread,
    /// <summary>Mounting, balancing, alignment charged to the tire.</summary>
    Installation,
    Other,
}

/// <summary>
/// Operational cost of a tire (seção 24) — not accounting. The purchase price stays on the tire; repairs and retreads
/// produce a row when their service order is completed; the rest are manual entries.
/// </summary>
public class TireCost : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int DescriptionMaxLength = 300;
    public const decimal MaxAmount = 1_000_000m;

    public Guid CompanyId { get; set; }
    public Guid TireId { get; set; }
    public TireCostType Type { get; set; }
    public DateOnly IncurredOn { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public Guid? ServiceOrderId { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Patterns that ask for a review (seção 35). They never state a cause.</summary>
public enum TireAnomalyType
{
    RapidTreadLoss,
    RepeatedRepairs,
    RepeatedPunctures,
    ShortLifecycle,
    RecurringPositionDamage,
}

public class TireAnomaly : ITenantScoped
{
    public const int MessageMaxLength = 400;

    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid TireId { get; set; }
    public TireAnomalyType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }
}

/// <summary>
/// Company policy (seção 17: configurable, never presented as a legal requirement). A company without a row uses
/// <see cref="Defaults"/>; thresholds apply to the next evaluations and to the date-based alerts immediately.
/// </summary>
public class TireSettings : AuditableEntity, ITenantScoped, IAuditable
{
    public Guid CompanyId { get; set; }
    /// <summary>Replacement point by company policy (mm).</summary>
    public decimal MinTreadDepthMm { get; set; } = 3m;
    /// <summary>"Approaching the minimum" from this depth (mm).</summary>
    public decimal TreadWarningDepthMm { get; set; } = 4m;
    /// <summary>Installed tires should be inspected at least every N days.</summary>
    public int InspectionIntervalDays { get; set; } = 30;
    /// <summary>Age (from manufacturing) after which the tire is flagged for review. 0 disables.</summary>
    public int MaxAgeYears { get; set; } = 5;
    public int PressureTolerancePercent { get; set; } = 10;
    public TirePressureUnit PressureUnit { get; set; } = TirePressureUnit.Psi;
    /// <summary>Tread loss faster than this (mm per 1,000 km) between two measurements is flagged. 0 disables.</summary>
    public decimal RapidWearMmPer1000Km { get; set; } = 0.5m;
    /// <summary>A tire disposed before running this many km is flagged as a short lifecycle. 0 disables.</summary>
    public int MinExpectedLifeKm { get; set; }
    /// <summary>Configurable rule (seções 36/62): an "unfit" inspection of a vehicle tire opens a maintenance request.</summary>
    public bool AutoMaintenanceRequestOnUnfit { get; set; }

    public static TireSettings Defaults() => new();
}
