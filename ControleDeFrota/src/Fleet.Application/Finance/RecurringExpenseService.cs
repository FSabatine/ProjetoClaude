using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Common;
using Fleet.Domain.Finance;
using Fleet.Domain.Operations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Finance;

public sealed record RecurringExpenseRequest
{
    public string? Description { get; init; }
    public Guid? ExpenseCategoryId { get; init; }
    public Guid? CostCenterId { get; init; }
    public Guid? VehicleId { get; init; }
    public Guid? WorkshopId { get; init; }
    public string? SupplierName { get; init; }
    public decimal? Amount { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public ExpenseFrequency? Frequency { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int DueDayOfMonth { get; init; } = 1;
    public bool IsActive { get; init; } = true;
}

public sealed record RecurringExpenseResponse(
    Guid Id, string Description, Guid ExpenseCategoryId, string CategoryName, Guid? CostCenterId, string? CostCenterName,
    Guid? VehicleId, string? LicensePlate, Guid? WorkshopId, string? WorkshopName, string? SupplierName,
    decimal? Amount, PaymentMethod? PaymentMethod, ExpenseFrequency Frequency, DateOnly StartDate, DateOnly? EndDate,
    int DueDayOfMonth, bool IsActive, DateOnly? NextDueDate, DateOnly? LastGeneratedDueDate);

public sealed class RecurringExpenseRequestValidator : AbstractValidator<RecurringExpenseRequest>
{
    public RecurringExpenseRequestValidator()
    {
        RuleFor(x => x.Description).Required("Descrição").MaxLen(RecurringExpense.DescriptionMaxLength);
        RuleFor(x => x.ExpenseCategoryId).NotNull().WithMessage("Categoria: campo obrigatório.");
        RuleFor(x => x.SupplierName).MaxLen(RecurringExpense.SupplierNameMaxLength);
        RuleFor(x => x.Amount).NotNull().WithMessage("Valor: campo obrigatório.")
            .GreaterThan(0m).WithMessage("Valor deve ser maior que zero.").When(x => x.Amount.HasValue);
        RuleFor(x => x.Frequency).NotNull().WithMessage("Frequência: campo obrigatório.").IsInEnum().WithMessage("Frequência inválida.");
        RuleFor(x => x.StartDate).NotNull().WithMessage("Data de início: campo obrigatório.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.EndDate.HasValue && x.StartDate.HasValue).WithMessage("Data de término não pode ser anterior ao início.");
        RuleFor(x => x.DueDayOfMonth).InclusiveBetween(1, 28).WithMessage("Dia de vencimento deve ser entre 1 e 28.");
    }
}

/// <summary>
/// Templates for recurring operational obligations (insurance, financing, leasing…). Generation happens in
/// RecurringExpenseGenerationScanner (ADR-040, same idea as DocumentExpirationScanner): this service only manages
/// the template.
/// </summary>
public sealed class RecurringExpenseService(IFleetDbContext db, IValidator<RecurringExpenseRequest> validator)
{
    public async Task<IReadOnlyList<RecurringExpenseResponse>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.RecurringExpenses.AsQueryable();
        if (!includeInactive) query = query.Where(r => r.IsActive);
        return await query.OrderBy(r => r.Description).Select(Projection()).ToListAsync(ct);
    }

    public async Task<RecurringExpenseResponse> CreateAsync(RecurringExpenseRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var category = await db.ExpenseCategories.SingleOrDefaultAsync(c => c.Id == request.ExpenseCategoryId, ct)
            ?? throw new NotFoundException("Categoria de despesa não encontrada.");
        if (category.IsSystemCategory)
            throw new BusinessRuleException($"\"{category.Name}\" é alimentada automaticamente pelo módulo de origem e não aceita despesas recorrentes manuais.");
        await EnsureReferencesExistAsync(request, ct);

        var recurring = new RecurringExpense { Id = Guid.NewGuid() };
        Apply(request, recurring);
        db.RecurringExpenses.Add(recurring);
        await db.SaveChangesAsync(ct);
        return await GetAsync(recurring.Id, ct);
    }

    public async Task<RecurringExpenseResponse> UpdateAsync(Guid id, RecurringExpenseRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var recurring = await LoadAsync(id, ct);
        var category = await db.ExpenseCategories.SingleOrDefaultAsync(c => c.Id == request.ExpenseCategoryId, ct)
            ?? throw new NotFoundException("Categoria de despesa não encontrada.");
        if (category.IsSystemCategory)
            throw new BusinessRuleException($"\"{category.Name}\" é alimentada automaticamente pelo módulo de origem e não aceita despesas recorrentes manuais.");
        await EnsureReferencesExistAsync(request, ct);
        Apply(request, recurring);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var recurring = await LoadAsync(id, ct);
        if (await db.Expenses.AnyAsync(e => e.RecurringExpenseId == id, ct))
            throw new BusinessRuleException("Esta despesa recorrente já gerou lançamentos e não pode ser excluída. Inative-a.");
        db.RecurringExpenses.Remove(recurring);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureReferencesExistAsync(RecurringExpenseRequest request, CancellationToken ct)
    {
        if (request.CostCenterId is { } costCenterId && !await db.CostCenters.AnyAsync(c => c.Id == costCenterId, ct))
            throw new NotFoundException("Centro de custo não encontrado.");
        if (request.VehicleId is { } vehicleId && !await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new NotFoundException("Veículo não encontrado.");
        if (request.WorkshopId is { } workshopId && !await db.Workshops.AnyAsync(w => w.Id == workshopId, ct))
            throw new NotFoundException("Fornecedor (oficina) não encontrado.");
    }

    private static void Apply(RecurringExpenseRequest request, RecurringExpense recurring)
    {
        recurring.Description = request.Description!.Trim();
        recurring.ExpenseCategoryId = request.ExpenseCategoryId!.Value;
        recurring.CostCenterId = request.CostCenterId;
        recurring.VehicleId = request.VehicleId;
        recurring.WorkshopId = request.WorkshopId;
        recurring.SupplierName = request.SupplierName.TrimToNull();
        recurring.Amount = request.Amount!.Value;
        recurring.PaymentMethod = request.PaymentMethod;
        recurring.Frequency = request.Frequency!.Value;
        recurring.StartDate = request.StartDate!.Value;
        recurring.EndDate = request.EndDate;
        recurring.DueDayOfMonth = request.DueDayOfMonth;
        recurring.IsActive = request.IsActive;
    }

    private System.Linq.Expressions.Expression<Func<RecurringExpense, RecurringExpenseResponse>> Projection() => r => new RecurringExpenseResponse(
        r.Id, r.Description, r.ExpenseCategoryId, r.ExpenseCategory.Name, r.CostCenterId, r.CostCenter == null ? null : r.CostCenter.Name,
        r.VehicleId, r.Vehicle == null ? null : r.Vehicle.LicensePlate, r.WorkshopId, r.Workshop == null ? null : r.Workshop.Name,
        r.SupplierName, r.Amount, r.PaymentMethod, r.Frequency, r.StartDate, r.EndDate, r.DueDayOfMonth, r.IsActive,
        r.IsActive ? (DateOnly?)NextDue(r) : null, r.LastGeneratedDueDate);

    private static DateOnly NextDue(RecurringExpense r) => r.LastGeneratedDueDate is { } last
        ? RecurringExpensePolicy.NextDueDate(last, r.Frequency, r.DueDayOfMonth)
        : RecurringExpensePolicy.FirstDueDate(r.StartDate, r.DueDayOfMonth);

    private async Task<RecurringExpenseResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.RecurringExpenses.Where(r => r.Id == id).Select(Projection()).SingleAsync(ct);

    private async Task<RecurringExpense> LoadAsync(Guid id, CancellationToken ct) =>
        await db.RecurringExpenses.SingleOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException("Despesa recorrente não encontrada. Ela pode ter sido excluída.");
}

/// <summary>
/// Turns active RecurringExpense templates into dated Expense rows ahead of time (spec §8 "upcoming obligations"),
/// idempotent via the (RecurringExpenseId, DueDate) unique index. Runs outside a request, for every company —
/// same shape as DocumentExpirationScanner (ADR-025/ADR-040).
/// </summary>
public sealed class RecurringExpenseGenerationScanner(IFleetDbContext db, IClock clock)
{
    public const int BatchSize = 200;
    /// <summary>Generate obligations due up to this many days ahead, so they show on the dashboard before they're due.</summary>
    public const int HorizonDays = 30;

    public async Task<int> ScanAsync(CancellationToken ct)
    {
        var horizon = clock.Today.AddDays(HorizonDays);
        var generated = 0;
        var templates = await db.RecurringExpenses.IgnoreQueryFilters()
            .Where(r => r.DeletedAt == null && r.IsActive &&
                        db.Companies.IgnoreQueryFilters().Any(c => c.Id == r.CompanyId && c.DeletedAt == null && c.IsActive))
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var template in templates)
        {
            var dueDates = RecurringExpensePolicy.DueDatesToGenerate(
                template.StartDate, template.EndDate, template.DueDayOfMonth, template.Frequency, template.LastGeneratedDueDate, horizon);
            foreach (var dueDate in dueDates)
            {
                // Idempotent even if the scanner runs twice before LastGeneratedDueDate is committed (several API instances).
                if (await db.Expenses.IgnoreQueryFilters().AnyAsync(e => e.RecurringExpenseId == template.Id && e.DueDate == dueDate, ct))
                    continue;
                var expense = new Expense
                {
                    Id = Guid.NewGuid(), CompanyId = template.CompanyId, ExpenseCategoryId = template.ExpenseCategoryId,
                    CostCenterId = template.CostCenterId, VehicleId = template.VehicleId, WorkshopId = template.WorkshopId,
                    SupplierName = template.SupplierName, Description = template.Description, ExpenseDate = dueDate, DueDate = dueDate,
                    Amount = template.Amount, PaymentMethod = template.PaymentMethod, IsRecurring = true, RecurringExpenseId = template.Id,
                };
                db.Expenses.Add(expense);
                db.OperationalEvents.Add(OperationalEventLog.Create(
                    OperationalEventType.RecurringExpenseGenerated, new EventSubject(nameof(Expense), expense.Id, VehicleId: expense.VehicleId),
                    $"Despesa recorrente \"{template.Description}\" gerada para {dueDate:dd/MM/yyyy}.",
                    new { templateId = template.Id }, clock.StartOfBusinessDayUtc(dueDate), userId: null, template.CompanyId));
                template.LastGeneratedDueDate = dueDate;
                generated++;
            }
        }
        if (generated > 0) await db.SaveChangesAsync(ct);
        return generated;
    }
}
