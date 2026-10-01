using Fleet.Domain.Common;
using Fleet.Domain.Implements;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Maintenance;

public enum WorkOrderStatus
{
    Draft,
    Approved,
    Scheduled,
    InProgress,
    WaitingParts,
    Completed,
    Cancelled,
    Rejected,
}

public enum WorkOrderItemStatus
{
    Pending,
    Done,
    Skipped,
}

/// <summary>
/// The maintenance job itself (seção 16/17): planned (preventive, from a MaintenanceRequest, or ad-hoc), executed,
/// and closed with diagnosis, parts, labor and cost. <see cref="Number"/> is sequential per company for display.
/// </summary>
public class WorkOrder : AuditableEntity, ITenantScoped, IAuditable
{
    public const int DescriptionMaxLength = 2000;
    public const int DiagnosisMaxLength = 2000;
    public const int ResolutionMaxLength = 2000;
    public const int NotesMaxLength = 2000;

    public Guid CompanyId { get; set; }
    /// <summary>Sequential per company (e.g. 37 → "OS-000037"); formatting happens in the Application layer.</summary>
    public int Sequence { get; set; }

    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid? ImplementId { get; set; }
    public Implement? Implement { get; set; }
    public Guid? MaintenanceRequestId { get; set; }
    public MaintenanceRequest? MaintenanceRequest { get; set; }
    public Guid? WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }

    public MaintenanceType Type { get; set; } = MaintenanceType.Corrective;
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Draft;

    public DateTime OpenedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Snapshot taken at completion — never read back into the vehicle.</summary>
    public int? OdometerKm { get; set; }
    public decimal? HourMeter { get; set; }

    public string Description { get; set; } = string.Empty;
    public string? Diagnosis { get; set; }
    public string? Resolution { get; set; }
    public string? Notes { get; set; }
    public Guid? CompletedBy { get; set; }

    public decimal PartsCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OtherCost { get; set; }
    public decimal TotalCost { get; set; }
    public int? DowntimeMinutes { get; set; }

    public List<WorkOrderItem> Items { get; set; } = [];
    public List<WorkOrderPart> Parts { get; set; } = [];
    public List<WorkOrderLabor> Labor { get; set; } = [];

    public bool IsClosed => WorkOrderWorkflow.IsClosed(Status);
    public bool IsActive => !IsClosed;
    public string Number => $"OS-{Sequence:D6}";
}

/// <summary>A task on the order (seção 19); linked back to a plan item when it comes from preventive maintenance.</summary>
public class WorkOrderItem
{
    public const int DescriptionMaxLength = 300;
    public const int NotesMaxLength = 500;

    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? MaintenancePlanItemId { get; set; }
    public bool IsRequired { get; set; } = true;
    public WorkOrderItemStatus Status { get; set; } = WorkOrderItemStatus.Pending;
    public string? Notes { get; set; }
}

/// <summary>A part used (seção 21) — a cost line, not stock control (that module is out of scope for now).</summary>
public class WorkOrderPart
{
    public const int PartNameMaxLength = 150;
    public const int PartNumberMaxLength = 60;
    public const int SupplierMaxLength = 150;
    public const int NotesMaxLength = 500;

    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public string? PartNumber { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitCost { get; set; }
    public string? Supplier { get; set; }
    public string? Notes { get; set; }

    public decimal TotalCost => Quantity * UnitCost;
}

/// <summary>Labor performed (seção 22). No separate Mechanic entity yet — the technician is free text.</summary>
public class WorkOrderLabor
{
    public const int TechnicianNameMaxLength = 150;
    public const int DescriptionMaxLength = 500;

    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public string? Description { get; set; }

    public decimal TotalCost => Hours * HourlyRate;
}

/// <summary>
/// Explicit state machine (seção 17): Draft → Approved → Scheduled → InProgress ⇄ WaitingParts → Completed,
/// with Cancelled/Rejected as exits from any non-final state. Completed/Cancelled/Rejected are final.
/// </summary>
public static class WorkOrderWorkflow
{
    private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> Allowed = new()
    {
        [WorkOrderStatus.Draft] = [WorkOrderStatus.Approved, WorkOrderStatus.Cancelled, WorkOrderStatus.Rejected],
        [WorkOrderStatus.Approved] = [WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Scheduled] = [WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.InProgress] = [WorkOrderStatus.WaitingParts, WorkOrderStatus.Completed, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.WaitingParts] = [WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Completed] = [],
        [WorkOrderStatus.Cancelled] = [],
        [WorkOrderStatus.Rejected] = [],
    };

    public static bool CanTransition(WorkOrderStatus from, WorkOrderStatus to) => Allowed[from].Contains(to);

    public static IReadOnlyList<WorkOrderStatus> NextStatuses(WorkOrderStatus from) => Allowed[from];

    public static bool IsClosed(WorkOrderStatus status) =>
        status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled or WorkOrderStatus.Rejected;

    /// <summary>Completing requires the resolution text; cancelling/rejecting requires a reason (carried in Notes).</summary>
    public static bool RequiresResolution(WorkOrderStatus to) => to == WorkOrderStatus.Completed;

    public static bool RequiresReason(WorkOrderStatus to) => to is WorkOrderStatus.Cancelled or WorkOrderStatus.Rejected;

    /// <summary>The vehicle is physically being worked on — drives Vehicle.Status (ADR-028).</summary>
    public static bool OccupiesVehicle(WorkOrderStatus status) =>
        status is WorkOrderStatus.InProgress or WorkOrderStatus.WaitingParts;
}
