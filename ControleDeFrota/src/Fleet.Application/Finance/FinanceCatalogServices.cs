using Fleet.Application.Common;
using Fleet.Domain.Common;
using Fleet.Domain.Finance;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Finance;

// ---------- Cost centers ----------

public sealed record CostCenterRequest
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public Guid? ParentCostCenterId { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record CostCenterResponse(
    Guid Id, string Code, string Name, string? Description, Guid? ParentCostCenterId, string? ParentCostCenterName, bool IsActive);

public sealed class CostCenterRequestValidator : AbstractValidator<CostCenterRequest>
{
    public CostCenterRequestValidator()
    {
        RuleFor(x => x.Code).Required("Código").MaxLen(CostCenter.CodeMaxLength);
        RuleFor(x => x.Name).Required("Nome").MaxLen(CostCenter.NameMaxLength);
        RuleFor(x => x.Description).MaxLen(CostCenter.DescriptionMaxLength);
    }
}

/// <summary>Configurable, hierarchical cost centers (ADR-040) — a "Branch" is just a leaf cost center, no separate entity.</summary>
public sealed class CostCenterService(IFleetDbContext db, IValidator<CostCenterRequest> validator)
{
    public async Task<IReadOnlyList<CostCenterResponse>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.CostCenters.AsQueryable();
        if (!includeInactive) query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.Code).Select(Projection()).ToListAsync(ct);
    }

    public async Task<CostCenterResponse> CreateAsync(CostCenterRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var center = new CostCenter();
        await ApplyAsync(request, center, ct);
        db.CostCenters.Add(center);
        await db.SaveChangesAsync(ct);
        return await GetAsync(center.Id, ct);
    }

    public async Task<CostCenterResponse> UpdateAsync(Guid id, CostCenterRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var center = await LoadAsync(id, ct);
        if (request.ParentCostCenterId == id)
            throw new BusinessRuleException("Um centro de custo não pode ser o próprio pai.");
        await ApplyAsync(request, center, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var center = await LoadAsync(id, ct);
        if (await db.CostCenters.AnyAsync(c => c.ParentCostCenterId == id, ct))
            throw new BusinessRuleException("Este centro de custo tem centros de custo filhos e não pode ser excluído.");
        if (await IsInUseAsync(id, ct))
            throw new BusinessRuleException("Este centro de custo está em uso (despesas, orçamentos ou recorrentes) e não pode ser excluído. Inative-o.");
        db.CostCenters.Remove(center);
        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> IsInUseAsync(Guid id, CancellationToken ct) =>
        await db.Expenses.AnyAsync(e => e.CostCenterId == id, ct) ||
        await db.Budgets.AnyAsync(b => b.CostCenterId == id, ct) ||
        await db.RecurringExpenses.AnyAsync(r => r.CostCenterId == id, ct);

    private async Task ApplyAsync(CostCenterRequest request, CostCenter center, CancellationToken ct)
    {
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await db.CostCenters.AnyAsync(c => c.Code == code && c.Id != center.Id, ct))
            throw new ConflictException("Já existe um centro de custo com este código.", "code");
        if (request.ParentCostCenterId is { } parentId && !await db.CostCenters.AnyAsync(c => c.Id == parentId, ct))
            throw new NotFoundException("Centro de custo pai não encontrado.");
        center.Code = code;
        center.Name = request.Name!.Trim();
        center.Description = request.Description.TrimToNull();
        center.ParentCostCenterId = request.ParentCostCenterId;
        center.IsActive = request.IsActive;
    }

    private System.Linq.Expressions.Expression<Func<CostCenter, CostCenterResponse>> Projection() => c =>
        new CostCenterResponse(c.Id, c.Code, c.Name, c.Description, c.ParentCostCenterId,
            c.ParentCostCenter == null ? null : c.ParentCostCenter.Name, c.IsActive);

    private async Task<CostCenterResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.CostCenters.Where(c => c.Id == id).Select(Projection()).SingleAsync(ct);

    private async Task<CostCenter> LoadAsync(Guid id, CancellationToken ct) =>
        await db.CostCenters.SingleOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Centro de custo não encontrado. Ele pode ter sido excluído.");
}

// ---------- Expense categories ----------

