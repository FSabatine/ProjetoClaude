using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Infrastructure.Persistence;

/// <summary>
/// DEVELOPMENT ONLY (Phase 6 samples). Cost centers, a recurring insurance template, a handful of manual expenses
/// across categories/statuses (paid, pending, overdue, cancelled) on the sample vehicles, and two budgets for the
/// current month. Fuel/Maintenance/Tires costs need no sample data here — CostAggregationService reads the
/// Phase 4/5 samples directly. Idempotent: skips when the company already has cost centers.
/// </summary>
internal sealed class DevFinanceSeeder(FleetDbContext db, IClock clock)
{
    public async Task<bool> SeedAsync(Guid companyId, CancellationToken ct)
    {
        if (await db.CostCenters.IgnoreQueryFilters().AnyAsync(c => c.CompanyId == companyId, ct)) return false;
        var vehicles = await db.Vehicles.IgnoreQueryFilters()
            .Where(v => v.CompanyId == companyId && v.DeletedAt == null).ToListAsync(ct);
        if (vehicles.Count == 0) return false;

        var categories = await db.ExpenseCategories.IgnoreQueryFilters().Where(c => c.CompanyId == companyId).ToListAsync(ct);
        if (categories.Count == 0)
        {
            categories = ExpenseCategoryService.CreateDefaults(companyId).ToList();
            db.ExpenseCategories.AddRange(categories);
        }
        Guid CategoryId(string code) => categories.First(c => c.Code == code).Id;

        var operations = new CostCenter { Id = Guid.NewGuid(), CompanyId = companyId, Code = "OPS", Name = "Operações" };
        var admin = new CostCenter { Id = Guid.NewGuid(), CompanyId = companyId, Code = "ADM", Name = "Administração" };
        db.CostCenters.AddRange(operations, admin);

        var scania = vehicles.FirstOrDefault(v => v.LicensePlate == "RDX1A23") ?? vehicles[0];
        var pickup = vehicles.FirstOrDefault(v => v.LicensePlate == "ABC1234") ?? vehicles[^1];
        var today = clock.Today;

        db.RecurringExpenses.Add(new RecurringExpense
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Description = "Seguro da frota", ExpenseCategoryId = CategoryId("INSURANCE"),
            CostCenterId = admin.Id, Amount = 4_800m, Frequency = ExpenseFrequency.Monthly, StartDate = today.AddMonths(-6), DueDayOfMonth = 5,
        });

        var expenses = new[]
        {
            new Expense
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ExpenseCategoryId = CategoryId("TOLLS"), CostCenterId = operations.Id,
                VehicleId = scania.Id, Description = "Pedágio BR-277 — viagem Curitiba/Paranaguá", ExpenseDate = today.AddDays(-20),
                DueDate = today.AddDays(-20), Amount = 187.40m, PaidAmount = 187.40m, PaymentDate = today.AddDays(-20),
            },
            new Expense
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ExpenseCategoryId = CategoryId("WASHING"), CostCenterId = operations.Id,
                VehicleId = pickup.Id, Description = "Lavagem completa", ExpenseDate = today.AddDays(-8), Amount = 45m,
            },
            new Expense
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ExpenseCategoryId = CategoryId("IPVA"), CostCenterId = admin.Id,
                VehicleId = scania.Id, Description = "IPVA 2026 — parcela 3/3", ExpenseDate = today.AddDays(-40),
                DueDate = today.AddDays(-10), Amount = 1_250m, PaidAmount = 0m,
            },
            new Expense
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ExpenseCategoryId = CategoryId("FINES"), CostCenterId = operations.Id,
                VehicleId = pickup.Id, Description = "Multa — excesso de velocidade", ExpenseDate = today.AddDays(-3),
                DueDate = today.AddDays(27), Amount = 293.47m,
            },
            new Expense
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ExpenseCategoryId = CategoryId("OTHER"), CostCenterId = admin.Id,
                Description = "Lançamento duplicado (exemplo cancelado)", ExpenseDate = today.AddDays(-15), Amount = 500m,
                CancelledAt = clock.UtcNow.AddDays(-14), CancellationReason = "Lançado em duplicidade por engano.",
            },
        };
        db.Expenses.AddRange(expenses);

        db.Budgets.AddRange(
            new Budget { Id = Guid.NewGuid(), CompanyId = companyId, Year = today.Year, Month = today.Month, ExpenseCategoryId = CategoryId("TOLLS"), Amount = 1_500m },
            new Budget { Id = Guid.NewGuid(), CompanyId = companyId, Year = today.Year, Month = today.Month, ExpenseCategoryId = CategoryId("FUEL"), Amount = 35_000m });

        await db.SaveChangesAsync(ct);
        return true;
    }
}
