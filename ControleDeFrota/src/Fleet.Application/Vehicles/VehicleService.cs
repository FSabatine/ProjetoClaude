using Fleet.Application.Assignments;
using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Mileage;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Vehicles;

/// <summary>Vehicles are tenant-scoped: EF filters every query by the caller's company.</summary>
public sealed class VehicleService(IFleetDbContext db, IClock clock, OperationalEventLog events, IValidator<VehicleRequest> validator)
{
    private static readonly SortMap<Vehicle> Sorts = new SortMap<Vehicle>("licensePlate")
        .Add("licensePlate", v => v.LicensePlate)
        .Add("model", v => v.Model)
        .Add("manufacturer", v => v.Manufacturer)
        .Add("modelYear", v => v.ModelYear)
        .Add("type", v => v.Type)
        .Add("currentOdometerKm", v => v.CurrentOdometerKm)
        .Add("odometerUpdatedAt", v => v.OdometerUpdatedAt)
        .Add("status", v => v.Status)
        .Add("createdAt", v => v.CreatedAt);

    public async Task<PagedResult<VehicleListItemResponse>> ListAsync(VehicleListRequest request, CancellationToken ct)
    {
        var active = AssignmentService.Active(db);
        var query = db.Vehicles.AsQueryable();
        if (request.Status is { } status) query = query.Where(v => v.Status == status);
        if (request.OperationalStatus is { } operational) query = WhereOperationalStatus(query, operational);
        if (request.Type is { } type) query = query.Where(v => v.Type == type);
        if (request.DriverId is { } driverId) query = query.Where(v => active.Any(a => a.VehicleId == v.Id && a.DriverId == driverId));
        if (request.MinOdometerKm is { } min) query = query.Where(v => v.CurrentOdometerKm >= min);
        if (request.MaxOdometerKm is { } max) query = query.Where(v => v.CurrentOdometerKm <= max);
        if (request.StaleMileage == true) query = WhereStaleMileage(query, clock.UtcNow);
        if (request.SearchTerm is { } term)
        {
            var identifier = LicensePlate.Normalize(term);
            query = query.Where(v => v.LicensePlate.Contains(identifier) || v.Model.Contains(term) ||
                                     v.Manufacturer.Contains(term) || v.Renavam.Contains(identifier) ||
                                     v.Chassis.Contains(identifier) ||
                                     active.Any(a => a.VehicleId == v.Id && a.Driver.FullName.Contains(term)));
        }

        var page = await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, v => new
        {
            v.Id, v.LicensePlate, v.Manufacturer, v.Model, v.ModelYear, v.Type, v.CurrentOdometerKm, v.OdometerUpdatedAt, v.Status,
            Driver = active.Where(a => a.VehicleId == v.Id).Select(a => new { a.DriverId, a.Driver.FullName }).FirstOrDefault(),
        }, ct);

        var items = page.Items.Select(v => new VehicleListItemResponse(
            v.Id, v.LicensePlate, v.Manufacturer, v.Model, v.ModelYear, v.Type, v.CurrentOdometerKm, v.OdometerUpdatedAt, v.Status,
            VehicleOperationalState.From(v.Status, v.Driver is not null), v.Driver?.DriverId, v.Driver?.FullName)).ToList();
        return new PagedResult<VehicleListItemResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<VehicleResponse> GetAsync(Guid id, CancellationToken ct) => await ToResponseAsync(await LoadAsync(id, ct), ct);

    public async Task<VehicleResponse> CreateAsync(VehicleRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        if (request.CurrentOdometerKm is null)
            throw ValidationErrors.ForField("currentOdometerKm", "Hodômetro: campo obrigatório (informe 0 para veículo novo).");

        var vehicle = new Vehicle();
        await ApplyAsync(request, vehicle, ct);
        vehicle.CurrentOdometerKm = request.CurrentOdometerKm.Value;
        vehicle.OdometerUpdatedAt = clock.UtcNow;
        db.Vehicles.Add(vehicle);
        // The first entry of the odometer history: later readings are validated against it (ADR-019).
        db.OdometerReadings.Add(new OdometerReading
        {
            VehicleId = vehicle.Id,
            OdometerKm = vehicle.CurrentOdometerKm,
            ReadAt = vehicle.OdometerUpdatedAt.Value,
            Source = OdometerReadingSource.Registration,
        });
        if (request.HourMeter is { } hourMeter)
        {
            vehicle.HourMeter = hourMeter;
            vehicle.HourMeterUpdatedAt = clock.UtcNow;
            // Same idea as the odometer: the first entry of the hour-meter history (ADR-027).
            db.HourMeterReadings.Add(new HourMeterReading
            {
                VehicleId = vehicle.Id,
                Hours = hourMeter,
                ReadAt = vehicle.HourMeterUpdatedAt.Value,
                Source = HourMeterReadingSource.Registration,
            });
        }
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(vehicle, ct);
    }

