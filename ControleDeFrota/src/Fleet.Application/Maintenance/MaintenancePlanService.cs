using Fleet.Application.Common;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Maintenance;

public sealed record MaintenancePlanItemRequest
{
    /// <summary>Existing item id to keep it (and its MaintenanceSchedule history) stable across edits; null for a new item.</summary>
    public Guid? Id { get; init; }
    public string? ServiceName { get; init; }
    public int? IntervalKm { get; init; }
    public int? IntervalMonths { get; init; }
    public decimal? IntervalHours { get; init; }
    public int? GraceKm { get; init; }
    public int? GraceDays { get; init; }
    public decimal? GraceHours { get; init; }
    public MaintenancePriority Priority { get; init; } = MaintenancePriority.Medium;
    public int? EstimatedDurationMinutes { get; init; }
    public decimal? EstimatedCost { get; init; }
    public bool IsRequired { get; init; } = true;
    public string? Notes { get; init; }
}

public sealed record MaintenancePlanRequest
{
    public string? Name { get; init; }
    /// <summary>Overrides the type/default plan for this one vehicle.</summary>
    public Guid? VehicleId { get; init; }
    /// <summary>Overrides the default plan for every vehicle of this type. Ignored when VehicleId is set.</summary>
    public VehicleType? VehicleType { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyList<MaintenancePlanItemRequest> Items { get; init; } = [];
}

public sealed record MaintenancePlanItemResponse(
    Guid Id, string ServiceName, int? IntervalKm, int? IntervalMonths, decimal? IntervalHours,
    int? GraceKm, int? GraceDays, decimal? GraceHours, MaintenancePriority Priority,
    int? EstimatedDurationMinutes, decimal? EstimatedCost, bool IsRequired, string? Notes);

public sealed record MaintenancePlanListItemResponse(
    Guid Id, string Name, Guid? VehicleId, string? VehicleLicensePlate, VehicleType? VehicleType, bool IsActive, int ItemCount);

public sealed record MaintenancePlanResponse(
    Guid Id, string Name, Guid? VehicleId, string? VehicleLicensePlate, VehicleType? VehicleType, bool IsActive,
    IReadOnlyList<MaintenancePlanItemResponse> Items, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed class MaintenancePlanRequestValidator : AbstractValidator<MaintenancePlanRequest>
{
    public MaintenancePlanRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(MaintenancePlan.NameMaxLength);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Inclua pelo menos um item no plano.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ServiceName).Required("Serviço").MaxLen(MaintenancePlanItem.ServiceNameMaxLength);
            item.RuleFor(i => i)
                .Must(i => i.IntervalKm is not null || i.IntervalMonths is not null || i.IntervalHours is not null)
                .WithMessage("Informe pelo menos um intervalo (km, meses ou horas).");
            item.RuleFor(i => i.IntervalKm).GreaterThan(0).When(i => i.IntervalKm is not null).WithMessage("Intervalo em km deve ser maior que zero.");
            item.RuleFor(i => i.IntervalMonths).GreaterThan(0).When(i => i.IntervalMonths is not null).WithMessage("Intervalo em meses deve ser maior que zero.");
            item.RuleFor(i => i.IntervalHours).GreaterThan(0).When(i => i.IntervalHours is not null).WithMessage("Intervalo em horas deve ser maior que zero.");
            item.RuleFor(i => i.EstimatedCost).NonNegative("Custo estimado");
            item.RuleFor(i => i.Notes).MaxLen(MaintenancePlanItem.NotesMaxLength);
        });
    }
}

