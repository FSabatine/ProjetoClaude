using Fleet.Domain.Common;
using Fleet.Domain.Drivers;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Fuel;

/// <summary>Stored status (ADR-032). "Corrected" is not a status: corrections are history rows on a still valid record.</summary>
public enum FuelingStatus
{
    Valid,
    /// <summary>Has at least one anomaly nobody reviewed yet. Counts in costs (the money was spent) — see DOMAIN.md.</summary>
    PendingReview,
    /// <summary>Final. Wrong record kept for history; ignored by every total and by consumption.</summary>
    Cancelled,
}

/// <summary>Operational information only — no financial reconciliation in this phase.</summary>
public enum PaymentMethod
{
    Cash,
    Pix,
    DebitCard,
    CreditCard,
    FuelCard,
    /// <summary>Billed to the company (faturado) by the station.</summary>
    Invoice,
    /// <summary>Taken from the company's own tank.</summary>
    InternalTank,
    Other,
}

/// <summary>Where the record came from. Future card/provider integrations add values here (ADR-031).</summary>
public enum FuelingSource
{
    Manual,
}

/// <summary>Why a fueling has (or has not) a consumption figure — shown to the user, so the absence is explained.</summary>
public enum ConsumptionResult
{
    /// <summary>Partial fill: its quantity is added to the next full-tank segment.</summary>
    PartialFill,
    /// <summary>First full tank of the vehicle: it starts the first segment.</summary>
    FirstFullTank,
    Calculated,
    /// <summary>The segment exists but cannot be trusted (odometer correction, mileage under review, mixed units…).</summary>
    NotReliable,
}

public enum ConsumptionBaselineSource
{
    /// <summary>Vehicle.ExpectedConsumption, set by the manager.</summary>
    Configured,
    /// <summary>Weighted average of the vehicle's own previous segments.</summary>
    VehicleHistory,
    /// <summary>Weighted average of the company's vehicles of the same type and fuel unit.</summary>
    VehicleTypeAverage,
}

public enum FuelAnomalyType
{
    ExcessiveQuantity,
    AbnormalPrice,
    MileageJump,
    HighFrequency,
    FuelTypeMismatch,
    LowConsumption,
    HighConsumption,
}

/// <summary>
/// The central fueling record (ADR-032). Quantity, price and total are a snapshot of what was paid; consumption
/// fields are the result of the segment this full-tank fueling closes, with the baseline used at that time — so
/// history does not move when settings or prices change later. Never deleted: wrong records are cancelled.
/// </summary>
public class Fueling : AuditableEntity, ITenantScoped, IAuditable
{
    public const int ReceiptMaxLength = 60;
    public const int NotesMaxLength = 1000;
    public const int ReasonMaxLength = 1000;

    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }
    public Guid? FuelStationId { get; set; }
    public FuelStation? FuelStation { get; set; }
    public Guid FuelTypeId { get; set; }
    public FuelType FuelType { get; set; } = null!;

    public DateTime FueledAt { get; set; }
    /// <summary>Business date (Brazil) of <see cref="FueledAt"/> — period filters and monthly grouping use it.</summary>
    public DateOnly FueledOn { get; set; }
    public int OdometerKm { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    /// <summary>Always calculated by the server (<see cref="FuelingAmounts.Total"/>).</summary>
    public decimal TotalAmount { get; set; }
    /// <summary>Full tank closes a consumption segment (tank-to-tank method); partial fills add to the next one.</summary>
    public bool IsFullTank { get; set; } = true;
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public FuelingSource Source { get; set; } = FuelingSource.Manual;

    public FuelingStatus Status { get; set; } = FuelingStatus.Valid;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    // Consumption of the segment closed by this fueling (only when IsFullTank).
    public ConsumptionResult ConsumptionResult { get; set; } = ConsumptionResult.PartialFill;
    public int? SegmentDistanceKm { get; set; }
    public decimal? SegmentQuantity { get; set; }
    public decimal? SegmentCost { get; set; }
    /// <summary>km per unit of the segment.</summary>
    public decimal? Consumption { get; set; }
    /// <summary>Baseline used when the segment was calculated (snapshot).</summary>
    public decimal? ExpectedConsumption { get; set; }
    /// <summary>Fuel the segment would have used at the baseline (distance / expected) — summed by reports for "expected" of a period.</summary>
    public decimal? SegmentExpectedQuantity { get; set; }
    public ConsumptionBaselineSource? BaselineSource { get; set; }
    /// <summary>(actual − expected) / expected × 100, rounded to one decimal.</summary>
    public decimal? ConsumptionDeviationPercent { get; set; }

    public List<FuelingAnomaly> Anomalies { get; set; } = [];
    public List<FuelingCorrection> Corrections { get; set; } = [];

    /// <summary>Status follows the anomalies: any unreviewed one means "requires review". Cancelled is final.</summary>
    public void RefreshStatus() =>
        Status = Status == FuelingStatus.Cancelled ? FuelingStatus.Cancelled
            : Anomalies.Any(a => a.ReviewedAt is null) ? FuelingStatus.PendingReview
            : FuelingStatus.Valid;
}

/// <summary>A warning about a fueling. Neutral by design: it asks for a review, it never accuses anyone.</summary>
public class FuelingAnomaly : ITenantScoped
{
    public const int MessageMaxLength = 400;

    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FuelingId { get; set; }
    public FuelAnomalyType Type { get; set; }
    /// <summary>pt-BR sentence with the facts (expected × actual).</summary>
    public string Message { get; set; } = string.Empty;
    public decimal? ExpectedValue { get; set; }
    public decimal? ActualValue { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
}

/// <summary>Append-only record of a correction: who, when, why and each field from → to (seção 14).</summary>
public class FuelingCorrection : ITenantScoped
{
    public const int ChangesMaxLength = 4000;

    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FuelingId { get; set; }
    public DateTime CorrectedAt { get; set; }
    public Guid? CorrectedBy { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>JSON array of { field, label, from, to } — values already formatted in pt-BR.</summary>
    public string Changes { get; set; } = "[]";
}
