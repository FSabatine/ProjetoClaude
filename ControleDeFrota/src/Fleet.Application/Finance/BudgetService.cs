using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Finance;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Finance;

public sealed record BudgetRequest
{
    public int? Year { get; init; }
    /// <summary>Null = annual budget for the whole year.</summary>
    public int? Month { get; init; }
    public Guid? ExpenseCategoryId { get; init; }
    public Guid? CostCenterId { get; init; }
    public Guid? VehicleId { get; init; }
    public decimal? Amount { get; init; }
    public string? Notes { get; init; }
}

public sealed record BudgetResponse(
    Guid Id, int Year, int? Month, Guid ExpenseCategoryId, string CategoryName, Guid? CostCenterId, string? CostCenterName,
    Guid? VehicleId, string? LicensePlate, decimal Amount, string? Notes);

/// <summary>Budget row + what was actually spent in the same scope/period (spec §13/§14) — Actual is never stored.</summary>
public sealed record BudgetVsActualResponse(
    BudgetResponse Budget, decimal? Actual, decimal? Remaining, decimal? UtilizationPercent, BudgetStatus Status, bool IsPartial);

public sealed class BudgetRequestValidator : AbstractValidator<BudgetRequest>
{
    public BudgetRequestValidator()
    {
        RuleFor(x => x.Year).NotNull().WithMessage("Ano: campo obrigatório.")
            .InclusiveBetween(2000, 2100).WithMessage("Ano inválido.").When(x => x.Year.HasValue);
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("Mês inválido (1 a 12).").When(x => x.Month.HasValue);
        RuleFor(x => x.ExpenseCategoryId).NotNull().WithMessage("Categoria: campo obrigatório.");
        RuleFor(x => x.Amount).NotNull().WithMessage("Valor orçado: campo obrigatório.")
            .GreaterThan(0m).WithMessage("Valor orçado deve ser maior que zero.").When(x => x.Amount.HasValue);
        RuleFor(x => x.Notes).MaxLen(Budget.NotesMaxLength);
    }
}

/// <summary>
/// Planned amounts by period/category/dimension (ADR-040). A budget line can target the whole category, a cost
/// center or a single vehicle — never forced into one shape, matching the spec's own mixed examples.
/// </summary>
public sealed class BudgetService(IFleetDbContext db, ICurrentUser currentUser, IValidator<BudgetRequest> validator, CostAggregationService costs)
{
    private bool CanSeeCosts => currentUser.HasPermission(Permissions.Finance.ViewCosts);

    public async Task<IReadOnlyList<BudgetResponse>> ListAsync(int year, CancellationToken ct) =>
        await db.Budgets.Where(b => b.Year == year).OrderBy(b => b.Month).ThenBy(b => b.ExpenseCategory.Name)
            .Select(Projection()).ToListAsync(ct);

    public async Task<IReadOnlyList<BudgetVsActualResponse>> ListVsActualAsync(int year, int? month, CancellationToken ct)
    {
        if (!CanSeeCosts) return [];
        var budgets = await db.Budgets.Where(b => b.Year == year && b.Month == month).Select(Projection()).ToListAsync(ct);
        var (from, to) = PeriodBounds(year, month);
        var result = new List<BudgetVsActualResponse>();
        foreach (var budget in budgets)
        {
            // A budget targeting one vehicle reads that vehicle's breakdown; a category/cost-center-wide budget
            // reads the fleet's cost-by-category (manual categories only need the category's own manual total).
            decimal? actual;
            bool isPartial;
            if (budget.VehicleId is { } vehicleId)
            {
                var breakdown = await costs.GetVehicleCostBreakdownAsync(vehicleId, from, to, ct);
                actual = CategoryAmount(budget.CategoryName, breakdown);
                isPartial = breakdown.IsPartial;
            }
            else
            {
                var (categories, partial) = await costs.GetFleetCostByCategoryAsync(from, to, ct);
                var match = categories.FirstOrDefault(c => c.ExpenseCategoryId == budget.ExpenseCategoryId || c.CategoryName == budget.CategoryName);
                actual = match?.Amount ?? 0m;
                isPartial = partial;
            }
            result.Add(new BudgetVsActualResponse(budget, actual, actual is { } a ? BudgetAnalysis.Remaining(budget.Amount, a) : null,
                actual is { } a2 ? BudgetAnalysis.UtilizationPercent(budget.Amount, a2) : null,
                actual is { } a3 ? BudgetAnalysis.Status(budget.Amount, a3) : BudgetStatus.NoBudget, isPartial));
        }
        return result;
    }

