using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Maintenance;

public sealed class WorkOrderListRequest : ListRequest
{
    public Guid? VehicleId { get; set; }
    public WorkOrderStatus? Status { get; set; }
    public MaintenanceType? Type { get; set; }
    public MaintenancePriority? Priority { get; set; }
    /// <summary>The "queue" (seção 30): not yet closed.</summary>
    public bool? ActiveOnly { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record WorkOrderItemRequest
{
    public Guid? Id { get; init; }
    public string? Description { get; init; }
    public Guid? MaintenancePlanItemId { get; init; }
    public bool IsRequired { get; init; } = true;
    public string? Notes { get; init; }
}

public sealed record WorkOrderRequest
{
    public Guid? VehicleId { get; init; }
    public Guid? ImplementId { get; init; }
    public Guid? WorkshopId { get; init; }
    public MaintenanceType Type { get; init; } = MaintenanceType.Corrective;
    public MaintenancePriority Priority { get; init; } = MaintenancePriority.Medium;
    public DateTime? ScheduledAt { get; init; }
    public string? Description { get; init; }
    public string? Diagnosis { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<WorkOrderItemRequest>? Items { get; init; }
}

public sealed record WorkOrderStatusRequest
{
    public WorkOrderStatus? Status { get; init; }
    /// <summary>Required to complete.</summary>
    public string? Resolution { get; init; }
    /// <summary>Required to cancel/reject.</summary>
    public string? Reason { get; init; }
    public int? OdometerKm { get; init; }
    public decimal? HourMeter { get; init; }
}

public sealed record WorkOrderItemCompleteRequest
{
    public bool Skip { get; init; }
    public string? Notes { get; init; }
}

public sealed record WorkOrderPartRequest
{
    public string? PartName { get; init; }
    public string? PartNumber { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? UnitCost { get; init; }
    public string? Supplier { get; init; }
    public string? Notes { get; init; }
}

public sealed record WorkOrderLaborRequest
{
    public string? TechnicianName { get; init; }
    public decimal? Hours { get; init; }
    public decimal? HourlyRate { get; init; }
    public string? Description { get; init; }
}

public sealed record WorkOrderItemResponse(
    Guid Id, string Description, Guid? MaintenancePlanItemId, bool IsRequired, WorkOrderItemStatus Status, string? Notes);

public sealed record WorkOrderPartResponse(
    Guid Id, string PartName, string? PartNumber, decimal Quantity, decimal UnitCost, decimal TotalCost, string? Supplier, string? Notes);

public sealed record WorkOrderLaborResponse(
    Guid Id, string TechnicianName, decimal Hours, decimal HourlyRate, decimal TotalCost, string? Description);

public sealed record RepeatedProblemResponse(string Description, int Occurrences);

public sealed record WorkOrderListItemResponse(
    Guid Id, string Number, Guid VehicleId, string LicensePlate, MaintenanceType Type, MaintenancePriority Priority,
    WorkOrderStatus Status, DateTime OpenedAt, DateTime? ScheduledAt, string Description, decimal? TotalCost);

public sealed record WorkOrderResponse(
    Guid Id, string Number, Guid VehicleId, string LicensePlate, Guid? ImplementId, string? ImplementPlate,
    Guid? MaintenanceRequestId, Guid? WorkshopId, string? WorkshopName,
    MaintenanceType Type, MaintenancePriority Priority, WorkOrderStatus Status, IReadOnlyList<WorkOrderStatus> NextStatuses,
    DateTime OpenedAt, DateTime? ScheduledAt, DateTime? StartedAt, DateTime? CompletedAt,
    int? OdometerKm, decimal? HourMeter, string Description, string? Diagnosis, string? Resolution, string? Notes,
    string? CompletedByName, decimal? PartsCost, decimal? LaborCost, decimal? OtherCost, decimal? TotalCost, int? DowntimeMinutes,
    IReadOnlyList<WorkOrderItemResponse> Items, IReadOnlyList<WorkOrderPartResponse> Parts, IReadOnlyList<WorkOrderLaborResponse> Labor,
    string? CreatedByName, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed class WorkOrderRequestValidator : AbstractValidator<WorkOrderRequest>
{
    public WorkOrderRequestValidator()
    {
        RuleFor(x => x.VehicleId).NotNull().WithMessage("Veículo: campo obrigatório.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Tipo inválido.");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Prioridade inválida.");
        RuleFor(x => x.Description).Required("Descrição").MaxLen(WorkOrder.DescriptionMaxLength);
        RuleFor(x => x.Diagnosis).MaxLen(WorkOrder.DiagnosisMaxLength);
        RuleFor(x => x.Notes).MaxLen(WorkOrder.NotesMaxLength);
        RuleForEach(x => x.Items).ChildRules(item =>
            item.RuleFor(i => i.Description).Required("Descrição do item").MaxLen(WorkOrderItem.DescriptionMaxLength));
    }
}

public sealed class WorkOrderStatusRequestValidator : AbstractValidator<WorkOrderStatusRequest>
{
    public WorkOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status).NotNull().WithMessage("Informe a nova situação.").IsInEnum().WithMessage("Situação inválida.");
        RuleFor(x => x.Resolution).MaxLen(WorkOrder.ResolutionMaxLength);
        RuleFor(x => x.Resolution)
            .Must(r => !string.IsNullOrWhiteSpace(r)).When(x => x.Status is { } s && WorkOrderWorkflow.RequiresResolution(s))
            .WithMessage("Descreva a resolução para concluir a ordem de serviço.");
        RuleFor(x => x.Reason).MaxLen(WorkOrder.NotesMaxLength);
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).When(x => x.Status is { } s && WorkOrderWorkflow.RequiresReason(s))
            .WithMessage(x => x.Status == WorkOrderStatus.Rejected ? "Informe o motivo da rejeição." : "Informe o motivo do cancelamento.");
    }
}

public sealed class WorkOrderPartRequestValidator : AbstractValidator<WorkOrderPartRequest>
{
    public WorkOrderPartRequestValidator()
    {
        RuleFor(x => x.PartName).Required("Peça").MaxLen(WorkOrderPart.PartNameMaxLength);
        RuleFor(x => x.PartNumber).MaxLen(WorkOrderPart.PartNumberMaxLength);
        RuleFor(x => x.Supplier).MaxLen(WorkOrderPart.SupplierMaxLength);
        RuleFor(x => x.Notes).MaxLen(WorkOrderPart.NotesMaxLength);
        RuleFor(x => x.Quantity).NotNull().WithMessage("Quantidade: campo obrigatório.").GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");
        RuleFor(x => x.UnitCost).NotNull().WithMessage("Custo unitário: campo obrigatório.").NonNegative("Custo unitário");
    }
}

public sealed class WorkOrderLaborRequestValidator : AbstractValidator<WorkOrderLaborRequest>
{
    public WorkOrderLaborRequestValidator()
    {
        RuleFor(x => x.TechnicianName).Required("Técnico").MaxLen(WorkOrderLabor.TechnicianNameMaxLength);
        RuleFor(x => x.Description).MaxLen(WorkOrderLabor.DescriptionMaxLength);
        RuleFor(x => x.Hours).NotNull().WithMessage("Horas: campo obrigatório.").GreaterThan(0).WithMessage("Horas deve ser maior que zero.");
        RuleFor(x => x.HourlyRate).NotNull().WithMessage("Valor da hora: campo obrigatório.").NonNegative("Valor da hora");
    }
}

/// <summary>
/// The maintenance job itself (seção 16/17). Also owns the ADR-028 rule: Vehicle.Status flips to UnderMaintenance
/// while a work order occupies the vehicle, and back to Available only when none do and nothing else changed it.
/// </summary>
public sealed class WorkOrderService(
    IFleetDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    OperationalEventLog events,
    MaintenanceScheduleService schedules,
    IValidator<WorkOrderRequest> validator,
    IValidator<WorkOrderStatusRequest> statusValidator,
    IValidator<WorkOrderPartRequest> partValidator,
    IValidator<WorkOrderLaborRequest> laborValidator)
{
    private static readonly SortMap<WorkOrder> Sorts = new SortMap<WorkOrder>("openedAt")
        .Add("openedAt", w => w.OpenedAt)
        .Add("status", w => w.Status)
        .Add("priority", w => w.Priority);

    public async Task<PagedResult<WorkOrderListItemResponse>> ListAsync(WorkOrderListRequest request, CancellationToken ct)
    {
        var query = db.WorkOrders.Include(w => w.Vehicle).AsQueryable();
        if (request.VehicleId is { } vehicleId) query = query.Where(w => w.VehicleId == vehicleId);
        if (request.Status is { } status) query = query.Where(w => w.Status == status);
        if (request.Type is { } type) query = query.Where(w => w.Type == type);
        if (request.Priority is { } priority) query = query.Where(w => w.Priority == priority);
        if (request.ActiveOnly == true)
            query = query.Where(w => w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled && w.Status != WorkOrderStatus.Rejected);
        if (request.From is { } from)
        {
            var start = clock.StartOfBusinessDayUtc(from);
            query = query.Where(w => w.OpenedAt >= start);
        }
        if (request.To is { } to)
        {
            var end = clock.StartOfBusinessDayUtc(to.AddDays(1));
            query = query.Where(w => w.OpenedAt < end);
        }
        if (request.SearchTerm is { } term)
        {
            var plate = LicensePlate.Normalize(term);
            query = query.Where(w => w.Description.Contains(term) || w.Vehicle.LicensePlate.Contains(plate));
        }

        var canViewCosts = currentUser.HasPermission(Permissions.Maintenance.ViewCosts);
        var direction = request.SortBy is null ? SortDirection.Desc : request.SortDirection;
        return await Sorts.Apply(query, request.SortBy, direction).ToPagedResultAsync(request, w => new WorkOrderListItemResponse(
            w.Id, "OS-" + w.Sequence.ToString("D6"), w.VehicleId, w.Vehicle.LicensePlate, w.Type, w.Priority,
            w.Status, w.OpenedAt, w.ScheduledAt, w.Description, canViewCosts ? w.TotalCost : null), ct);
    }

    public async Task<WorkOrderResponse> GetAsync(Guid id, CancellationToken ct) => await ToResponseAsync(await LoadAsync(id, ct), ct);

    public async Task<WorkOrderResponse> CreateAsync(WorkOrderRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == request.VehicleId, ct)
            ?? throw ValidationErrors.ForField("vehicleId", "Veículo não encontrado. Selecione um veículo da lista.");

        var workOrder = new WorkOrder { VehicleId = vehicle.Id, OpenedAt = clock.UtcNow, Sequence = await NextSequenceAsync(ct) };
        await ApplyAsync(request, workOrder, ct);
        ReplaceItems(workOrder, request.Items ?? []);
        RecalculateCosts(workOrder);
        db.WorkOrders.Add(workOrder);
        events.Record(OperationalEventType.WorkOrderOpened, Subject(workOrder),
            $"Ordem de serviço {workOrder.Number} aberta para {LicensePlate.Format(vehicle.LicensePlate)}: {Short(workOrder.Description)}",
            new { workOrder.Type, workOrder.Priority });
        await db.SaveChangesAsync(ct);
        return await GetAsync(workOrder.Id, ct);
    }

    /// <summary>Used by MaintenanceRequestService to open a WorkOrder already Approved from an approved request — same unit of work.</summary>
    internal async Task<WorkOrder> CreateFromRequestAsync(MaintenanceRequest request, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == request.VehicleId, ct)
            ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
        var workOrder = new WorkOrder
        {
            VehicleId = vehicle.Id,
            MaintenanceRequestId = request.Id,
            Type = request.MaintenanceType,
            Priority = request.Priority,
            Status = WorkOrderStatus.Approved,
            OpenedAt = clock.UtcNow,
            Description = request.Description,
            Sequence = await NextSequenceAsync(ct),
        };
        RecalculateCosts(workOrder);
        db.WorkOrders.Add(workOrder);
        events.Record(OperationalEventType.WorkOrderOpened, Subject(workOrder),
            $"Ordem de serviço {workOrder.Number} aberta a partir da solicitação de manutenção de {LicensePlate.Format(vehicle.LicensePlate)}.",
            new { workOrder.Type, workOrder.Priority, requestId = request.Id });
        return workOrder;
    }

