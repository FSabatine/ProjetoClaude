using Fleet.Domain.Common;
using Fleet.Domain.Drivers;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Finance;

/// <summary>Distinct from Fuel's PaymentMethod (ADR-031): this one is general-purpose, not pump-specific.</summary>
public enum PaymentMethod
{
    BankTransfer,
    Pix,
    CreditCard,
    DebitCard,
    Cash,
    FleetCard,
    DirectDebit,
    Invoice,
    Other,
}

/// <summary>Never stored — always computed by <see cref="ExpensePaymentPolicy"/> from the dates and paid amount.</summary>
public enum PaymentStatus
{
    Pending,
    Scheduled,
    PartiallyPaid,
    Paid,
    Overdue,
    Cancelled,
}

/// <summary>
/// A manual or recurring-generated operational expense (ADR-040). Fuel/Maintenance/Tire costs are NEVER entered
/// here — they are read straight from their own tables by CostAggregationService. This is the ledger for
/// everything else: insurance, taxes, tolls, rentals, fines, washing…
/// </summary>
public class Expense : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int DescriptionMaxLength = 200;
    public const int ReferenceNumberMaxLength = 60;
    public const int SupplierNameMaxLength = 150;
    public const int NotesMaxLength = 1000;
    public const int ReasonMaxLength = 500;
    public const decimal MaxAmount = 10_000_000m;

    public Guid CompanyId { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public Guid? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }
    /// <summary>Optional: reuses the maintenance Workshop catalog when the supplier is already registered there.</summary>
    public Guid? WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }
    /// <summary>Free text when the supplier is not a registered workshop (no generic Supplier catalog, ADR-040).</summary>
    public string? SupplierName { get; set; }

    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    public decimal PaidAmount { get; set; }
    public DateOnly? PaymentDate { get; set; }

    public bool IsRecurring { get; set; }
    public Guid? RecurringExpenseId { get; set; }
    public RecurringExpense? RecurringExpenseTemplate { get; set; }

    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