/// <summary>
/// Configurable preventive maintenance plans (seção 5/8). Resolving which plan applies to a vehicle is
/// <see cref="MaintenancePlanResolver"/> — this service only owns CRUD.
/// </summary>
public sealed class MaintenancePlanService(IFleetDbContext db, IValidator<MaintenancePlanRequest> validator)
{
    public async Task<IReadOnlyList<MaintenancePlanListItemResponse>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.MaintenancePlans.Include(p => p.Vehicle).AsQueryable();
        if (!includeInactive) query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.Name)
            .Select(p => new MaintenancePlanListItemResponse(
                p.Id, p.Name, p.VehicleId, p.Vehicle != null ? p.Vehicle.LicensePlate : null, p.VehicleType, p.IsActive, p.Items.Count))
            .ToListAsync(ct);
    }

    public async Task<MaintenancePlanResponse> GetAsync(Guid id, CancellationToken ct) => ToResponse(await LoadAsync(id, ct));

    public async Task<MaintenancePlanResponse> CreateAsync(MaintenancePlanRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var plan = new MaintenancePlan();
        await ApplyAsync(request, plan, ct);
        plan.Items = request.Items.Select(NewItem).ToList();
        db.MaintenancePlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return ToResponse(plan);
    }

    public async Task<MaintenancePlanResponse> UpdateAsync(Guid id, MaintenancePlanRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var plan = await LoadAsync(id, ct);
        await ApplyAsync(request, plan, ct);
        await ReplaceItemsAsync(plan, request.Items, ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(plan);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var plan = await LoadAsync(id, ct);
        db.MaintenancePlans.Remove(plan);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(MaintenancePlanRequest request, MaintenancePlan plan, CancellationToken ct)
    {
        if (request.VehicleId is { } vehicleId && !await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw ValidationErrors.ForField("vehicleId", "Veículo não encontrado. Selecione um veículo da lista.");

        plan.Name = request.Name!.Trim();
        plan.VehicleId = request.VehicleId;
        plan.VehicleType = request.VehicleId is null ? request.VehicleType : null;
        plan.IsActive = request.IsActive;
    }

    private async Task ReplaceItemsAsync(MaintenancePlan plan, IReadOnlyList<MaintenancePlanItemRequest> items, CancellationToken ct)
    {
        var existing = plan.Items.ToDictionary(i => i.Id);
        var requestedIds = items.Where(i => i.Id is not null).Select(i => i.Id!.Value).ToHashSet();
        var removedIds = existing.Keys.Where(id => !requestedIds.Contains(id)).ToList();
        if (removedIds.Count > 0)
        {
            var inUse = await db.MaintenanceSchedules.AnyAsync(s => removedIds.Contains(s.MaintenancePlanItemId), ct) ||
                        await db.WorkOrders.SelectMany(w => w.Items).AnyAsync(i => i.MaintenancePlanItemId != null && removedIds.Contains(i.MaintenancePlanItemId.Value), ct);
            if (inUse)
                throw new BusinessRuleException("Um dos itens removidos já tem manutenções registradas. Deixe-o inativo em vez de removê-lo (defina um intervalo bem longo).");
        }

        var kept = new List<MaintenancePlanItem>();
        foreach (var request in items)
        {
            if (request.Id is { } itemId && existing.Remove(itemId, out var item))
            {
                Fill(item, request);
                kept.Add(item);
            }
            else
            {
                kept.Add(NewItem(request));
            }
        }
        foreach (var removed in existing.Values) plan.Items.Remove(removed);
        foreach (var added in kept.Where(i => !plan.Items.Contains(i))) plan.Items.Add(added);
    }

    private static MaintenancePlanItem NewItem(MaintenancePlanItemRequest request)
    {
        var item = new MaintenancePlanItem();
        Fill(item, request);
        return item;
    }

    private static void Fill(MaintenancePlanItem item, MaintenancePlanItemRequest request)
    {
        item.ServiceName = request.ServiceName!.Trim();
        item.IntervalKm = request.IntervalKm;
        item.IntervalMonths = request.IntervalMonths;
        item.IntervalHours = request.IntervalHours;
        item.GraceKm = request.GraceKm;
        item.GraceDays = request.GraceDays;
        item.GraceHours = request.GraceHours;
        item.Priority = request.Priority;
        item.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        item.EstimatedCost = request.EstimatedCost;
        item.IsRequired = request.IsRequired;
        item.Notes = request.Notes.TrimToNull();
    }

    private async Task<MaintenancePlan> LoadAsync(Guid id, CancellationToken ct) =>
        await db.MaintenancePlans.Include(p => p.Items).Include(p => p.Vehicle).SingleOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException("Plano de manutenção não encontrado. Ele pode ter sido excluído.");

    private static MaintenancePlanResponse ToResponse(MaintenancePlan p) => new(
        p.Id, p.Name, p.VehicleId, p.Vehicle?.LicensePlate, p.VehicleType, p.IsActive,
        p.Items.Select(i => new MaintenancePlanItemResponse(
            i.Id, i.ServiceName, i.IntervalKm, i.IntervalMonths, i.IntervalHours, i.GraceKm, i.GraceDays, i.GraceHours,
            i.Priority, i.EstimatedDurationMinutes, i.EstimatedCost, i.IsRequired, i.Notes)).ToList(),
        p.CreatedAt, p.UpdatedAt);
}
