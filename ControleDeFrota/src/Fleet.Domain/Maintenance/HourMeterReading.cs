using Fleet.Domain.Common;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Maintenance;

public enum HourMeterReadingSource
{
    /// <summary>Value informed when the vehicle was registered.</summary>
    Registration,
    Manual,
    WorkOrder,
    /// <summary>Audited correction by a manager; may be lower than the previous reading.</summary>
    Correction,
}

public enum HourMeterReadingStatus
{
    Valid,
    /// <summary>Suspicious jump: kept in history, NOT applied until a manager approves it.</summary>
    PendingReview,
    Rejected,
}

/// <summary>
/// Engine-hour history, mirroring OdometerReading/ADR-019 exactly: readings are never edited or deleted;
/// mistakes are rejected or corrected. Feeds hour-based preventive maintenance (seção 6).
/// </summary>
public class HourMeterReading : AuditableEntity, ITenantScoped, IAuditable
{
    public const int NotesMaxLength = 500;
    public const int AnomalyMaxLength = 300;

    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public decimal Hours { get; set; }
    public DateTime ReadAt { get; set; }
    public HourMeterReadingSource Source { get; set; }
    public HourMeterReadingStatus Status { get; set; } = HourMeterReadingStatus.Valid;

    public string? Anomaly { get; set; }
    public string? Notes { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }
}

public sealed record HourMeterBaseline(decimal Hours, DateTime ReadAt);

public enum HourMeterCheck
{
    Valid,
    Decrease,
    SuspiciousJump,
}

public sealed record HourMeterEvaluation(HourMeterCheck Check, string? Anomaly);

/// <summary>Validation of a new hour-meter reading against the baseline — same shape as OdometerPolicy.</summary>
public static class HourMeterPolicy
{
    public const decimal MaxHours = 999_999m;
    /// <summary>An engine rarely runs more than ~20h/day even with shifts; leaves room for real use while catching typos.</summary>
    public const decimal MaxPlausibleHoursPerDay = 20m;

    public static HourMeterEvaluation Evaluate(decimal hours, DateTime readAt, HourMeterBaseline? baseline)
    {
        if (baseline is null) return new(HourMeterCheck.Valid, null);
        if (hours < baseline.Hours) return new(HourMeterCheck.Decrease, null);

        var delta = hours - baseline.Hours;
        var days = Math.Max(1d, (readAt - baseline.ReadAt).TotalDays);
        var perDay = delta / (decimal)days;
        if (perDay <= MaxPlausibleHoursPerDay) return new(HourMeterCheck.Valid, null);

        var elapsed = days < 1.5 ? "1 dia" : $"{BrazilianFormat.Number(Math.Round(days))} dias";
        return new(HourMeterCheck.SuspiciousJump,
            $"Aumento de {BrazilianFormat.Number(delta, 1)} h em {elapsed} (média de {BrazilianFormat.Number(perDay, 1)} h/dia; " +
            $"o limite plausível é {BrazilianFormat.Number(MaxPlausibleHoursPerDay, 1)} h/dia).");
    }
}