    private static decimal CategoryAmount(string categoryName, VehicleCostBreakdown breakdown) => categoryName switch
    {
        "Combustível" => breakdown.FuelCost,
        "Manutenção" => breakdown.MaintenanceCost,
        "Pneus" => breakdown.TireCost,
        _ => breakdown.OtherCost,
    };

    private static (DateOnly From, DateOnly To) PeriodBounds(int year, int? month)
    {
        if (month is { } m) return (new DateOnly(year, m, 1), new DateOnly(year, m, DateTime.DaysInMonth(year, m)));
        return (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
    }

    public async Task<BudgetResponse> CreateAsync(BudgetRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureReferencesExistAsync(request, ct);
        if (await db.Budgets.AnyAsync(b => b.Year == request.Year && b.Month == request.Month &&
            b.ExpenseCategoryId == request.ExpenseCategoryId && b.CostCenterId == request.CostCenterId && b.VehicleId == request.VehicleId, ct))
            throw new ConflictException("Já existe um orçamento para este período, categoria e escopo.");

        var budget = new Budget { Id = Guid.NewGuid() };
        Apply(request, budget);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(ct);
        return await GetAsync(budget.Id, ct);
    }

    public async Task<BudgetResponse> UpdateAsync(Guid id, BudgetRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var budget = await LoadAsync(id, ct);
        await EnsureReferencesExistAsync(request, ct);
        if (await db.Budgets.AnyAsync(b => b.Id != id && b.Year == request.Year && b.Month == request.Month &&
            b.ExpenseCategoryId == request.ExpenseCategoryId && b.CostCenterId == request.CostCenterId && b.VehicleId == request.VehicleId, ct))
            throw new ConflictException("Já existe um orçamento para este período, categoria e escopo.");
        Apply(request, budget);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var budget = await LoadAsync(id, ct);
        db.Budgets.Remove(budget);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureReferencesExistAsync(BudgetRequest request, CancellationToken ct)
    {
        if (!await db.ExpenseCategories.AnyAsync(c => c.Id == request.ExpenseCategoryId, ct))
            throw new NotFoundException("Categoria de despesa não encontrada.");
        if (request.CostCenterId is { } costCenterId && !await db.CostCenters.AnyAsync(c => c.Id == costCenterId, ct))
            throw new NotFoundException("Centro de custo não encontrado.");
        if (request.VehicleId is { } vehicleId && !await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new NotFoundException("Veículo não encontrado.");
    }

    private static void Apply(BudgetRequest request, Budget budget)
    {
        budget.Year = request.Year!.Value;
        budget.Month = request.Month;
        budget.ExpenseCategoryId = request.ExpenseCategoryId!.Value;
        budget.CostCenterId = request.CostCenterId;
        budget.VehicleId = request.VehicleId;
        budget.Amount = request.Amount!.Value;
        budget.Notes = request.Notes.TrimToNull();
    }

    private System.Linq.Expressions.Expression<Func<Budget, BudgetResponse>> Projection() => b => new BudgetResponse(
        b.Id, b.Year, b.Month, b.ExpenseCategoryId, b.ExpenseCategory.Name, b.CostCenterId, b.CostCenter == null ? null : b.CostCenter.Name,
        b.VehicleId, b.Vehicle == null ? null : b.Vehicle.LicensePlate, b.Amount, b.Notes);

    private async Task<BudgetResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.Budgets.Where(b => b.Id == id).Select(Projection()).SingleAsync(ct);

    private async Task<Budget> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Budgets.SingleOrDefaultAsync(b => b.Id == id, ct)
        ?? throw new NotFoundException("Orçamento não encontrado. Ele pode ter sido excluído.");
}
