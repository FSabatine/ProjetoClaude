using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Application.Operations;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Files;
using Fleet.Domain.Finance;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Finance;

public sealed record ExpenseRequest
{
    public Guid? ExpenseCategoryId { get; init; }
    public Guid? CostCenterId { get; init; }
    public Guid? VehicleId { get; init; }
    public Guid? DriverId { get; init; }
    public Guid? WorkshopId { get; init; }
    public string? SupplierName { get; init; }
    public string? Description { get; init; }
    public string? ReferenceNumber { get; init; }
    public DateOnly? ExpenseDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public decimal? Amount { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public sealed record ExpensePaymentRequest
{
    public decimal? PaidAmount { get; init; }
    public DateOnly? PaymentDate { get; init; }
}

public sealed record ExpenseCancelRequest
{
    public string? Reason { get; init; }
}

public sealed class ExpenseListRequest : ListRequest
{
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ExpenseCategoryId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public PaymentStatus? Status { get; set; }
    public bool? IsRecurring { get; set; }
}

public sealed record ExpenseResponse(
    Guid Id, Guid ExpenseCategoryId, string CategoryName, Guid? CostCenterId, string? CostCenterName,
    Guid? VehicleId, string? LicensePlate, Guid? DriverId, string? DriverName, Guid? WorkshopId, string? WorkshopName,
    string? SupplierName, string Description, string? ReferenceNumber, DateOnly ExpenseDate, DateOnly? DueDate,
    decimal? Amount, decimal? PaidAmount, DateOnly? PaymentDate, PaymentMethod? PaymentMethod, PaymentStatus Status,
    bool IsRecurring, Guid? RecurringExpenseId, string? Notes, bool IsDuplicateSuspect,
    bool CanEdit, bool CanCancel, bool CanRegisterPayment, DateTime? CancelledAt, string? CancellationReason);

public sealed class ExpenseRequestValidator : AbstractValidator<ExpenseRequest>
{
    public ExpenseRequestValidator()
    {
        RuleFor(x => x.ExpenseCategoryId).NotNull().WithMessage("Categoria: campo obrigatório.");
        RuleFor(x => x.Description).Required("Descrição").MaxLen(Expense.DescriptionMaxLength);
        RuleFor(x => x.ReferenceNumber).MaxLen(Expense.ReferenceNumberMaxLength);
        RuleFor(x => x.SupplierName).MaxLen(Expense.SupplierNameMaxLength);
        RuleFor(x => x.Notes).MaxLen(Expense.NotesMaxLength);
        RuleFor(x => x.ExpenseDate).NotNull().WithMessage("Data da despesa: campo obrigatório.");
        RuleFor(x => x.Amount).NotNull().WithMessage("Valor: campo obrigatório.")
            .GreaterThan(0m).WithMessage("Valor deve ser maior que zero.")
            .LessThanOrEqualTo(Expense.MaxAmount).WithMessage($"Valor não pode passar de {Expense.MaxAmount:N0}.")
            .When(x => x.Amount.HasValue);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.ExpenseDate!.Value)
            .When(x => x.DueDate.HasValue && x.ExpenseDate.HasValue)
            .WithMessage("Vencimento não pode ser anterior à data da despesa.");
    }
}

public sealed class ExpensePaymentRequestValidator : AbstractValidator<ExpensePaymentRequest>
{
    public ExpensePaymentRequestValidator()
    {
        RuleFor(x => x.PaidAmount).NotNull().WithMessage("Valor pago: campo obrigatório.")
            .GreaterThanOrEqualTo(0m).WithMessage("Valor pago não pode ser negativo.");
        RuleFor(x => x.PaymentDate).NotNull().WithMessage("Data do pagamento: campo obrigatório.")
            .When(x => x.PaidAmount is > 0);
    }
}

public sealed class ExpenseCancelRequestValidator : AbstractValidator<ExpenseCancelRequest>
{
    public ExpenseCancelRequestValidator() =>
        RuleFor(x => x.Reason).Required("Motivo").MaxLen(Expense.ReasonMaxLength);
}

/// <summary>
/// The manual/recurring expense ledger (ADR-040). Fuel/Maintenance/Tires costs never pass through here — see
/// CostAggregationService. Nothing is ever hard-deleted: a mistaken entry is cancelled, never removed (spec §32).
/// </summary>
public sealed class ExpenseService(
    IFleetDbContext db, ICurrentUser currentUser, IClock clock, OperationalEventLog events, FileService files,
    IValidator<ExpenseRequest> validator, IValidator<ExpensePaymentRequest> paymentValidator, IValidator<ExpenseCancelRequest> cancelValidator)
{
    private bool CanSeeCosts => currentUser.HasPermission(Permissions.Finance.ViewCosts);

    public async Task<PagedResult<ExpenseResponse>> ListAsync(ExpenseListRequest request, CancellationToken ct)
    {
        var query = db.Expenses.AsQueryable();
        if (request.VehicleId is { } vehicleId) query = query.Where(e => e.VehicleId == vehicleId);
        if (request.DriverId is { } driverId) query = query.Where(e => e.DriverId == driverId);
        if (request.CostCenterId is { } costCenterId) query = query.Where(e => e.CostCenterId == costCenterId);
        if (request.ExpenseCategoryId is { } categoryId) query = query.Where(e => e.ExpenseCategoryId == categoryId);
        if (request.From is { } from) query = query.Where(e => e.ExpenseDate >= from);
        if (request.To is { } to) query = query.Where(e => e.ExpenseDate <= to);
        if (request.IsRecurring is { } recurring) query = query.Where(e => e.IsRecurring == recurring);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm;
            query = query.Where(e => e.Description.Contains(term) || (e.ReferenceNumber != null && e.ReferenceNumber.Contains(term)));
        }

        var sort = new SortMap<Expense>("expenseDate")
            .Add("expenseDate", e => e.ExpenseDate)
            .Add("dueDate", e => e.DueDate)
            .Add("amount", e => e.Amount)
            .Add("description", e => e.Description);
        query = sort.Apply(query, request.SortBy, request.SortDirection);

        var today = clock.Today;
        var page = await query.ToPagedResultAsync(request, Projection(today), ct);
        if (request.Status is { } status)
        {
            // Status is computed, not stored: filter after projection (small pages only — the list is already paged).
            var filtered = page.Items.Where(e => e.Status == status).ToList();
            return page with { Items = filtered };
        }
        return page;
    }

