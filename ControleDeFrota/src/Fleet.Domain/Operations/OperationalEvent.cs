using Fleet.Domain.Common;

namespace Fleet.Domain.Operations;

/// <summary>
/// Catalog of operational events (ADR-025). New modules add values here; consumers (history, future notifications)
/// switch on the type, never on the summary text.
/// </summary>
public enum OperationalEventType
{
    VehicleAssigned,
    VehicleAssignmentEnded,
    VehicleStatusChanged,
    MileageRecorded,
    MileageAnomalyDetected,
    MileageCorrected,
    MileageReviewed,
    DocumentCreated,
    DocumentRenewed,
    DocumentDeleted,
    DocumentExpiring,
    DocumentExpired,
    ChecklistCompleted,
    ChecklistFailed,
    OccurrenceCreated,
    OccurrenceStatusChanged,
}

/// <summary>
/// Something that happened in the operation, written in the same transaction as the change that caused it.
/// It is both the vehicle/driver timeline (operational history) and an outbox: future notification channels
/// read rows with <see cref="PublishedAt"/> = null, deliver them and stamp the column. Append-only.
/// </summary>
public class OperationalEvent : ITenantScoped
{
    public const int SummaryMaxLength = 300;
    public const int SubjectTypeMaxLength = 50;

    public long Id { get; set; }
    public Guid CompanyId { get; set; }
    public OperationalEventType Type { get; set; }
    public DateTime OccurredAt { get; set; }
    /// <summary>Null = system (e.g. the document expiration scanner).</summary>
    public Guid? UserId { get; set; }

    // What the event is about — the timeline of each of these shows it.
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? ImplementId { get; set; }

    /// <summary>Record that caused the event ("VehicleAssignment", "Occurrence"…) — for drill-down.</summary>
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }

    /// <summary>Human sentence in pt-BR, frozen at the time of the event (history must not change later).</summary>
    public string Summary { get; set; } = string.Empty;
    /// <summary>Structured payload (JSON) for consumers; keep it free of personal data beyond ids.</summary>
    public string Data { get; set; } = "{}";

    /// <summary>Outbox marker for future notification dispatchers.</summary>
    public DateTime? PublishedAt { get; set; }
}
