using Fleet.Domain.Common;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Finance;

/// <summary>
/// A planned amount for a period/category/dimension (ADR-040). Actual is never stored here — it is computed on
/// read by CostAggregationService over the same period/filters, the same "status is never stored" pattern used
/// throughout the project (DocumentExpiryPolicy, MaintenanceSchedulePolicy…).
/// </summary>
public class Budget : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NotesMaxLength = 500;

    public Guid CompanyId { get; set; }
    public int Year { get; set; }
    /// <summary>Null = annual budget for the whole year; 1–12 = a single month's budget.</summary>
    public int? Month { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public Guid? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
