using Fleet.Domain.Companies;
using Fleet.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

// Phase 6 — finance. Indexes are CompanyId-first (tenant isolation, ADR-003); money columns use HasPrecision(14, 2)
// (ADR-033 handles the SQLite-as-REAL conversion globally); FKs are Restrict (no cascades across modules).

internal sealed class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ToTable("CostCenters");
        builder.Property(c => c.Code).HasMaxLength(CostCenter.CodeMaxLength);
        builder.Property(c => c.Name).HasMaxLength(CostCenter.NameMaxLength);
        builder.Property(c => c.Description).HasMaxLength(CostCenter.DescriptionMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ParentCostCenter).WithMany().HasForeignKey(c => c.ParentCostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(c => new { c.CompanyId, c.ParentCostCenterId });
    }
}

internal sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("ExpenseCategories");
        builder.Property(c => c.Name).HasMaxLength(ExpenseCategory.NameMaxLength);
        builder.Property(c => c.Code).HasMaxLength(ExpenseCategory.CodeMaxLength);
        builder.Property(c => c.Description).HasMaxLength(ExpenseCategory.DescriptionMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ParentCategory).WithMany().HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(c => new { c.CompanyId, c.ParentCategoryId });
        // One system category per aggregation source per company (never two "Fuel" categories).
        builder.HasIndex(c => new { c.CompanyId, c.CostAggregationKey })
            .IsUnique()
            .HasFilter($"{ConfigurationExtensions.NotDeletedFilter} AND [CostAggregationKey] IS NOT NULL");
    }
}

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");
        builder.Property(e => e.Description).HasMaxLength(Expense.DescriptionMaxLength);
        builder.Property(e => e.ReferenceNumber).HasMaxLength(Expense.ReferenceNumberMaxLength);
        builder.Property(e => e.SupplierName).HasMaxLength(Expense.SupplierNameMaxLength);
        builder.Property(e => e.Notes).HasMaxLength(Expense.NotesMaxLength);
        builder.Property(e => e.CancellationReason).HasMaxLength(Expense.ReasonMaxLength);
        builder.Property(e => e.Amount).HasPrecision(14, 2);
        builder.Property(e => e.PaidAmount).HasPrecision(14, 2);

        builder.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ExpenseCategory).WithMany().HasForeignKey(e => e.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Vehicle).WithMany().HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Driver).WithMany().HasForeignKey(e => e.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Workshop).WithMany().HasForeignKey(e => e.WorkshopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.RecurringExpenseTemplate).WithMany()
            .HasForeignKey(e => e.RecurringExpenseId).OnDelete(DeleteBehavior.Restrict);

        // Listing/report filters: by vehicle, by category, by cost center, by date, and the recurring-generation cursor.
        builder.HasIndex(e => new { e.CompanyId, e.ExpenseDate });
        builder.HasIndex(e => new { e.CompanyId, e.VehicleId, e.ExpenseDate });
        builder.HasIndex(e => new { e.CompanyId, e.ExpenseCategoryId, e.ExpenseDate });
        builder.HasIndex(e => new { e.CompanyId, e.CostCenterId });
        builder.HasIndex(e => new { e.CompanyId, e.DueDate });
        // One generated Expense per recurring template per due date (idempotent generation).
        builder.HasIndex(e => new { e.RecurringExpenseId, e.DueDate })
            .IsUnique()
            .HasFilter($"{ConfigurationExtensions.NotDeletedFilter} AND [RecurringExpenseId] IS NOT NULL");
    }
}

internal sealed class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> builder)
    {
        builder.ToTable("RecurringExpenses");
        builder.Property(r => r.Description).HasMaxLength(RecurringExpense.DescriptionMaxLength);
        builder.Property(r => r.SupplierName).HasMaxLength(RecurringExpense.SupplierNameMaxLength);
        builder.Property(r => r.Amount).HasPrecision(14, 2);

        builder.HasOne<Company>().WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ExpenseCategory).WithMany().HasForeignKey(r => r.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CostCenter).WithMany().HasForeignKey(r => r.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Vehicle).WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Workshop).WithMany().HasForeignKey(r => r.WorkshopId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.IsActive });
    }
}

internal sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");
        builder.Property(b => b.Amount).HasPrecision(14, 2);
        builder.Property(b => b.Notes).HasMaxLength(Budget.NotesMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(b => b.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.ExpenseCategory).WithMany().HasForeignKey(b => b.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.CostCenter).WithMany().HasForeignKey(b => b.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Vehicle).WithMany().HasForeignKey(b => b.VehicleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.CompanyId, b.Year, b.Month });
        builder.HasIndex(b => new { b.CompanyId, b.ExpenseCategoryId, b.Year, b.Month });
    }
}
