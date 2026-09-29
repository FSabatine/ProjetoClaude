using Fleet.Application.Common;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Vehicles;

/// <summary>Vehicles are tenant-scoped: EF filters every query by the caller's company.</summary>
public sealed class VehicleService(IFleetDbContext db, IValidator<VehicleRequest> validator)
{
    private static readonly SortMap<Vehicle> Sorts = new SortMap<Vehicle>("licensePlate")
        .Add("licensePlate", v => v.LicensePlate)
        .Add("model", v => v.Model)
        .Add("manufacturer", v => v.Manufacturer)
        .Add("modelYear", v => v.ModelYear)
        .Add("type", v => v.Type)
        .Add("currentOdometerKm", v => v.CurrentOdometerKm)
        .Add("status", v => v.Status)
        .Add("createdAt", v => v.CreatedAt);

    public async Task<PagedResult<VehicleListItemResponse>> ListAsync(VehicleListRequest request, CancellationToken ct)
    {
        var query = db.Vehicles.AsQueryable();
        if (request.Status is { } status) query = query.Where(v => v.Status == status);
        if (request.Type is { } type) query = query.Where(v => v.Type == type);
        if (request.SearchTerm is { } term)
        {
            var identifier = LicensePlate.Normalize(term);
            query = query.Where(v => v.LicensePlate.Contains(identifier) || v.Model.Contains(term) ||
                                     v.Manufacturer.Contains(term) || v.Renavam.Contains(identifier) ||
                                     v.Chassis.Contains(identifier));
        }

        return await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, v =>
            new VehicleListItemResponse(
                v.Id, v.LicensePlate, v.Manufacturer, v.Model, v.ModelYear, v.Type, v.CurrentOdometerKm, v.Status), ct);
    }

    public async Task<VehicleResponse> GetAsync(Guid id, CancellationToken ct) => ToResponse(await LoadAsync(id, ct));

    public async Task<VehicleResponse> CreateAsync(VehicleRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var vehicle = new Vehicle();
        await ApplyAsync(request, vehicle, ct);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        return ToResponse(vehicle);
    }

    public async Task<VehicleResponse> UpdateAsync(Guid id, VehicleRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var vehicle = await LoadAsync(id, ct);
        await ApplyAsync(request, vehicle, ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(vehicle);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await LoadAsync(id, ct);
        if (vehicle.Status == VehicleStatus.OnTrip)
            throw new BusinessRuleException("Não é possível excluir um veículo em viagem. Altere a situação do veículo antes de excluí-lo.");
        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Vehicle> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Vehicles.SingleOrDefaultAsync(v => v.Id == id, ct)
        ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");

    private async Task ApplyAsync(VehicleRequest request, Vehicle vehicle, CancellationToken ct)
    {
        var plate = LicensePlate.Normalize(request.LicensePlate);
        var renavam = Renavam.Normalize(request.Renavam);
        var chassis = Chassis.Normalize(request.Chassis);

        await RegisteredAssetRules.EnsurePlateIsFreeAsync(db, plate, vehicle.Id, exceptImplementId: null, ct);
        if (await db.Vehicles.AnyAsync(v => v.Renavam == renavam && v.Id != vehicle.Id, ct))
            throw new ConflictException("Já existe um veículo com este RENAVAM nesta empresa.", "renavam");
        if (await db.Vehicles.AnyAsync(v => v.Chassis == chassis && v.Id != vehicle.Id, ct))
            throw new ConflictException("Já existe um veículo com este chassi nesta empresa.", "chassis");

        vehicle.LicensePlate = plate;
        vehicle.Renavam = renavam;
        vehicle.Chassis = chassis;
        vehicle.Manufacturer = request.Manufacturer!.Trim();
        vehicle.Model = request.Model!.Trim();
        vehicle.ManufacturingYear = request.ManufacturingYear!.Value;
        vehicle.ModelYear = request.ModelYear!.Value;
        vehicle.Color = request.Color.TrimToNull();
        vehicle.Type = request.Type!.Value;
        vehicle.Category = request.Category;
        vehicle.FuelType = request.FuelType!.Value;
        vehicle.CargoCapacityKg = request.CargoCapacityKg;
        vehicle.TareWeightKg = request.TareWeightKg;
        // ADR-009 / DOMAIN: odometer may be corrected freely until reading history arrives (Phase 2).
        vehicle.CurrentOdometerKm = request.CurrentOdometerKm!.Value;
        vehicle.HourMeter = request.HourMeter;
        vehicle.Status = request.Status;
        vehicle.AcquisitionDate = request.AcquisitionDate;
        vehicle.AcquisitionValue = request.AcquisitionValue;
        vehicle.Notes = request.Notes.TrimToNull();
    }

    private static VehicleResponse ToResponse(Vehicle v) => new(
        v.Id, v.LicensePlate, v.Renavam, v.Chassis, v.Manufacturer, v.Model, v.ManufacturingYear, v.ModelYear,
        v.Color, v.Type, v.Category, v.FuelType, v.CargoCapacityKg, v.TareWeightKg, v.CurrentOdometerKm,
        v.HourMeter, v.Status, v.AcquisitionDate, v.AcquisitionValue, v.Notes, v.CreatedAt, v.UpdatedAt);
}
