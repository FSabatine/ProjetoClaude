using Fleet.Domain.Common;
using Fleet.Domain.Drivers;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Maintenance;

public enum MaintenanceType
{
    Preventive,
    Corrective,
    Inspection,
}

public enum MaintenanceRequestSource
{
    Driver,
    Checklist,
    FleetManager,
    Occurrence,
    AutomaticAlert,
}

public enum MaintenanceRequestStatus
{
    Open,
    /// <summary>Approved: a WorkOrder was created from it in the same transaction.</summary>
    Converted,
    Rejected,
}

/// <summary>
/// The "someone needs maintenance done" step before a WorkOrder exists (seção 12/13). Not every occurrence or
/// checklist failure becomes one — opening it is always a deliberate action by whoever reports or reviews.
/// </summary>
public class MaintenanceRequest : AuditableEntity, ITenantScoped, IAuditable
{
    public const int DescriptionMaxLength = 2000;
    public const int RejectionReasonMaxLength = 1000;

    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public MaintenanceRequestSource Source { get; set; } = MaintenanceRequestSource.FleetManager;
    public MaintenanceType MaintenanceType { get; set; } = MaintenanceType.Corrective;
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;
    public string Description { get; set; } = string.Empty;
    public DateTime ReportedAt { get; set; }
    public int? OdometerKm { get; set; }
    public decimal? HourMeter { get; set; }
    public Guid? OccurrenceId { get; set; }
    public Occurrence? Occurrence { get; set; }

    public MaintenanceRequestStatus Status { get; set; } = MaintenanceRequestStatus.Open;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? WorkOrderId { get; set; }

    public bool IsClosed => MaintenanceRequestWorkflow.IsClosed(Status);
}

/// <summary>Open → Converted | Rejected. Both final: a rejected request that still matters is reopened as a new one.</summary>
public static class MaintenanceRequestWorkflow
{
    public static bool IsClosed(MaintenanceRequestStatus status) =>
        status is MaintenanceRequestStatus.Converted or MaintenanceRequestStatus.Rejected;
}
