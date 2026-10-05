using Fleet.Domain.Common;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Finance;

public enum ExpenseFrequency
{
    Monthly,
    Quarterly,
    Semiannual,
    Annual,
}

/// <summary>
/// Template for a recurring operational obligation (insurance, financing, leasing, subscriptions…). The generation
/// job (ADR-040) turns it into dated <see cref="Expense"/> rows ahead of time, the same idea as MaintenanceSchedule/
/// DocumentExpirationScanner: a value gets written once a fact happens (here, once a due date is reached), not
/// speculatively for all future time.
/// </summary>
public class RecurringExpense : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int DescriptionMaxLength = 200;
    public const int SupplierNameMaxLength = 150;

    public Guid CompanyId { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public Guid? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid? WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }
    public string? SupplierName { get; set; }

    public decimal Amount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public ExpenseFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    /// <summary>1–28, to avoid month-length edge cases (ADR-040).</summary>
    public int DueDayOfMonth { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    /// <summary>Due date of the last Expense this template generated — the cursor the generation job advances from.</summary>
    public DateOnly? LastGeneratedDueDate { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