    public async Task<WorkOrderResponse> UpdateAsync(Guid id, WorkOrderRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var workOrder = await LoadAsync(id, ct);
        if (workOrder.IsClosed)
            throw new BusinessRuleException("Ordens de serviço encerradas não podem ser alteradas.");
        if (workOrder.Status is not (WorkOrderStatus.Draft or WorkOrderStatus.Approved or WorkOrderStatus.Scheduled))
            throw new BusinessRuleException("A ordem de serviço já está em execução. Use as ações de itens, peças e mão de obra.");

        await ApplyAsync(request, workOrder, ct);
        ReplaceItems(workOrder, request.Items ?? []);
        RecalculateCosts(workOrder);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<WorkOrderResponse> ChangeStatusAsync(Guid id, WorkOrderStatusRequest request, CancellationToken ct)
    {
        await statusValidator.ValidateAndThrowAsync(request, ct);
        var workOrder = await LoadAsync(id, ct);
        var to = request.Status!.Value;
        if (!WorkOrderWorkflow.CanTransition(workOrder.Status, to))
            throw new BusinessRuleException(workOrder.IsClosed
                ? "Esta ordem de serviço já foi encerrada e não pode mudar de situação."
                : "Esta mudança de situação não é permitida.");
        if (to == WorkOrderStatus.Completed && workOrder.Items.Any(i => i.IsRequired && i.Status == WorkOrderItemStatus.Pending))
            throw new BusinessRuleException("Há itens obrigatórios pendentes. Marque-os como concluídos ou dispensados antes de fechar a ordem.");

        var from = workOrder.Status;
        var wasOccupying = WorkOrderWorkflow.OccupiesVehicle(from);
        var willOccupy = WorkOrderWorkflow.OccupiesVehicle(to);
        if (!wasOccupying && willOccupy) await OccupyVehicleAsync(workOrder, ct);

        workOrder.Status = to;
        if (to == WorkOrderStatus.InProgress) workOrder.StartedAt ??= clock.UtcNow;
        if (WorkOrderWorkflow.IsClosed(to))
        {
            workOrder.CompletedAt = clock.UtcNow;
            workOrder.CompletedBy = currentUser.UserId;
            workOrder.DowntimeMinutes = workOrder.StartedAt is { } started ? (int)(workOrder.CompletedAt.Value - started).TotalMinutes : null;
            if (to == WorkOrderStatus.Completed)
            {
                workOrder.Resolution = request.Resolution!.Trim();
                workOrder.OdometerKm = await ResolveOdometerAsync(workOrder, request.OdometerKm, ct);
                workOrder.HourMeter = request.HourMeter ?? workOrder.Vehicle.HourMeter;
            }
            else
            {
                workOrder.Notes = AppendReason(workOrder.Notes, request.Reason!.Trim());
            }
        }

        if (wasOccupying && !willOccupy) await ReleaseVehicleIfLastAsync(workOrder, ct);

        events.Record(OperationalEventType.WorkOrderStatusChanged, Subject(workOrder),
            $"Ordem de serviço {workOrder.Number}: {StatusText(from)} → {StatusText(to)}.", new { from, to });
        if (to == WorkOrderStatus.Completed)
        {
            events.Record(OperationalEventType.WorkOrderCompleted, Subject(workOrder),
                $"Ordem de serviço {workOrder.Number} concluída.", new { workOrder.TotalCost });
            foreach (var item in workOrder.Items.Where(i => i.MaintenancePlanItemId is not null))
                await schedules.RecalculateAsync(workOrder.VehicleId, item.MaintenancePlanItemId!.Value, workOrder, ct);
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<WorkOrderResponse> CompleteItemAsync(Guid id, Guid itemId, WorkOrderItemCompleteRequest request, CancellationToken ct)
    {
        var workOrder = await LoadAsync(id, ct);
        EnsureEditable(workOrder);
        var item = workOrder.Items.SingleOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("Item não encontrado nesta ordem de serviço.");
        item.Status = request.Skip ? WorkOrderItemStatus.Skipped : WorkOrderItemStatus.Done;
        if (request.Notes.TrimToNull() is { } notes) item.Notes = notes;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<WorkOrderResponse> AddPartAsync(Guid id, WorkOrderPartRequest request, CancellationToken ct)
    {
        await partValidator.ValidateAndThrowAsync(request, ct);
        var workOrder = await LoadAsync(id, ct);
        EnsureEditable(workOrder);
        workOrder.Parts.Add(new WorkOrderPart
        {
            PartName = request.PartName!.Trim(),
            PartNumber = request.PartNumber.TrimToNull(),
            Quantity = request.Quantity!.Value,
            UnitCost = request.UnitCost!.Value,
            Supplier = request.Supplier.TrimToNull(),
            Notes = request.Notes.TrimToNull(),
        });
        RecalculateCosts(workOrder);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<WorkOrderResponse> RemovePartAsync(Guid id, Guid partId, CancellationToken ct)
    {
        var workOrder = await LoadAsync(id, ct);
        EnsureEditable(workOrder);
        var part = workOrder.Parts.SingleOrDefault(p => p.Id == partId) ?? throw new NotFoundException("Peça não encontrada nesta ordem de serviço.");
        workOrder.Parts.Remove(part);
        RecalculateCosts(workOrder);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<WorkOrderResponse> AddLaborAsync(Guid id, WorkOrderLaborRequest request, CancellationToken ct)
    {
        await laborValidator.ValidateAndThrowAsync(request, ct);
        var workOrder = await LoadAsync(id, ct);
        EnsureEditable(workOrder);
        workOrder.Labor.Add(new WorkOrderLabor
        {
            TechnicianName = request.TechnicianName!.Trim(),
            Hours = request.Hours!.Value,
            HourlyRate = request.HourlyRate!.Value,
            Description = request.Description.TrimToNull(),
        });
        RecalculateCosts(workOrder);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<WorkOrderResponse> RemoveLaborAsync(Guid id, Guid laborId, CancellationToken ct)
    {
        var workOrder = await LoadAsync(id, ct);
        EnsureEditable(workOrder);
        var labor = workOrder.Labor.SingleOrDefault(l => l.Id == laborId) ?? throw new NotFoundException("Mão de obra não encontrada nesta ordem de serviço.");
        workOrder.Labor.Remove(labor);
        RecalculateCosts(workOrder);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Repeated problems (seção 31): same vehicle + description prefix, counted over the last 90 days.</summary>
    public async Task<IReadOnlyList<RepeatedProblemResponse>> RepeatedProblemsAsync(Guid vehicleId, CancellationToken ct)
    {
        var since = clock.UtcNow.AddDays(-90);
        var orders = await db.WorkOrders
            .Where(w => w.VehicleId == vehicleId && w.Type == MaintenanceType.Corrective && w.OpenedAt >= since)
            .Select(w => w.Description).ToListAsync(ct);
        return orders.GroupBy(d => d.Length <= 60 ? d : d[..60])
            .Where(g => g.Count() > 1)
            .Select(g => new RepeatedProblemResponse(g.Key, g.Count()))
            .OrderByDescending(g => g.Occurrences).ToList();
    }

    private async Task<int> ResolveOdometerAsync(WorkOrder workOrder, int? requested, CancellationToken ct)
    {
        if (requested is not { } km) return workOrder.Vehicle.CurrentOdometerKm;
        if (km < workOrder.Vehicle.CurrentOdometerKm)
            throw ValidationErrors.ForField("odometerKm",
                $"O hodômetro informado ({BrazilianFormat.Number(km)} km) é menor que o atual do veículo " +
                $"({BrazilianFormat.Number(workOrder.Vehicle.CurrentOdometerKm)} km). Registre a leitura real pelo módulo de hodômetro.");
        await Task.CompletedTask;
        return km;
    }

    private async Task OccupyVehicleAsync(WorkOrder workOrder, CancellationToken ct)
    {
        var vehicle = workOrder.Vehicle;
        if (vehicle.Status is VehicleStatus.OnTrip or VehicleStatus.Inactive)
            throw new BusinessRuleException(
                $"O veículo {LicensePlate.Format(vehicle.LicensePlate)} está {VehicleStatusText(vehicle.Status).ToLowerInvariant()} e não pode entrar em manutenção agora.");
        if (vehicle.Status != VehicleStatus.Available) { await Task.CompletedTask; return; }

        var previous = vehicle.Status;
        vehicle.Status = VehicleStatus.UnderMaintenance;
        events.Record(OperationalEventType.VehicleStatusChanged, new EventSubject(nameof(Vehicle), vehicle.Id, vehicle.Id),
            $"Situação do veículo {LicensePlate.Format(vehicle.LicensePlate)} alterada de {VehicleStatusText(previous)} para " +
            $"{VehicleStatusText(vehicle.Status)} (ordem de serviço {workOrder.Number}).",
            new { from = previous, to = vehicle.Status });
    }

    private async Task ReleaseVehicleIfLastAsync(WorkOrder workOrder, CancellationToken ct)
    {
        var stillOccupied = await db.WorkOrders.AnyAsync(w => w.VehicleId == workOrder.VehicleId && w.Id != workOrder.Id &&
            (w.Status == WorkOrderStatus.InProgress || w.Status == WorkOrderStatus.WaitingParts), ct);
        if (stillOccupied || workOrder.Vehicle.Status != VehicleStatus.UnderMaintenance) return;

        var previous = workOrder.Vehicle.Status;
        workOrder.Vehicle.Status = VehicleStatus.Available;
        events.Record(OperationalEventType.VehicleStatusChanged, new EventSubject(nameof(Vehicle), workOrder.Vehicle.Id, workOrder.Vehicle.Id),
            $"Situação do veículo {LicensePlate.Format(workOrder.Vehicle.LicensePlate)} alterada de {VehicleStatusText(previous)} para " +
            $"{VehicleStatusText(workOrder.Vehicle.Status)} (ordem de serviço {workOrder.Number} encerrada).",
            new { from = previous, to = workOrder.Vehicle.Status });
    }

    private void EnsureEditable(WorkOrder workOrder)
    {
        if (workOrder.IsClosed) throw new BusinessRuleException("Ordens de serviço encerradas não podem ser alteradas.");
    }

    private async Task<int> NextSequenceAsync(CancellationToken ct)
    {
        var max = await db.WorkOrders.Select(w => (int?)w.Sequence).MaxAsync(ct);
        return (max ?? 0) + 1;
    }

    private async Task ApplyAsync(WorkOrderRequest request, WorkOrder workOrder, CancellationToken ct)
    {
        if (request.ImplementId is { } implementId && !await db.Implements.AnyAsync(i => i.Id == implementId, ct))
            throw ValidationErrors.ForField("implementId", "Implemento não encontrado. Selecione um implemento da lista.");
        if (request.WorkshopId is { } workshopId && !await db.Workshops.AnyAsync(w => w.Id == workshopId, ct))
            throw ValidationErrors.ForField("workshopId", "Oficina não encontrada. Selecione uma oficina da lista.");

        workOrder.ImplementId = request.ImplementId;
        workOrder.WorkshopId = request.WorkshopId;
        workOrder.Type = request.Type;
        workOrder.Priority = request.Priority;
        workOrder.ScheduledAt = request.ScheduledAt;
        workOrder.Description = request.Description!.Trim();
        workOrder.Diagnosis = request.Diagnosis.TrimToNull();
        workOrder.Notes = request.Notes.TrimToNull();
    }

    private static void ReplaceItems(WorkOrder workOrder, IReadOnlyList<WorkOrderItemRequest> items)
    {
        var existing = workOrder.Items.ToDictionary(i => i.Id);
        var requestedIds = items.Where(i => i.Id is not null).Select(i => i.Id!.Value).ToHashSet();
        foreach (var removedId in existing.Keys.Where(itemId => !requestedIds.Contains(itemId)).ToList())
            workOrder.Items.Remove(existing[removedId]);
        foreach (var request in items)
        {
            if (request.Id is { } itemId && existing.TryGetValue(itemId, out var item))
                Fill(item, request);
            else
                workOrder.Items.Add(NewItem(request));
        }
    }

    private static WorkOrderItem NewItem(WorkOrderItemRequest request)
    {
        var item = new WorkOrderItem();
        Fill(item, request);
        return item;
    }

    private static void Fill(WorkOrderItem item, WorkOrderItemRequest request)
    {
        item.Description = request.Description!.Trim();
        item.MaintenancePlanItemId = request.MaintenancePlanItemId;
        item.IsRequired = request.IsRequired;
        item.Notes = request.Notes.TrimToNull();
    }

    private static void RecalculateCosts(WorkOrder workOrder)
    {
        workOrder.PartsCost = workOrder.Parts.Sum(p => p.TotalCost);
        workOrder.LaborCost = workOrder.Labor.Sum(l => l.TotalCost);
        workOrder.TotalCost = workOrder.PartsCost + workOrder.LaborCost + workOrder.OtherCost;
    }

    private static string? AppendReason(string? notes, string reason) =>
        string.IsNullOrWhiteSpace(notes) ? reason : $"{notes}\n{reason}";

    private async Task<WorkOrder> LoadAsync(Guid id, CancellationToken ct) =>
        await db.WorkOrders.Include(w => w.Vehicle).Include(w => w.Implement).Include(w => w.Workshop)
            .Include(w => w.Items).Include(w => w.Parts).Include(w => w.Labor)
            .SingleOrDefaultAsync(w => w.Id == id, ct)
        ?? throw new NotFoundException("Ordem de serviço não encontrada.");

    private async Task<WorkOrderResponse> ToResponseAsync(WorkOrder w, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, [w.CreatedBy, w.CompletedBy], ct);
        var canViewCosts = currentUser.HasPermission(Permissions.Maintenance.ViewCosts);

        return new WorkOrderResponse(
            w.Id, w.Number, w.VehicleId, w.Vehicle.LicensePlate, w.ImplementId, w.Implement?.LicensePlate,
            w.MaintenanceRequestId, w.WorkshopId, w.Workshop?.Name,
            w.Type, w.Priority, w.Status, WorkOrderWorkflow.NextStatuses(w.Status),
            w.OpenedAt, w.ScheduledAt, w.StartedAt, w.CompletedAt, w.OdometerKm, w.HourMeter,
            w.Description, w.Diagnosis, w.Resolution, w.Notes, names.Get(w.CompletedBy),
            canViewCosts ? w.PartsCost : null, canViewCosts ? w.LaborCost : null,
            canViewCosts ? w.OtherCost : null, canViewCosts ? w.TotalCost : null, w.DowntimeMinutes,
            w.Items.Select(i => new WorkOrderItemResponse(i.Id, i.Description, i.MaintenancePlanItemId, i.IsRequired, i.Status, i.Notes)).ToList(),
            w.Parts.Select(p => new WorkOrderPartResponse(p.Id, p.PartName, p.PartNumber,
                p.Quantity, canViewCosts ? p.UnitCost : 0, canViewCosts ? p.TotalCost : 0, p.Supplier, p.Notes)).ToList(),
            w.Labor.Select(l => new WorkOrderLaborResponse(l.Id, l.TechnicianName, l.Hours,
                canViewCosts ? l.HourlyRate : 0, canViewCosts ? l.TotalCost : 0, l.Description)).ToList(),
            names.Get(w.CreatedBy), w.CreatedAt, w.UpdatedAt);
    }

    private static EventSubject Subject(WorkOrder w) => new(nameof(WorkOrder), w.Id, w.VehicleId);

    private static string Short(string text) => text.Length <= 80 ? text : text[..79] + "…";

    private static string StatusText(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Draft => "rascunho",
        WorkOrderStatus.Approved => "aprovada",
        WorkOrderStatus.Scheduled => "agendada",
        WorkOrderStatus.InProgress => "em andamento",
        WorkOrderStatus.WaitingParts => "aguardando peças",
        WorkOrderStatus.Completed => "concluída",
        WorkOrderStatus.Cancelled => "cancelada",
        _ => "rejeitada",
    };

    private static string VehicleStatusText(VehicleStatus status) => status switch
    {
        VehicleStatus.Available => "Disponível",
        VehicleStatus.OnTrip => "Em viagem",
        VehicleStatus.UnderMaintenance => "Em manutenção",
        VehicleStatus.Unavailable => "Indisponível",
        _ => "Inativo",
    };
}
