using Fleet.Domain.Common;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Maintenance;

public enum MaintenancePriority
{
    Low,
    Medium,
    High,
    /// <summary>Vehicle should not operate until evaluated.</summary>
    Critical,
}

/// <summary>
/// A configurable preventive maintenance plan (seção 5/8). <see cref="VehicleId"/> and <see cref="VehicleType"/>
/// are both null for the company's default plan. <see cref="MaintenancePlanResolver"/> is the only place that
/// decides which plan applies to a vehicle.
/// </summary>
public class MaintenancePlan : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 150;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Overrides the default/type plan for this one vehicle.</summary>
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    /// <summary>Overrides the default plan for every vehicle of this type.</summary>
    public VehicleType? VehicleType { get; set; }
    public bool IsActive { get; set; } = true;
    public List<MaintenancePlanItem> Items { get; set; } = [];

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>
/// One serviceable item of a plan (seção 7), e.g. "Troca de óleo a cada 20.000 km". At least one interval is
/// required; the first one reached triggers the maintenance (<see cref="MaintenanceSchedulePolicy"/>).
/// </summary>
public class MaintenancePlanItem
{
    public const int ServiceNameMaxLength = 150;
    public const int NotesMaxLength = 1000;

    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int? IntervalKm { get; set; }
    public int? IntervalMonths { get; set; }
    public decimal? IntervalHours { get; set; }
    /// <summary>Tolerance after the due point before it counts as Overdue, and the look-ahead window for DueSoon.</summary>
    public int? GraceKm { get; set; }
    public int? GraceDays { get; set; }
    public decimal? GraceHours { get; set; }
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;
    public int? EstimatedDurationMinutes { get; set; }
    public decimal? EstimatedCost { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>
/// Which plan applies to a vehicle (seção 8): the vehicle's own plan wins, then its type's plan,
/// then the company default. A pure function — no persistence here.
/// </summary>
public static class MaintenancePlanResolver
{
    public static MaintenancePlan? ResolveFor(Vehicle vehicle, IReadOnlyCollection<MaintenancePlan> activePlans)
    {
        var vehiclePlan = activePlans.FirstOrDefault(p => p.VehicleId == vehicle.Id);
        if (vehiclePlan is not null) return vehiclePlan;

        var typePlan = activePlans.FirstOrDefault(p => p.VehicleId is null && p.VehicleType == vehicle.Type);
        if (typePlan is not null) return typePlan;

        return activePlans.FirstOrDefault(p => p.VehicleId is null && p.VehicleType is null);
    }
}
