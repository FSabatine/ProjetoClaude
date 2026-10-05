using Fleet.Domain.Common;

namespace Fleet.Domain.Intelligence;

public enum FleetAlertSeverity
{
    Info,
    Warning,
    Critical,
}

public enum FleetAlertStatus
{
    New,
    Read,
    InProgress,
    Resolved,
    Dismissed,
}

public enum FleetAlertCategory
{
    Maintenance,
    Fuel,
    Tires,
    Finance,
    Documents,
    Operations,
}

/// <summary>
/// Who may see an alert (ADR-045). Stored on the alert so the list filters in SQL; the permissions behind each audience
/// live in <see cref="AlertAudiences"/>. An alert that quotes R$ always uses <see cref="FleetCosts"/> (ADR-042: the AND of
/// every *.viewcosts involved).
/// </summary>
public enum AlertAudience
{
    Maintenance,
    Fuel,
    Tires,
    FinanceView,
    FleetCosts,
    Documents,
    DriverDocuments,
    Occurrences,
    Checklists,
    Mileage,
    Vehicles,
}

/// <summary>
/// A finding that requires attention, produced by an automation rule (ADR-045). Never deleted: it is resolved
/// (manually or automatically when the condition stops) or dismissed. One open alert per (rule, dedup key) — enforced
/// by a filtered unique index, so two scans running at once cannot duplicate it.
/// Not IAuditable on purpose: every scan refreshes open alerts (LastDetectedAt) and would flood AuditLogs; who read,
/// took and closed it is kept in the alert's own fields (ReadBy, AssignedToUserId, ClosedBy).
/// </summary>
public class FleetAlert : AuditableEntity, ITenantScoped
{
    public const int TitleMaxLength = 150;
    public const int TextMaxLength = 1000;
    public const int ActionMaxLength = 500;
    public const int DedupKeyMaxLength = 200;
    public const int EntityTypeMaxLength = 50;
    public const int TabMaxLength = 30;
    public const int NotesMaxLength = 500;

    public Guid CompanyId { get; set; }
    public Guid? AutomationRuleId { get; set; }
    public AutomationRule? AutomationRule { get; set; }
    public AutomationTrigger Trigger { get; set; }
    public FleetAlertCategory Category { get; set; }
    public AlertAudience Audience { get; set; }
    public FleetAlertSeverity Severity { get; set; }
    public FleetAlertStatus Status { get; set; } = FleetAlertStatus.New;
    /// <summary>Higher first. See <see cref="AlertPriority"/>.</summary>
    public int Priority { get; set; }

    public string Title { get; set; } = string.Empty;
    /// <summary>What happened and why the system believes it (pt-BR, neutral wording).</summary>
    public string Explanation { get; set; } = string.Empty;
    /// <summary>The numbers behind the conclusion, so the user can check it.</summary>
    public string Evidence { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;

    /// <summary>Record the alert leads to (Vehicle, Tire, Expense, Budget, Document, Occurrence…).</summary>
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid? VehicleId { get; set; }
    /// <summary>Section of the target page (e.g. "maintenance"), when relevant.</summary>
    public string? Tab { get; set; }
    public string DedupKey { get; set; } = string.Empty;

    public DateTime DetectedAt { get; set; }
    public DateTime LastDetectedAt { get; set; }
    /// <summary>How many scans confirmed the condition while the alert was open.</summary>
    public int DetectionCount { get; set; } = 1;
    /// <summary>How many times the same condition was resolved before and came back (feeds the priority).</summary>
    public int RecurrenceCount { get; set; }

    public DateTime? ReadAt { get; set; }
    public Guid? ReadBy { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public string? ClosingNotes { get; set; }
    /// <summary>True when the scan closed it because the condition no longer holds.</summary>
    public bool AutoResolved { get; set; }
}

/// <summary>Status transitions of an alert (same shape as OccurrenceWorkflow).</summary>
public static class FleetAlertWorkflow
{
    public static readonly FleetAlertStatus[] OpenStatuses = [FleetAlertStatus.New, FleetAlertStatus.Read, FleetAlertStatus.InProgress];

    public static bool IsOpen(FleetAlertStatus status) => status is FleetAlertStatus.New or FleetAlertStatus.Read or FleetAlertStatus.InProgress;

    public static IReadOnlyList<FleetAlertStatus> NextStatuses(FleetAlertStatus current) => current switch
    {
        FleetAlertStatus.New => [FleetAlertStatus.Read, FleetAlertStatus.InProgress, FleetAlertStatus.Resolved, FleetAlertStatus.Dismissed],
        FleetAlertStatus.Read => [FleetAlertStatus.InProgress, FleetAlertStatus.Resolved, FleetAlertStatus.Dismissed],
        FleetAlertStatus.InProgress => [FleetAlertStatus.Resolved, FleetAlertStatus.Dismissed],
        _ => [],
    };

    public static bool CanTransition(FleetAlertStatus from, FleetAlertStatus to) => NextStatuses(from).Contains(to);

    /// <summary>Dismissing hides a finding the system still sees — the reason must be recorded.</summary>
    public static bool RequiresNotes(FleetAlertStatus to) => to == FleetAlertStatus.Dismissed;
}

/// <summary>
/// Ordering of alerts (spec §10): severity first, then impact, urgency and recurrence. A simple, explainable score —
/// not a statistical model. Detectors give impact/urgency on a 0–30 scale.
/// </summary>
public static class AlertPriority
{
    public const int MaxFactor = 30;

    public static int Compute(FleetAlertSeverity severity, int impact, int urgency, int recurrenceCount)
    {
        var baseScore = severity switch
        {
            FleetAlertSeverity.Critical => 100,
            FleetAlertSeverity.Warning => 50,
            _ => 10,
        };
        return baseScore + Clamp(impact) + Clamp(urgency) + Math.Min(recurrenceCount * 10, MaxFactor);
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, MaxFactor);
}

/// <summary>Personal inbox entry (the bell). Messages never carry R$ values — the alert behind it does, behind its audience.</summary>
public class UserNotification : ITenantScoped
{
    public const int TitleMaxLength = 150;
    public const int MessageMaxLength = 500;
    public const int LinkMaxLength = 300;

    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public Guid? FleetAlertId { get; set; }
    public FleetAlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>Route inside the app (never an external URL).</summary>
    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