    public async Task<VehicleResponse> UpdateAsync(Guid id, VehicleRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var vehicle = await LoadAsync(id, ct);
        if (request.CurrentOdometerKm is { } odometer && odometer != vehicle.CurrentOdometerKm)
            throw ValidationErrors.ForField("currentOdometerKm",
                "O hodômetro é atualizado pelo registro de leituras, que valida e guarda o histórico. Use \"Registrar leitura\" na aba Quilometragem.");
        if (request.HourMeter is { } requestedHourMeter && requestedHourMeter != vehicle.HourMeter)
            throw ValidationErrors.ForField("hourMeter",
                "O horímetro é atualizado pelo registro de leituras, que valida e guarda o histórico. Use a aba Manutenção.");

        var previousStatus = vehicle.Status;
        if (request.Status == VehicleStatus.Inactive && previousStatus != VehicleStatus.Inactive &&
            await AssignmentService.Active(db).AnyAsync(a => a.VehicleId == id, ct))
            throw new BusinessRuleException("Este veículo tem um motorista alocado. Encerre a alocação antes de inativar o veículo.");

        await ApplyAsync(request, vehicle, ct);
        if (vehicle.Status != previousStatus)
        {
            events.Record(OperationalEventType.VehicleStatusChanged, new EventSubject(nameof(Vehicle), vehicle.Id, vehicle.Id),
                $"Situação do veículo {LicensePlate.Format(vehicle.LicensePlate)} alterada de {StatusText(previousStatus)} para {StatusText(vehicle.Status)}.",
                new { from = previousStatus, to = vehicle.Status });
        }
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(vehicle, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await LoadAsync(id, ct);
        if (vehicle.Status == VehicleStatus.OnTrip)
            throw new BusinessRuleException("Não é possível excluir um veículo em viagem. Altere a situação do veículo antes de excluí-lo.");
        // Deleting is for wrong registrations. A vehicle with operational history is real: it is inactivated instead,
        // so its history stays consistent and reportable.
        if (await db.VehicleAssignments.AnyAsync(a => a.VehicleId == id, ct) ||
            await db.ChecklistExecutions.AnyAsync(e => e.VehicleId == id, ct) ||
            await db.Occurrences.AnyAsync(o => o.VehicleId == id, ct))
            throw new BusinessRuleException("Este veículo tem histórico operacional (alocações, checklists ou ocorrências) e não pode ser excluído. Inative-o.");

        foreach (var document in await db.Documents.Where(d => d.VehicleId == id).ToListAsync(ct)) db.Documents.Remove(document);
        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>SQL form of <see cref="VehicleOperationalState.From"/> — keep both in sync (tests cover each status).</summary>
    public static IQueryable<Vehicle> WhereOperationalStatus(IQueryable<Vehicle> query, VehicleOperationalStatus status, IQueryable<Fleet.Domain.Assignments.VehicleAssignment> active) =>
        status switch
        {
            VehicleOperationalStatus.Assigned => query.Where(v => v.Status == VehicleStatus.Available && active.Any(a => a.VehicleId == v.Id)),
            VehicleOperationalStatus.Available => query.Where(v => v.Status == VehicleStatus.Available && !active.Any(a => a.VehicleId == v.Id)),
            VehicleOperationalStatus.OnTrip => query.Where(v => v.Status == VehicleStatus.OnTrip),
            VehicleOperationalStatus.Unavailable => query.Where(v => v.Status == VehicleStatus.Unavailable),
            VehicleOperationalStatus.UnderMaintenance => query.Where(v => v.Status == VehicleStatus.UnderMaintenance),
            _ => query.Where(v => v.Status == VehicleStatus.Inactive),
        };

    private IQueryable<Vehicle> WhereOperationalStatus(IQueryable<Vehicle> query, VehicleOperationalStatus status) =>
        WhereOperationalStatus(query, status, AssignmentService.Active(db));

    /// <summary>Active vehicles without an odometer update in the last OdometerPolicy.StaleAfterDays days.</summary>
    public static IQueryable<Vehicle> WhereStaleMileage(IQueryable<Vehicle> query, DateTime utcNow)
    {
        var limit = utcNow.AddDays(-OdometerPolicy.StaleAfterDays);
        return query.Where(v => v.Status != VehicleStatus.Inactive && (v.OdometerUpdatedAt == null || v.OdometerUpdatedAt < limit));
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
        vehicle.HourMeter = request.HourMeter;
        vehicle.Status = request.Status;
        vehicle.AcquisitionDate = request.AcquisitionDate;
        vehicle.AcquisitionValue = request.AcquisitionValue;
        vehicle.Notes = request.Notes.TrimToNull();
    }

    private async Task<VehicleResponse> ToResponseAsync(Vehicle v, CancellationToken ct)
    {
        var current = await AssignmentService.Active(db).Where(a => a.VehicleId == v.Id)
            .Select(a => new CurrentAssignmentResponse(a.Id, a.DriverId, a.Driver.FullName, a.StartedAt))
            .FirstOrDefaultAsync(ct);
        return new VehicleResponse(
            v.Id, v.LicensePlate, v.Renavam, v.Chassis, v.Manufacturer, v.Model, v.ManufacturingYear, v.ModelYear,
            v.Color, v.Type, v.Category, v.FuelType, v.CargoCapacityKg, v.TareWeightKg, v.CurrentOdometerKm, v.OdometerUpdatedAt,
            v.HourMeter, v.Status, VehicleOperationalState.From(v.Status, current is not null), current,
            v.AcquisitionDate, v.AcquisitionValue, v.Notes, v.CreatedAt, v.UpdatedAt);
    }

    private static string StatusText(VehicleStatus status) => status switch
    {
        VehicleStatus.Available => "Disponível",
        VehicleStatus.OnTrip => "Em viagem",
        VehicleStatus.UnderMaintenance => "Em manutenção",
        VehicleStatus.Unavailable => "Indisponível",
        _ => "Inativo",
    };
}
