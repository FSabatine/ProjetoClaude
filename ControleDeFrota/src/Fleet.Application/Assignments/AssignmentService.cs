using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Assignments;
using Fleet.Domain.Drivers;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Assignments;

public sealed record AssignmentCreateRequest
{
    public Guid? DriverId { get; init; }
    /// <summary>Defaults to now. May be in the past (registering a hand-over late), never in the future.</summary>
    public DateTime? StartedAt { get; init; }
    /// <summary>
    /// Confirms ending the vehicle's current assignment and/or the driver's current one. Without it the request is
    /// refused with 409, so a hand-over never happens by accident.
    /// </summary>
    public bool EndCurrent { get; init; }
    public string? Notes { get; init; }
}

public sealed record AssignmentEndRequest
{
    public DateTime? EndedAt { get; init; }
    public string? Reason { get; init; }
}

public sealed record AssignmentResponse(
    Guid Id,
    Guid VehicleId,
    string LicensePlate,
    string VehicleDescription,
    Guid DriverId,
    string DriverName,
    DateTime StartedAt,
    DateTime? EndedAt,
    bool IsActive,
    string? Notes,
    string? EndReason,
    string? CreatedByName);

public sealed class AssignmentCreateRequestValidator : AbstractValidator<AssignmentCreateRequest>
{
    public AssignmentCreateRequestValidator(IClock clock)
    {
        RuleFor(x => x.DriverId).NotNull().WithMessage("Motorista: campo obrigatório.");
        RuleFor(x => x.StartedAt)
            .Must(d => d is null || d.Value <= clock.UtcNow.Add(AssignmentService.ClockTolerance))
            .WithMessage("A data de início não pode ser futura. Alocações agendadas não são suportadas.");
        RuleFor(x => x.Notes).MaxLen(VehicleAssignment.NotesMaxLength);
    }
}

public sealed class AssignmentEndRequestValidator : AbstractValidator<AssignmentEndRequest>
{
    public AssignmentEndRequestValidator(IClock clock)
    {
        RuleFor(x => x.EndedAt)
            .Must(d => d is null || d.Value <= clock.UtcNow.Add(AssignmentService.ClockTolerance))
            .WithMessage("A data de encerramento não pode ser futura.");
        RuleFor(x => x.Reason).MaxLen(VehicleAssignment.NotesMaxLength);
    }
}