    public async Task<ExpenseResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.Expenses.Where(e => e.Id == id).Select(Projection(clock.Today)).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Despesa não encontrada. Ela pode ter sido excluída.");

    public async Task<ExpenseResponse> CreateAsync(ExpenseRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var category = await db.ExpenseCategories.SingleOrDefaultAsync(c => c.Id == request.ExpenseCategoryId, ct)
            ?? throw new NotFoundException("Categoria de despesa não encontrada.");
        if (category.IsSystemCategory)
            throw new BusinessRuleException($"\"{category.Name}\" é alimentada automaticamente pelo módulo de origem e não aceita lançamentos manuais.");
        await EnsureReferencesExistAsync(request, ct);

        var expense = new Expense { Id = Guid.NewGuid() };
        Apply(request, expense);
        db.Expenses.Add(expense);
        await files.AttachAsync(request.FileIds, FileOwnerType.Expense, expense.Id, "fileIds", ct);
        events.Record(OperationalEventType.ExpenseCreated, Subject(expense), $"Despesa \"{expense.Description}\" registrada ({category.Name}).");
        await CheckBudgetAsync(expense, category.Name, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(expense.Id, ct);
    }

    /// <summary>
    /// Manual expenses only ever belong to non-system categories (Fuel/Maintenance/Tires are rejected above), so
    /// "actual" here is just the sum of other manual expenses in the same scope — no blending needed (ADR-040).
    /// </summary>
    private async Task CheckBudgetAsync(Expense expense, string categoryName, CancellationToken ct)
    {
        var budgets = await db.Budgets
            .Where(b => b.ExpenseCategoryId == expense.ExpenseCategoryId && b.Year == expense.ExpenseDate.Year &&
                        (b.Month == null || b.Month == expense.ExpenseDate.Month) &&
                        (b.CostCenterId == null || b.CostCenterId == expense.CostCenterId) &&
                        (b.VehicleId == null || b.VehicleId == expense.VehicleId))
            .ToListAsync(ct);
        foreach (var budget in budgets)
        {
            // expense is still only in the change tracker (not yet saved), so it is added to the DB sum by hand —
            // it always matches this budget's own scope, since `budgets` was already filtered against its values.
            var actual = expense.Amount + await db.Expenses
                .Where(e => e.Id != expense.Id && e.ExpenseCategoryId == budget.ExpenseCategoryId && e.CancelledAt == null &&
                            e.ExpenseDate.Year == budget.Year && (budget.Month == null || e.ExpenseDate.Month == budget.Month) &&
                            (budget.CostCenterId == null || e.CostCenterId == budget.CostCenterId) &&
                            (budget.VehicleId == null || e.VehicleId == budget.VehicleId))
                .SumAsync(e => e.Amount, ct);
            if (actual > budget.Amount)
                events.Record(OperationalEventType.BudgetExceeded, Subject(expense),
                    $"Orçamento de \"{categoryName}\" ultrapassado para o período.");
        }
    }

    public async Task<ExpenseResponse> UpdateAsync(Guid id, ExpenseRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var expense = await LoadAsync(id, ct);
        if (expense.CancelledAt is not null)
            throw new BusinessRuleException("Esta despesa está cancelada e não pode ser editada.");
        if (request.Amount < expense.PaidAmount)
            throw new BusinessRuleException("O valor não pode ficar menor que o valor já pago. Registre um estorno ou cancele a despesa.");
        var category = await db.ExpenseCategories.SingleOrDefaultAsync(c => c.Id == request.ExpenseCategoryId, ct)
            ?? throw new NotFoundException("Categoria de despesa não encontrada.");
        if (category.IsSystemCategory)
            throw new BusinessRuleException($"\"{category.Name}\" é alimentada automaticamente pelo módulo de origem e não aceita lançamentos manuais.");
        await EnsureReferencesExistAsync(request, ct);

        Apply(request, expense);
        await files.AttachAsync(request.FileIds, FileOwnerType.Expense, expense.Id, "fileIds", ct);
        events.Record(OperationalEventType.ExpenseEdited, Subject(expense), $"Despesa \"{expense.Description}\" editada.");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ExpenseResponse> RegisterPaymentAsync(Guid id, ExpensePaymentRequest request, CancellationToken ct)
    {
        await paymentValidator.ValidateAndThrowAsync(request, ct);
        var expense = await LoadAsync(id, ct);
        if (expense.CancelledAt is not null)
            throw new BusinessRuleException("Esta despesa está cancelada e não aceita pagamento.");
        if (request.PaidAmount > expense.Amount)
            throw ValidationErrors.ForField("paidAmount", "Valor pago não pode passar do valor da despesa.");
        expense.PaidAmount = request.PaidAmount!.Value;
        expense.PaymentDate = request.PaidAmount > 0 ? request.PaymentDate : null;
        events.Record(OperationalEventType.ExpensePaymentRegistered, Subject(expense), $"Pagamento registrado para \"{expense.Description}\".");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ExpenseResponse> CancelAsync(Guid id, ExpenseCancelRequest request, CancellationToken ct)
    {
        await cancelValidator.ValidateAndThrowAsync(request, ct);
        var expense = await LoadAsync(id, ct);
        if (expense.CancelledAt is not null)
            throw new BusinessRuleException("Esta despesa já está cancelada.");
        expense.CancelledAt = clock.UtcNow;
        expense.CancelledBy = currentUser.UserId;
        expense.CancellationReason = request.Reason!.Trim();
        events.Record(OperationalEventType.ExpenseCancelled, Subject(expense), $"Despesa \"{expense.Description}\" cancelada.");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task EnsureReferencesExistAsync(ExpenseRequest request, CancellationToken ct)
    {
        if (request.CostCenterId is { } costCenterId && !await db.CostCenters.AnyAsync(c => c.Id == costCenterId, ct))
            throw new NotFoundException("Centro de custo não encontrado.");
        if (request.VehicleId is { } vehicleId && !await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new NotFoundException("Veículo não encontrado.");
        if (request.DriverId is { } driverId && !await db.Drivers.AnyAsync(d => d.Id == driverId, ct))
            throw new NotFoundException("Motorista não encontrado.");
        if (request.WorkshopId is { } workshopId && !await db.Workshops.AnyAsync(w => w.Id == workshopId, ct))
            throw new NotFoundException("Fornecedor (oficina) não encontrado.");
    }

    private static void Apply(ExpenseRequest request, Expense expense)
    {
        expense.ExpenseCategoryId = request.ExpenseCategoryId!.Value;
        expense.CostCenterId = request.CostCenterId;
        expense.VehicleId = request.VehicleId;
        expense.DriverId = request.DriverId;
        expense.WorkshopId = request.WorkshopId;
        expense.SupplierName = request.SupplierName.TrimToNull();
        expense.Description = request.Description!.Trim();
        expense.ReferenceNumber = request.ReferenceNumber.TrimToNull();
        expense.ExpenseDate = request.ExpenseDate!.Value;
        expense.DueDate = request.DueDate;
        expense.Amount = request.Amount!.Value;
        expense.PaymentMethod = request.PaymentMethod;
        expense.Notes = request.Notes.TrimToNull();
    }

    internal static EventSubject Subject(Expense e) => new(nameof(Expense), e.Id, VehicleId: e.VehicleId, DriverId: e.DriverId);

    private System.Linq.Expressions.Expression<Func<Expense, ExpenseResponse>> Projection(DateOnly today) => e => new ExpenseResponse(
        e.Id, e.ExpenseCategoryId, e.ExpenseCategory.Name, e.CostCenterId, e.CostCenter == null ? null : e.CostCenter.Name,
        e.VehicleId, e.Vehicle == null ? null : e.Vehicle.LicensePlate, e.DriverId, e.Driver == null ? null : e.Driver.FullName,
        e.WorkshopId, e.Workshop == null ? null : e.Workshop.Name, e.SupplierName, e.Description, e.ReferenceNumber,
        e.ExpenseDate, e.DueDate,
        CanSeeCosts || e.CreatedBy == currentUser.UserId ? e.Amount : (decimal?)null,
        CanSeeCosts || e.CreatedBy == currentUser.UserId ? e.PaidAmount : (decimal?)null,
        e.PaymentDate, e.PaymentMethod,
        e.CancelledAt != null ? PaymentStatus.Cancelled
            : e.PaidAmount >= e.Amount && e.Amount > 0 ? PaymentStatus.Paid
            : e.PaidAmount > 0 ? PaymentStatus.PartiallyPaid
            : e.DueDate != null && e.DueDate < today ? PaymentStatus.Overdue
            : e.DueDate != null ? PaymentStatus.Scheduled
            : PaymentStatus.Pending,
        e.IsRecurring, e.RecurringExpenseId, e.Notes,
        db.Expenses.Any(other => other.Id != e.Id && other.CancelledAt == null && other.ExpenseCategoryId == e.ExpenseCategoryId &&
            other.Amount == e.Amount && other.ExpenseDate == e.ExpenseDate &&
            (other.VehicleId == e.VehicleId || (other.VehicleId == null && e.VehicleId == null))),
        e.CancelledAt == null && currentUser.HasPermission(Permissions.Finance.Edit),
        e.CancelledAt == null && currentUser.HasPermission(Permissions.Finance.Cancel),
        e.CancelledAt == null && currentUser.HasPermission(Permissions.Finance.RegisterPayment),
        e.CancelledAt, e.CancellationReason);

    private async Task<Expense> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Expenses.SingleOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new NotFoundException("Despesa não encontrada. Ela pode ter sido excluída.");
}
