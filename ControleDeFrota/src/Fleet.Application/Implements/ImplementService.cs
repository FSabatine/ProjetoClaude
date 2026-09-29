using Fleet.Application.Common;
using Fleet.Domain.Implements;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Implements;

/// <summary>Implements are tenant-scoped: EF filters every query by the caller's company.</summary>
public sealed class ImplementService(IFleetDbContext db, IValidator<ImplementRequest> validator)
{
    private static readonly SortMap<Implement> Sorts = new SortMap<Implement>("licensePlate")
        .Add("licensePlate", i => i.LicensePlate)
        .Add("model", i => i.Model)
        .Add("manufacturer", i => i.Manufacturer)
        .Add("modelYear", i => i.ModelYear)
        .Add("type", i => i.Type)
        .Add("status", i => i.Status)
        .Add("createdAt", i => i.CreatedAt);

    public async Task<PagedResult<ImplementListItemResponse>> ListAsync(ImplementListRequest request, CancellationToken ct)
    {
        var query = db.Implements.AsQueryable();
        if (request.Status is { } status) query = query.Where(i => i.Status == status);
        if (request.Type is { } type) query = query.Where(i => i.Type == type);
        if (request.SearchTerm is { } term)
        {
            var identifier = LicensePlate.Normalize(term);
            query = query.Where(i => i.LicensePlate.Contains(identifier) || i.Model.Contains(term) ||
                                     i.Manufacturer.Contains(term) || i.Renavam.Contains(identifier) ||
                                     i.Chassis.Contains(identifier));
        }

        return await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, i =>
            new ImplementListItemResponse(
                i.Id, i.LicensePlate, i.Manufacturer, i.Model, i.ModelYear, i.Type, i.Capacity, i.CapacityUnit, i.Status), ct);
    }

    public async Task<ImplementResponse> GetAsync(Guid id, CancellationToken ct) => ToResponse(await LoadAsync(id, ct));

    public async Task<ImplementResponse> CreateAsync(ImplementRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var implement = new Implement();
        await ApplyAsync(request, implement, ct);
        db.Implements.Add(implement);
        await db.SaveChangesAsync(ct);
        return ToResponse(implement);
    }

    public async Task<ImplementResponse> UpdateAsync(Guid id, ImplementRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var implement = await LoadAsync(id, ct);
        await ApplyAsync(request, implement, ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(implement);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var implement = await LoadAsync(id, ct);
        if (implement.Status == ImplementStatus.InUse)
            throw new BusinessRuleException("Não é possível excluir um implemento em uso. Altere a situação do implemento antes de excluí-lo.");
        db.Implements.Remove(implement);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Implement> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Implements.SingleOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new NotFoundException("Implemento não encontrado. Ele pode ter sido excluído.");

    private async Task ApplyAsync(ImplementRequest request, Implement implement, CancellationToken ct)
    {
        var plate = LicensePlate.Normalize(request.LicensePlate);
        var renavam = Renavam.Normalize(request.Renavam);
        var chassis = Chassis.Normalize(request.Chassis);

        await RegisteredAssetRules.EnsurePlateIsFreeAsync(db, plate, exceptVehicleId: null, implement.Id, ct);
        if (await db.Implements.AnyAsync(i => i.Renavam == renavam && i.Id != implement.Id, ct))
            throw new ConflictException("Já existe um implemento com este RENAVAM nesta empresa.", "renavam");
        if (await db.Implements.AnyAsync(i => i.Chassis == chassis && i.Id != implement.Id, ct))
            throw new ConflictException("Já existe um implemento com este chassi nesta empresa.", "chassis");

        implement.LicensePlate = plate;
        implement.Renavam = renavam;
        implement.Chassis = chassis;
        implement.Manufacturer = request.Manufacturer!.Trim();
        implement.Model = request.Model!.Trim();
        implement.ManufacturingYear = request.ManufacturingYear!.Value;
        implement.ModelYear = request.ModelYear!.Value;
        implement.Type = request.Type!.Value;
        implement.Capacity = request.Capacity;
        implement.CapacityUnit = request.Capacity is null ? null : request.CapacityUnit;
        implement.TareWeightKg = request.TareWeightKg;
        implement.Status = request.Status;
        implement.Notes = request.Notes.TrimToNull();
    }

    private static ImplementResponse ToResponse(Implement i) => new(
        i.Id, i.LicensePlate, i.Renavam, i.Chassis, i.Manufacturer, i.Model, i.ManufacturingYear, i.ModelYear,
        i.Type, i.Capacity, i.CapacityUnit, i.TareWeightKg, i.Status, i.Notes, i.CreatedAt, i.UpdatedAt);
}