/// <summary>Driver ↔ vehicle allocation with preserved history (ADR-020).</summary>
public sealed class AssignmentService(
    IFleetDbContext db,
    IClock clock,
    OperationalEventLog events,
    IValidator<AssignmentCreateRequest> createValidator,
    IValidator<AssignmentEndRequest> endValidator)
{
    /// <summary>Client clocks drift; a few minutes "in the future" is still "now".</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    public async Task<PagedResult<AssignmentResponse>> ListForVehicleAsync(Guid vehicleId, ListRequest request, CancellationToken ct)
    {
        await EnsureVehicleExistsAsync(vehicleId, ct);
        return await ListAsync(db.VehicleAssignments.Where(a => a.VehicleId == vehicleId), request, ct);
    }

    public async Task<PagedResult<AssignmentResponse>> ListForDriverAsync(Guid driverId, ListRequest request, CancellationToken ct)
    {
        if (!await db.Drivers.AnyAsync(d => d.Id == driverId, ct))
            throw new NotFoundException("Motorista não encontrado. Ele pode ter sido excluído.");
        return await ListAsync(db.VehicleAssignments.Where(a => a.DriverId == driverId), request, ct);
    }

    public async Task<AssignmentResponse> AssignAsync(Guid vehicleId, AssignmentCreateRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
        var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == request.DriverId, ct)
            ?? throw ValidationErrors.ForField("driverId", "Motorista não encontrado. Selecione um motorista da lista.");

        if (AssignmentRules.VehicleBlockReason(vehicle) is { } vehicleBlock) throw new BusinessRuleException(vehicleBlock);
        if (AssignmentRules.DriverBlockReason(driver, clock.Today) is { } driverBlock) throw new BusinessRuleException(driverBlock);

        var startedAt = Min(request.StartedAt ?? clock.UtcNow, clock.UtcNow);
        var currentOfVehicle = await ActiveQuery().SingleOrDefaultAsync(a => a.VehicleId == vehicleId, ct);
        var currentOfDriver = await ActiveQuery().SingleOrDefaultAsync(a => a.DriverId == driver.Id, ct);

        if (currentOfVehicle?.DriverId == driver.Id)
            throw new BusinessRuleException($"{driver.FullName} já é o motorista atual deste veículo.");

        var toEnd = new[] { currentOfVehicle, currentOfDriver }.OfType<VehicleAssignment>().ToList();
        if (toEnd.Count > 0 && !request.EndCurrent)
            throw new ConflictException(DescribeConflict(currentOfVehicle, currentOfDriver, driver));

        foreach (var current in toEnd.Where(c => startedAt < c.StartedAt))
            throw ValidationErrors.ForField("startedAt",
                $"A nova alocação precisa começar depois do início da alocação atual ({FormatDateTime(current.StartedAt)}).");
        await EnsureNoOverlapWithHistoryAsync(vehicleId, driver.Id, startedAt, ct);

        var assignment = new VehicleAssignment
        {
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            DriverId = driver.Id,
            Driver = driver,
            StartedAt = startedAt,
            Notes = request.Notes.TrimToNull(),
        };

        await db.InTransactionAsync(async () =>
        {
            foreach (var current in toEnd)
            {
                End(current, startedAt, current == currentOfVehicle
                    ? $"Substituído(a) por {driver.FullName}."
                    : $"Motorista passou para o veículo {Plate(vehicle)}.");
            }
            // The filtered unique indexes need the current rows ended before the new one is inserted.
            await db.SaveChangesAsync(ct);

            db.VehicleAssignments.Add(assignment);
            events.Record(OperationalEventType.VehicleAssigned, Subject(assignment),
                $"Motorista {driver.FullName} alocado(a) ao veículo {Plate(vehicle)}.",
                new { assignment.StartedAt });
            await db.SaveChangesAsync(ct);
        }, ct);

        return await GetAsync(assignment.Id, ct);
    }

    public async Task<AssignmentResponse> EndAsync(Guid assignmentId, AssignmentEndRequest request, CancellationToken ct)
    {
        await endValidator.ValidateAndThrowAsync(request, ct);
        var assignment = await db.VehicleAssignments.Include(a => a.Vehicle).Include(a => a.Driver)
            .SingleOrDefaultAsync(a => a.Id == assignmentId, ct)
            ?? throw new NotFoundException("Alocação não encontrada.");
        if (!assignment.IsActive)
            throw new BusinessRuleException("Esta alocação já foi encerrada.");

        var endedAt = Min(request.EndedAt ?? clock.UtcNow, clock.UtcNow);
        if (endedAt < assignment.StartedAt)
            throw ValidationErrors.ForField("endedAt",
                $"O encerramento não pode ser anterior ao início da alocação ({FormatDateTime(assignment.StartedAt)}).");

        End(assignment, endedAt, request.Reason.TrimToNull());
        await db.SaveChangesAsync(ct);
        return await GetAsync(assignment.Id, ct);
    }

    /// <summary>Active assignment rows — the source of "current driver" everywhere (lists, dashboard, rules).</summary>
    public static IQueryable<VehicleAssignment> Active(IFleetDbContext db) => db.VehicleAssignments.Where(a => a.EndedAt == null);

    private IQueryable<VehicleAssignment> ActiveQuery() => Active(db).Include(a => a.Vehicle).Include(a => a.Driver);

    private void End(VehicleAssignment assignment, DateTime endedAt, string? reason)
    {
        assignment.EndedAt = endedAt;
        assignment.EndReason = reason;
        events.Record(OperationalEventType.VehicleAssignmentEnded, Subject(assignment),
            $"Encerrada a alocação de {assignment.Driver.FullName} no veículo {Plate(assignment.Vehicle)}.",
            new { assignment.StartedAt, assignment.EndedAt, reason });
    }

    /// <summary>A late registration must not overlap a period that is already in the history.</summary>
    private async Task EnsureNoOverlapWithHistoryAsync(Guid vehicleId, Guid driverId, DateTime startedAt, CancellationToken ct)
    {
        var overlapping = await db.VehicleAssignments
            .Where(a => (a.VehicleId == vehicleId || a.DriverId == driverId) && a.EndedAt != null && a.EndedAt > startedAt)
            .OrderByDescending(a => a.EndedAt)
            .Select(a => new { a.EndedAt })
            .FirstOrDefaultAsync(ct);
        if (overlapping is not null)
            throw ValidationErrors.ForField("startedAt",
                $"Este período se sobrepõe a uma alocação já encerrada (até {FormatDateTime(overlapping.EndedAt!.Value)}). Informe um início posterior.");
    }

    private static string DescribeConflict(VehicleAssignment? ofVehicle, VehicleAssignment? ofDriver, Driver driver)
    {
        var parts = new List<string>();
        if (ofVehicle is not null) parts.Add($"o veículo está com {ofVehicle.Driver.FullName}");
        if (ofDriver is not null) parts.Add($"{driver.FullName} está com o veículo {Plate(ofDriver.Vehicle)}");
        return $"Não foi possível alocar: {string.Join(" e ", parts)}. Confirme a troca para encerrar a alocação atual.";
    }

    private async Task<AssignmentResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await ListAsync(db.VehicleAssignments.Where(a => a.Id == id), new ListRequest(), ct)).Items.Single();

    private async Task<PagedResult<AssignmentResponse>> ListAsync(IQueryable<VehicleAssignment> query, ListRequest request, CancellationToken ct)
    {
        var page = await query.OrderByDescending(a => a.StartedAt).ThenByDescending(a => a.Id)
            .ToPagedResultAsync(request, a => new
            {
                a.Id, a.VehicleId, a.Vehicle.LicensePlate, a.Vehicle.Manufacturer, a.Vehicle.Model, a.DriverId,
                a.Driver.FullName, a.StartedAt, a.EndedAt, a.Notes, a.EndReason, a.CreatedBy,
            }, ct);
        var names = await UserNames.LoadAsync(db, page.Items.Select(a => a.CreatedBy), ct);
        var items = page.Items.Select(a => new AssignmentResponse(
            a.Id, a.VehicleId, a.LicensePlate, $"{a.Manufacturer} {a.Model}", a.DriverId, a.FullName, a.StartedAt, a.EndedAt,
            a.EndedAt is null, a.Notes, a.EndReason, names.Get(a.CreatedBy))).ToList();
        return new PagedResult<AssignmentResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private async Task EnsureVehicleExistsAsync(Guid vehicleId, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
    }

    private static EventSubject Subject(VehicleAssignment a) => new(nameof(VehicleAssignment), a.Id, a.VehicleId, a.DriverId);

    private static string Plate(Vehicle vehicle) => LicensePlate.Format(vehicle.LicensePlate);

    private string FormatDateTime(DateTime utc) => clock.FormatDateTime(utc);

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
