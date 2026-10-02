using Fleet.Domain.Common;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Mileage;

public enum OdometerReadingSource
{
    /// <summary>Value informed when the vehicle was registered.</summary>
    Registration,
    Manual,
    Checklist,
    /// <summary>Odometer informed in a fueling (Phase 4) — fueling is one source of readings, not a second mileage system.</summary>
    Fueling,
    /// <summary>Audited correction by a manager; may be lower than the previous reading.</summary>
    Correction,
}

public enum OdometerReadingStatus
{
    /// <summary>Accepted; the latest valid reading is the vehicle's current odometer.</summary>
    Valid,
    /// <summary>Suspicious jump: kept in history, NOT applied until a manager approves it (ADR-019).</summary>
    PendingReview,
    Rejected,
}

/// <summary>Odometer history (ADR-019). Readings are never edited or deleted; mistakes are rejected or corrected.</summary>
public class OdometerReading : AuditableEntity, ITenantScoped, IAuditable
{
    public const int NotesMaxLength = 500;
    public const int AnomalyMaxLength = 300;

    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public int OdometerKm { get; set; }
    public DateTime ReadAt { get; set; }
    public OdometerReadingSource Source { get; set; }
    public OdometerReadingStatus Status { get; set; } = OdometerReadingStatus.Valid;

    /// <summary>Why the reading was flagged (pt-BR, shown to the reviewer).</summary>
    public string? Anomaly { get; set; }
    /// <summary>Reason of a correction, or free notes.</summary>
    public string? Notes { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }

    public Guid? ChecklistExecutionId { get; set; }
    /// <summary>Fueling that produced this reading (Source = Fueling, or a Correction made from the fueling).</summary>
    public Guid? FuelingId { get; set; }
}

/// <summary>The previous valid reading a new one is compared with.</summary>
public sealed record OdometerBaseline(int OdometerKm, DateTime ReadAt);

public enum OdometerCheck
{
    Valid,
    Decrease,
    SuspiciousJump,
}

public sealed record OdometerEvaluation(OdometerCheck Check, string? Anomaly);

/// <summary>Validation of a new reading against the baseline — the single place for the mileage rules.</summary>
public static class OdometerPolicy
{
    public const int MaxOdometerKm = 9_999_999;
    /// <summary>
    /// Above this average a jump is suspicious. A truck with two drivers rarely exceeds ~1,200 km/day;
    /// 1,500 leaves room for real operations while catching typos (an extra digit is a 10x jump).
    /// </summary>
    public const int MaxPlausibleKmPerDay = 1_500;
    /// <summary>An active vehicle without a reading for this long shows up as "no recent mileage".</summary>
    public const int StaleAfterDays = 7;

    public static OdometerEvaluation Evaluate(int odometerKm, DateTime readAt, OdometerBaseline? baseline)
    {
        if (baseline is null) return new(OdometerCheck.Valid, null);
        if (odometerKm < baseline.OdometerKm) return new(OdometerCheck.Decrease, null);

        var delta = odometerKm - baseline.OdometerKm;
        // Less than a day still allows one day's worth of driving.
        var days = Math.Max(1d, (readAt - baseline.ReadAt).TotalDays);
        var perDay = delta / days;
        if (perDay <= MaxPlausibleKmPerDay) return new(OdometerCheck.Valid, null);

        var elapsed = days < 1.5 ? "1 dia" : $"{BrazilianFormat.Number(Math.Round(days))} dias";
        return new(OdometerCheck.SuspiciousJump,
            $"Aumento de {BrazilianFormat.Number(delta)} km em {elapsed} (média de {BrazilianFormat.Number(perDay)} km/dia; " +
            $"o limite plausível é {BrazilianFormat.Number(MaxPlausibleKmPerDay)} km/dia).");
    }
}
