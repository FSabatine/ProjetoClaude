using Fleet.Application.Common;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Operations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Operations;

public sealed class HistoryRequest : ListRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public OperationalEventType? Type { get; set; }
    /// <summary>Area of the event (maintenance, fuel, tires, finance, documents, operations).</summary>
    public FleetAlertCategory? Category { get; set; }
}

public sealed record HistoryEntryResponse(
    long Id,
    OperationalEventType Type,
    DateTime OccurredAt,
    string Summary,
    string? UserName,
    string SubjectType,
    Guid SubjectId,
    Guid? VehicleId,
    Guid? DriverId,
    FleetAlertCategory Category);

public sealed class HistoryRequestValidator : AbstractValidator<HistoryRequest>
{
    public HistoryRequestValidator() => this.ValidPeriod(x => x.From, x => x.To, "to");
}

/// <summary>
/// Unified timeline of a vehicle, driver or tire, newest first, read from the operational events (ADR-025). Each event
/// is shown only to who can see its module (the same audience map as the alerts, ADR-045) — a vehicle viewer without
/// finance access does not read expense entries in the vehicle history.
/// </summary>
public sealed class OperationalHistoryService(IFleetDbContext db, IClock clock, ICurrentUser currentUser, IValidator<HistoryRequest> validator)
{
    public IReadOnlyList<OperationalEventType> VisibleTypes() =>
        Enum.GetValues<OperationalEventType>()
            .Where(t => AlertAudiences.RequiredPermissions(AutomationTriggerCatalog.EventAudience(t)).All(currentUser.HasPermission))
            .ToList();

    public async Task<PagedResult<HistoryEntryResponse>> ForVehicleAsync(Guid vehicleId, HistoryRequest request, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
        return await ListAsync(db.OperationalEvents.Where(e => e.VehicleId == vehicleId), request, ct);
    }

    public async Task<PagedResult<HistoryEntryResponse>> ForDriverAsync(Guid driverId, HistoryRequest request, CancellationToken ct)
    {
        if (!await db.Drivers.AnyAsync(d => d.Id == driverId, ct))
            throw new NotFoundException("Motorista não encontrado. Ele pode ter sido excluído.");
        return await ListAsync(db.OperationalEvents.Where(e => e.DriverId == driverId), request, ct);
    }

    /// <summary>Lifecycle timeline of a tire (Phase 5): every tire event carries TireId.</summary>
    public async Task<PagedResult<HistoryEntryResponse>> ForTireAsync(Guid tireId, HistoryRequest request, CancellationToken ct)
    {
        if (!await db.Tires.AnyAsync(t => t.Id == tireId, ct))
            throw new NotFoundException("Pneu não encontrado. Ele pode ter sido excluído.");
        return await ListAsync(db.OperationalEvents.Where(e => e.TireId == tireId), request, ct);
    }

    private async Task<PagedResult<HistoryEntryResponse>> ListAsync(IQueryable<OperationalEvent> query, HistoryRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        if (request.From is { } from)
        {
            var start = clock.StartOfBusinessDayUtc(from);
            query = query.Where(e => e.OccurredAt >= start);
        }
        if (request.To is { } to)
        {
            var end = clock.StartOfBusinessDayUtc(to.AddDays(1));
            query = query.Where(e => e.OccurredAt < end);
        }
        if (request.Type is { } type) query = query.Where(e => e.Type == type);
        var types = VisibleTypes();
        if (request.Category is { } category) types = types.Where(t => AutomationTriggerCatalog.EventCategory(t) == category).ToList();
        query = query.Where(e => types.Contains(e.Type));

        var page = await query.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .ToPagedResultAsync(request, e => e, ct);
        var names = await UserNames.LoadAsync(db, page.Items.Select(e => e.UserId), ct);
        var items = page.Items.Select(e => new HistoryEntryResponse(
            e.Id, e.Type, e.OccurredAt, e.Summary, names.Get(e.UserId), e.SubjectType, e.SubjectId, e.VehicleId, e.DriverId,
            AutomationTriggerCatalog.EventCategory(e.Type))).ToList();
        return new PagedResult<HistoryEntryResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
