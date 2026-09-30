using Fleet.Domain.Common;
using Fleet.Domain.Drivers;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Checklists;

/// <summary>How often a checklist is expected. Drives the "pending checklists" indicator.</summary>
public enum ChecklistFrequency
{
    OnDemand,
    Daily,
    Weekly,
}

public enum ChecklistResponseType
{
    /// <summary>Conforme / Não conforme / Não se aplica.</summary>
    PassFail,
    Number,
    Text,
}

public enum ChecklistChoice
{
    Pass,
    Fail,
    NotApplicable,
}

public enum ChecklistResult
{
    Approved,
    /// <summary>At least one item failed (each failure opened an occurrence).</summary>
    Failed,
}

/// <summary>
/// Reusable, configurable inspection model (ADR-024). Editing the items bumps <see cref="Version"/>; executions keep
/// a snapshot of the items, so changing a template never rewrites the past.
/// </summary>
public class ChecklistTemplate : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int MaxItems = 100;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ChecklistFrequency Frequency { get; set; } = ChecklistFrequency.OnDemand;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public List<ChecklistTemplateItem> Items { get; set; } = [];

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Part of the template aggregate — its changes are audited through the template's Version.</summary>
public class ChecklistTemplateItem
{
    public const int SectionMaxLength = 60;
    public const int LabelMaxLength = 200;
    public const int UnitMaxLength = 20;

    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int Position { get; set; }
    /// <summary>Visual group ("Pneus", "Iluminação") — optional.</summary>
    public string? Section { get; set; }
    public string Label { get; set; } = string.Empty;
    public ChecklistResponseType ResponseType { get; set; } = ChecklistResponseType.PassFail;
    public bool IsRequired { get; set; } = true;
    public string? Unit { get; set; }
    /// <summary>A "Não conforme" answer must carry a photo.</summary>
    public bool RequiresPhotoOnFail { get; set; }
    /// <summary>Occurrence opened when the item fails (e.g. a tire item → TireProblem).</summary>
    public OccurrenceType FailureOccurrenceType { get; set; } = OccurrenceType.MechanicalIssue;
    public OccurrenceSeverity FailureSeverity { get; set; } = OccurrenceSeverity.Medium;
}

/// <summary>An inspection performed on a vehicle. Immutable after submission.</summary>
public class ChecklistExecution : AuditableEntity, ITenantScoped, IAuditable
{
    public const int NotesMaxLength = 1000;
    public const int LocationMaxLength = 200;

    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    /// <summary>Driver inspected with (optional; defaults to the vehicle's current driver).</summary>
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public Guid TemplateId { get; set; }
    // Snapshot: the execution stays readable even if the template is renamed, edited or deleted.
    public string TemplateName { get; set; } = string.Empty;
    public int TemplateVersion { get; set; }
    public ChecklistFrequency Frequency { get; set; }

    public DateTime PerformedAt { get; set; }
    /// <summary>Business date (America/Sao_Paulo) of PerformedAt — "done today?" without timezone math in queries.</summary>
    public DateOnly PerformedOn { get; set; }
    public int? OdometerKm { get; set; }
    public ChecklistResult Result { get; set; }
    public int FailedItems { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }

    public List<ChecklistAnswer> Answers { get; set; } = [];
}

/// <summary>One answered item, with the question copied from the template at execution time.</summary>
public class ChecklistAnswer
{
    public const int TextMaxLength = 500;
    public const int CommentMaxLength = 500;

    public Guid Id { get; set; }
    public Guid ExecutionId { get; set; }
    public Guid TemplateItemId { get; set; }

    // Snapshot of the template item.
    public int Position { get; set; }
    public string? Section { get; set; }
    public string Label { get; set; } = string.Empty;
    public ChecklistResponseType ResponseType { get; set; }
    public bool IsRequired { get; set; }
    public string? Unit { get; set; }

    public ChecklistChoice? Choice { get; set; }
    public decimal? NumberValue { get; set; }
    public string? TextValue { get; set; }
    public string? Comment { get; set; }
    /// <summary>Severity of a failed item (defaults to the template item's).</summary>
    public OccurrenceSeverity? Severity { get; set; }
    public Guid? OccurrenceId { get; set; }

    public bool IsFailure => ResponseType == ChecklistResponseType.PassFail && Choice == ChecklistChoice.Fail;

    public bool IsAnswered => ResponseType switch
    {
        ChecklistResponseType.PassFail => Choice is not null,
        ChecklistResponseType.Number => NumberValue is not null,
        _ => !string.IsNullOrWhiteSpace(TextValue),
    };
}

/// <summary>Which checklist a vehicle still owes today/this week.</summary>
public static class ChecklistSchedule
{
    /// <summary>Weeks start on Monday (Brazilian business convention).</summary>
    public static DateOnly PeriodStart(ChecklistFrequency frequency, DateOnly today) => frequency switch
    {
        ChecklistFrequency.Weekly => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
        _ => today,
    };

    /// <summary>
    /// A vehicle owes the recurring checklists while it is in operation: it has a driver and its condition allows use.
    /// Pool vehicles without a driver are not expected to be inspected every day (no trips module yet to tell usage).
    /// </summary>
    public static bool IsExpectedFor(VehicleStatus status, bool hasActiveAssignment) =>
        hasActiveAssignment && status is VehicleStatus.Available or VehicleStatus.OnTrip;
}
