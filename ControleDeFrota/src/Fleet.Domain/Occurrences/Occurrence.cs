using Fleet.Domain.Common;
using Fleet.Domain.Drivers;
using Fleet.Domain.Implements;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Occurrences;

public enum OccurrenceType
{
    MechanicalIssue,
    TireProblem,
    Accident,
    VehicleDamage,
    MissingEquipment,
    DocumentationProblem,
    DriverReport,
    GeneralObservation,
}

public enum OccurrenceSeverity
{
    Low,
    Medium,
    High,
    /// <summary>Prevents safe use of the vehicle.</summary>
    Critical,
}

public enum OccurrenceStatus
{
    Open,
    InAnalysis,
    Resolved,
    Cancelled,
}

public enum OccurrenceSource
{
    Manual,
    /// <summary>Created automatically from a failed checklist item.</summary>
    Checklist,
}

/// <summary>
/// A general operational occurrence (ADR-023): problems, damage, observations. It is the entry point the future
/// Maintenance and Claims modules will consume — no work orders here. Never deleted: a mistaken record is cancelled.
/// </summary>
public class Occurrence : AuditableEntity, ITenantScoped, IAuditable
{
    public const int DescriptionMaxLength = 2000;
    public const int LocationMaxLength = 200;
    public const int ResolutionMaxLength = 2000;

    public Guid CompanyId { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }
    public Guid? ImplementId { get; set; }
    public Implement? Implement { get; set; }

    public OccurrenceType Type { get; set; }
    public OccurrenceSeverity Severity { get; set; } = OccurrenceSeverity.Medium;
    public DateTime OccurredAt { get; set; }
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;

    public OccurrenceStatus Status { get; set; } = OccurrenceStatus.Open;
    /// <summary>How it was solved, or why it was cancelled (required for both).</summary>
    public string? Resolution { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }

    public OccurrenceSource Source { get; set; } = OccurrenceSource.Manual;
    public Guid? ChecklistExecutionId { get; set; }

    public bool IsClosed => OccurrenceWorkflow.IsClosed(Status);
}

/// <summary>
/// Explicit state machine. Open → InAnalysis → Resolved; any non-closed state → Resolved | Cancelled.
/// Resolved and Cancelled are final: a problem that comes back is a new occurrence (keeps the history honest).
/// </summary>
public static class OccurrenceWorkflow
{
    private static readonly Dictionary<OccurrenceStatus, OccurrenceStatus[]> Allowed = new()
    {
        [OccurrenceStatus.Open] = [OccurrenceStatus.InAnalysis, OccurrenceStatus.Resolved, OccurrenceStatus.Cancelled],
        [OccurrenceStatus.InAnalysis] = [OccurrenceStatus.Resolved, OccurrenceStatus.Cancelled],
        [OccurrenceStatus.Resolved] = [],
        [OccurrenceStatus.Cancelled] = [],
    };

    public static bool CanTransition(OccurrenceStatus from, OccurrenceStatus to) => Allowed[from].Contains(to);

    public static IReadOnlyList<OccurrenceStatus> NextStatuses(OccurrenceStatus from) => Allowed[from];

    public static bool IsClosed(OccurrenceStatus status) => status is OccurrenceStatus.Resolved or OccurrenceStatus.Cancelled;

    /// <summary>Closing requires a written resolution / cancellation reason.</summary>
    public static bool RequiresResolution(OccurrenceStatus to) => IsClosed(to);
}