public sealed record ExpenseCategoryRequest
{
    public string? Name { get; init; }
    public string? Code { get; init; }
    public string? Description { get; init; }
    public Guid? ParentCategoryId { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record ExpenseCategoryResponse(
    Guid Id, string Name, string Code, string? Description, Guid? ParentCategoryId, string? ParentCategoryName,
    bool IsActive, bool IsSystemCategory);

public sealed class ExpenseCategoryRequestValidator : AbstractValidator<ExpenseCategoryRequest>
{
    public ExpenseCategoryRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(ExpenseCategory.NameMaxLength);
        RuleFor(x => x.Code).Required("Código").MaxLen(ExpenseCategory.CodeMaxLength)
            .Matches("^[A-Za-z0-9-]*$").WithMessage("Código: use apenas letras, números e hífen.");
        RuleFor(x => x.Description).MaxLen(ExpenseCategory.DescriptionMaxLength);
    }
}

/// <summary>
/// Configurable per company (ADR-040). Fuel/Maintenance/Tires are system categories, seeded once, read-only —
/// they are fed by CostAggregationService directly from the Fuel/Maintenance/Tires tables, never by a manual expense.
/// </summary>
public sealed class ExpenseCategoryService(IFleetDbContext db, ICurrentUser currentUser, IValidator<ExpenseCategoryRequest> validator)
{
    public async Task<IReadOnlyList<ExpenseCategoryResponse>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);
        var query = db.ExpenseCategories.AsQueryable();
        if (!includeInactive) query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.ParentCategoryId == null ? 0 : 1).ThenBy(c => c.Name).Select(Projection()).ToListAsync(ct);
    }

    public async Task<ExpenseCategoryResponse> CreateAsync(ExpenseCategoryRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var category = new ExpenseCategory();
        await ApplyAsync(request, category, ct);
        db.ExpenseCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return await GetAsync(category.Id, ct);
    }

    public async Task<ExpenseCategoryResponse> UpdateAsync(Guid id, ExpenseCategoryRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var category = await LoadAsync(id, ct);
        if (request.ParentCategoryId == id)
            throw new BusinessRuleException("Uma categoria não pode ser a própria categoria-mãe.");
        if (category.IsSystemCategory && !request.IsActive)
            throw new BusinessRuleException("Esta categoria é alimentada automaticamente por outro módulo e não pode ser inativada.");
        await ApplyAsync(request, category, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var category = await LoadAsync(id, ct);
        if (category.IsSystemCategory)
            throw new BusinessRuleException("Esta categoria é alimentada automaticamente por outro módulo (combustível, manutenção ou pneus) e não pode ser excluída.");
        if (await db.ExpenseCategories.AnyAsync(c => c.ParentCategoryId == id, ct))
            throw new BusinessRuleException("Esta categoria tem subcategorias e não pode ser excluída.");
        if (await db.Expenses.AnyAsync(e => e.ExpenseCategoryId == id, ct) ||
            await db.Budgets.AnyAsync(b => b.ExpenseCategoryId == id, ct) ||
            await db.RecurringExpenses.AnyAsync(r => r.ExpenseCategoryId == id, ct))
            throw new BusinessRuleException("Esta categoria está em uso (despesas, orçamentos ou recorrentes) e não pode ser excluída. Inative-a.");
        db.ExpenseCategories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Every company starts with the default catalog the first time it is read (same pattern as FuelTypeService).</summary>
    public async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        var companyId = currentUser.CompanyId;
        if (await db.ExpenseCategories.IgnoreQueryFilters().AnyAsync(c => c.CompanyId == companyId, ct)) return;
        db.ExpenseCategories.AddRange(CreateDefaults(companyId));
        await db.SaveChangesAsync(ct);
    }

    public static IReadOnlyList<ExpenseCategory> CreateDefaults(Guid? companyId = null)
    {
        var byCode = new Dictionary<string, ExpenseCategory>();
        var result = new List<ExpenseCategory>();
        foreach (var d in ExpenseCategoryDefaults.All)
        {
            var category = new ExpenseCategory
            {
                Id = Guid.NewGuid(), CompanyId = companyId ?? Guid.Empty, Name = d.Name, Code = d.Code,
                IsSystemCategory = d.IsSystem, CostAggregationKey = d.Source,
                ParentCategoryId = d.ParentCode is null ? null : byCode[d.ParentCode].Id,
            };
            byCode[d.Code] = category;
            result.Add(category);
        }
        return result;
    }

    private async Task ApplyAsync(ExpenseCategoryRequest request, ExpenseCategory category, CancellationToken ct)
    {
        var name = request.Name!.Trim();
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await db.ExpenseCategories.AnyAsync(c => c.Code == code && c.Id != category.Id, ct))
            throw new ConflictException("Já existe uma categoria com este código.", "code");
        if (request.ParentCategoryId is { } parentId && !await db.ExpenseCategories.AnyAsync(c => c.Id == parentId, ct))
            throw new NotFoundException("Categoria-mãe não encontrada.");
        category.Name = name;
        category.Code = code;
        category.Description = request.Description.TrimToNull();
        category.ParentCategoryId = request.ParentCategoryId;
        if (!category.IsSystemCategory) category.IsActive = request.IsActive;
    }

    private System.Linq.Expressions.Expression<Func<ExpenseCategory, ExpenseCategoryResponse>> Projection() => c =>
        new ExpenseCategoryResponse(c.Id, c.Name, c.Code, c.Description, c.ParentCategoryId,
            c.ParentCategory == null ? null : c.ParentCategory.Name, c.IsActive, c.IsSystemCategory);

    private async Task<ExpenseCategoryResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.ExpenseCategories.Where(c => c.Id == id).Select(Projection()).SingleAsync(ct);

    private async Task<ExpenseCategory> LoadAsync(Guid id, CancellationToken ct) =>
        await db.ExpenseCategories.SingleOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Categoria de despesa não encontrada. Ela pode ter sido excluída.");
}
