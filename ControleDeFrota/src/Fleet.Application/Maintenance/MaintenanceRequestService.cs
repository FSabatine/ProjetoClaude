using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Maintenance;

public sealed class MaintenanceRequestListRequest : ListRequest
{
    public Guid? VehicleId { get; set; }
    public MaintenanceRequestStatus? Status { get; set; }
    public bool? OpenOnly { get; set; }
}

public sealed record MaintenanceRequestRequest
{
    public Guid? VehicleId { get; init; }
    public Guid? DriverId { get; init; }
    public MaintenanceRequestSource Source { get; init; } = MaintenanceRequestSource.FleetManager;
    public MaintenanceType MaintenanceType { get; init; } = MaintenanceType.Corrective;
    public MaintenancePriority Priority { get; init; } = MaintenancePriority.Medium;
    public string? Description { get; init; }
    public DateTime? ReportedAt { get; init; }
    public int? OdometerKm { get; init; }
    public decimal? HourMeter { get; init; }
    /// <summary>When set, Source is forced to Occurrence — this request traces back to it (botão "Abrir solicitação").</summary>
    public Guid? OccurrenceId { get; init; }
}

public sealed record MaintenanceRequestRejectRequest
{
    public string? Reason { get; init; }
}

public sealed record MaintenanceRequestListItemResponse(
    Guid Id, Guid VehicleId, string LicensePlate, MaintenanceRequestSource Source, MaintenanceType MaintenanceType,
    MaintenancePriority Priority, MaintenanceRequestStatus Status, DateTime ReportedAt, string Description);

public sealed record MaintenanceRequestResponse(
    Guid Id, Guid VehicleId, string LicensePlate, Guid? DriverId, string? DriverName,
    MaintenanceRequestSource Source, MaintenanceType MaintenanceType, MaintenancePriority Priority,
    string Description, DateTime ReportedAt, int? OdometerKm, decimal? HourMeter, Guid? OccurrenceId,
    MaintenanceRequestStatus Status, DateTime? ReviewedAt, string? ReviewedByName, string? RejectionReason,
    Guid? WorkOrderId, string? WorkOrderNumber, string? CreatedByName, DateTime CreatedAt);

public sealed class MaintenanceRequestRequestValidator : AbstractValidator<MaintenanceRequestRequest>
{
    public MaintenanceRequestRequestValidator(IClock clock)
    {
        RuleFor(x => x.VehicleId).NotNull().WithMessage("Veículo: campo obrigatório.");
        RuleFor(x => x.Source).IsInEnum().WithMessage("Origem inválida.");
        RuleFor(x => x.MaintenanceType).IsInEnum().WithMessage("Tipo inválido.");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Prioridade inválida.");
        RuleFor(x => x.Description).Required("Descrição").MaxLen(MaintenanceRequest.DescriptionMaxLength);
        RuleFor(x => x.ReportedAt)
            .Must(d => d is null || d.Value <= clock.UtcNow.AddMinutes(5)).WithMessage("A data não pode ser futura.");
    }
}

public sealed class MaintenanceRequestRejectRequestValidator : AbstractValidator<MaintenanceRequestRejectRequest>
{
    public MaintenanceRequestRejectRequestValidator() =>
        RuleFor(x => x.Reason).Required("Motivo").MaxLen(MaintenanceRequest.RejectionReasonMaxLength);
}

/// <summary>
/// "Someone needs maintenance done" (seção 12/13). Approving creates the WorkOrder in the same transaction
/// (WorkOrderService.CreateFromRequestAsync) — there is no separate "approved, no work order yet" state.
/// </summary>
public sealed class MaintenanceRequestService(
    IFleetDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    OperationalEventLog events,
    WorkOrderService workOrders,
    IValidator<MaintenanceRequestRequest> validator,
    IValidator<MaintenanceRequestRejectRequest> rejectValidator)
{
    private static readonly SortMap<MaintenanceRequest> Sorts = new SortMap<MaintenanceRequest>("reportedAt")
        .Add("reportedAt", r => r.ReportedAt)
        .Add("priority", r => r.Priority)
        .Add("status", r => r.Status);

    public async Task<PagedResult<MaintenanceRequestListItemResponse>> ListAsync(MaintenanceRequestListRequest request, CancellationToken ct)
    {
        var query = db.MaintenanceRequests.Include(r => r.Vehicle).AsQueryable();
        if (request.VehicleId is { } vehicleId) query = query.Where(r => r.VehicleId == vehicleId);
        if (request.Status is { } status) query = query.Where(r => r.Status == status);
        if (request.OpenOnly == true) query = query.Where(r => r.Status == MaintenanceRequestStatus.Open);

        var direction = request.SortBy is null ? SortDirection.Desc : request.SortDirection;
        return await Sorts.Apply(query, request.SortBy, direction).ToPagedResultAsync(request, r => new MaintenanceRequestListItemResponse(
            r.Id, r.VehicleId, r.Vehicle.LicensePlate, r.Source, r.MaintenanceType, r.Priority, r.Status, r.ReportedAt, r.Description), ct);
    }

    public async Task<MaintenanceRequestResponse> GetAsync(Guid id, CancellationToken ct) => await ToResponseAsync(await LoadAsync(id, ct), ct);

    public async Task<MaintenanceRequestResponse> CreateAsync(MaintenanceRequestRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == request.VehicleId, ct)
            ?? throw ValidationErrors.ForField("vehicleId", "Veículo não encontrado. Selecione um veículo da lista.");
        if (request.DriverId is { } driverId && !await db.Drivers.AnyAsync(d => d.Id == driverId, ct))
            throw ValidationErrors.ForField("driverId", "Motorista não encontrado. Selecione um motorista da lista.");
        if (request.OccurrenceId is { } occurrenceId && !await db.Occurrences.AnyAsync(o => o.Id == occurrenceId, ct))
            throw ValidationErrors.ForField("occurrenceId", "Ocorrência não encontrada.");

        var maintenanceRequest = new MaintenanceRequest
        {
            VehicleId = vehicle.Id,
            DriverId = request.DriverId,
            Source = request.OccurrenceId is not null ? MaintenanceRequestSource.Occurrence : request.Source,
            MaintenanceType = request.MaintenanceType,
            Priority = request.Priority,
            Description = request.Description!.Trim(),
            ReportedAt = Min(request.ReportedAt ?? clock.UtcNow, clock.UtcNow),
            OdometerKm = request.OdometerKm,
            HourMeter = request.HourMeter,
            OccurrenceId = request.OccurrenceId,
        };
        db.MaintenanceRequests.Add(maintenanceRequest);
        events.Record(OperationalEventType.MaintenanceRequestCreated, Subject(maintenanceRequest),
            $"Solicitação de manutenção registrada para {LicensePlate.Format(vehicle.LicensePlate)}: {Short(maintenanceRequest.Description)}",
            new { maintenanceRequest.MaintenanceType, maintenanceRequest.Priority, maintenanceRequest.Source });
        await db.SaveChangesAsync(ct);
        return await GetAsync(maintenanceRequest.Id, ct);
    }

    /// <summary>Approves and immediately opens the work order (Status = Approved) — one decision, one action.</summary>
    public async Task<MaintenanceRequestResponse> ApproveAsync(Guid id, CancellationToken ct)
    {
        var request = await LoadAsync(id, ct);
        if (request.IsClosed)
            throw new BusinessRuleException("Esta solicitação já foi avaliada e não pode mudar de situação.");

        var workOrder = await workOrders.CreateFromRequestAsync(request, ct);
        request.Status = MaintenanceRequestStatus.Converted;
        request.ReviewedAt = clock.UtcNow;
        request.ReviewedBy = currentUser.UserId;
        request.WorkOrderId = workOrder.Id;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<MaintenanceRequestResponse> RejectAsync(Guid id, MaintenanceRequestRejectRequest requestBody, CancellationToken ct)
    {
        await rejectValidator.ValidateAndThrowAsync(requestBody, ct);
        var request = await LoadAsync(id, ct);
        if (request.IsClosed)
            throw new BusinessRuleException("Esta solicitação já foi avaliada e não pode mudar de situação.");

        request.Status = MaintenanceRequestStatus.Rejected;
        request.RejectionReason = requestBody.Reason!.Trim();
        request.ReviewedAt = clock.UtcNow;
        request.ReviewedBy = currentUser.UserId;
        events.Record(OperationalEventType.MaintenanceRequestRejected, Subject(request),
            $"Solicitação de manutenção de {LicensePlate.Format(request.Vehicle.LicensePlate)} rejeitada: {request.RejectionReason}",
            new { request.RejectionReason });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<MaintenanceRequest> LoadAsync(Guid id, CancellationToken ct) =>
        await db.MaintenanceRequests.Include(r => r.Vehicle).Include(r => r.Driver).SingleOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException("Solicitação de manutenção não encontrada.");

    private async Task<MaintenanceRequestResponse> ToResponseAsync(MaintenanceRequest r, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, [r.CreatedBy, r.ReviewedBy], ct);
        var workOrderSequence = r.WorkOrderId is { } workOrderId
            ? await db.WorkOrders.Where(w => w.Id == workOrderId).Select(w => (int?)w.Sequence).SingleOrDefaultAsync(ct)
            : null;

        return new MaintenanceRequestResponse(
            r.Id, r.VehicleId, r.Vehicle.LicensePlate, r.DriverId, r.Driver?.FullName,
            r.Source, r.MaintenanceType, r.Priority, r.Description, r.ReportedAt, r.OdometerKm, r.HourMeter, r.OccurrenceId,
            r.Status, r.ReviewedAt, names.Get(r.ReviewedBy), r.RejectionReason,
            r.WorkOrderId, workOrderSequence is { } seq ? "OS-" + seq.ToString("D6") : null,
            names.Get(r.CreatedBy), r.CreatedAt);
    }

    private static EventSubject Subject(MaintenanceRequest r) => new(nameof(MaintenanceRequest), r.Id, r.VehicleId, r.DriverId);

    private static string Short(string text) => text.Length <= 80 ? text : text[..79] + "…";

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
