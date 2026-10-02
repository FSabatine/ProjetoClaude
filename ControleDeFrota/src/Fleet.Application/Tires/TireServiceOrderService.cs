using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Domain.Authorization;
using Fleet.Domain.Files;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tires;

public sealed record TireServiceRequest
{
    public TireServiceKind? Kind { get; init; }
    public DateTime? SentAt { get; init; }
    public Guid? WorkshopId { get; init; }
    public string? ProviderName { get; init; }
    public TireRepairType? RepairType { get; init; }
    public string? Description { get; init; }
    // Installed tire (repair in place): recorded already completed.
    public decimal? Cost { get; init; }
    public DateOnly? WarrantyUntil { get; init; }
    public string? ResultNotes { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public sealed record TireServiceCompletionRequest
{
    public DateTime? CompletedAt { get; init; }
    public TireServiceResult? Result { get; init; }
    public Guid? WorkshopId { get; init; }
    public string? ProviderName { get; init; }
    public TireRepairType? RepairType { get; init; }
    public string? TreadPattern { get; init; }
    public decimal? NewTreadDepthMm { get; init; }
    public decimal? Cost { get; init; }
    public DateOnly? WarrantyUntil { get; init; }
    public string? ResultNotes { get; init; }
    public string? StorageLocation { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public sealed record TireServiceCancelRequest
{
    public string? Reason { get; init; }
}

public sealed record TireCostRequest
{
    public TireCostType? Type { get; init; }
    public DateOnly? IncurredOn { get; init; }
    public decimal? Amount { get; init; }
    public string? Description { get; init; }
}

public sealed record TireServiceOrderResponse(
    Guid Id, Guid TireId, string TireCode, TireServiceKind Kind, TireServiceStatus Status, TireServiceResult? Result, bool InPlace,
    Guid? WorkshopId, string? ProviderName, DateTime SentAt, DateTime? CompletedAt, TireRepairType? RepairType, int? RetreadNumber,
    string? TreadPattern, decimal? NewTreadDepthMm, decimal? Cost, DateOnly? WarrantyUntil, string? Description, string? ResultNotes,
    string? CancellationReason, string? CreatedByName, IReadOnlyList<FileResponse> Files, bool CanManage);

public sealed record TireCostResponse(
    Guid Id, TireCostType Type, DateOnly IncurredOn, decimal Amount, string? Description, Guid? ServiceOrderId, bool CanDelete, string? CreatedByName);

public sealed class TireServiceRequestValidator : AbstractValidator<TireServiceRequest>
{
    public TireServiceRequestValidator()
    {
        RuleFor(x => x.Kind).NotNull().WithMessage("Tipo de serviço: informe conserto ou recapagem.").IsInEnum().WithMessage("Tipo inválido.");
        RuleFor(x => x.RepairType).IsInEnum().WithMessage("Tipo de conserto inválido.");
        RuleFor(x => x.ProviderName).MaxLen(TireServiceOrder.ProviderMaxLength);
        RuleFor(x => x.Description).MaxLen(TireServiceOrder.TextMaxLength);
        RuleFor(x => x.ResultNotes).MaxLen(TireServiceOrder.TextMaxLength);
        RuleFor(x => x.Cost).InclusiveBetween(0m, TireCost.MaxAmount).WithMessage("Valor inválido.");
    }
}

public sealed class TireServiceCompletionRequestValidator : AbstractValidator<TireServiceCompletionRequest>
{
    public TireServiceCompletionRequestValidator()
    {
        RuleFor(x => x.Result).NotNull().WithMessage("Resultado: informe se o serviço foi aprovado ou reprovado.").IsInEnum().WithMessage("Resultado inválido.");
        RuleFor(x => x.RepairType).IsInEnum().WithMessage("Tipo de conserto inválido.");
        RuleFor(x => x.ProviderName).MaxLen(TireServiceOrder.ProviderMaxLength);
        RuleFor(x => x.TreadPattern).MaxLen(TireServiceOrder.TreadPatternMaxLength);
        RuleFor(x => x.NewTreadDepthMm).InclusiveBetween(1m, TireMeasurementRules.MaxTreadMm).WithMessage("Sulco novo deve estar entre 1 e 40 mm.");
        RuleFor(x => x.Cost).InclusiveBetween(0m, TireCost.MaxAmount).WithMessage("Valor inválido.");
        RuleFor(x => x.ResultNotes).MaxLen(TireServiceOrder.TextMaxLength);
        RuleFor(x => x.ResultNotes).Must(n => !string.IsNullOrWhiteSpace(n)).When(x => x.Result == TireServiceResult.Rejected)
            .WithMessage("Informe o motivo da reprovação (ex.: carcaça sem condição de recapagem).");
        RuleFor(x => x.StorageLocation).MaxLen(Tire.StorageMaxLength);
    }
}

public sealed class TireCostRequestValidator : AbstractValidator<TireCostRequest>
{
    public TireCostRequestValidator(IClock clock)
    {
        RuleFor(x => x.Type).NotNull().WithMessage("Tipo de custo: campo obrigatório.")
            .Must(t => t is null or TireCostType.Installation or TireCostType.Other)
            .WithMessage("Custos de conserto e recapagem são registrados ao concluir o serviço. Aqui: montagem ou outros.");
        RuleFor(x => x.IncurredOn).NotNull().WithMessage("Data: campo obrigatório.")
            .Must(d => d is null || d <= clock.Today).WithMessage("A data não pode ser futura.");
        RuleFor(x => x.Amount).NotNull().WithMessage("Valor: campo obrigatório.")
            .GreaterThan(0m).WithMessage("O valor deve ser maior que zero.")
            .LessThanOrEqualTo(TireCost.MaxAmount).WithMessage("Valor muito alto. Confira.");
        RuleFor(x => x.Description).MaxLen(TireCost.DescriptionMaxLength);
    }
}

/// <summary>
/// Repairs and retreads (seções 22, 23, 69) and the tire's operational costs (seções 24–26). Who decides whether a carcass
/// can be retreaded is the provider/manager — recorded as the result; the system never decides eligibility by itself.
/// </summary>
public sealed class TireServiceOrderService(
    IFleetDbContext db,
    TireLifecycle lifecycle,
    TireMonitoring monitoring,
    TireService tires,
    TireSettingsService settingsService,
    FileService files,
    IValidator<TireServiceRequest> validator,
    IValidator<TireServiceCompletionRequest> completionValidator,
    IValidator<TireCostRequest> costValidator)
{
    public async Task<TireServiceOrderResponse> SendAsync(Guid tireId, TireServiceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var kind = request.Kind!.Value;
        RequireKindPermission(kind);
        if (request.Cost is not null && !lifecycle.CanSeeCosts)
            throw new ForbiddenException("Informar o valor do serviço exige a permissão de visualizar custos de pneus.");
        var tire = await lifecycle.LoadTireAsync(tireId, ct);
        await EnsureWorkshopAsync(request.WorkshopId, ct);
        var at = lifecycle.ResolveTime(request.SentAt, tire, "sentAt");
        TireServiceOrder order = null!;

        if (tire.Status == TireStatus.Installed)
        {
            // A puncture fixed on the vehicle: no removal, recorded already done (seção 22).
            if (kind != TireServiceKind.Repair)
                throw new BusinessRuleException($"O pneu {tire.Code} está instalado. Para recapar, remova-o com o destino \"Recapagem\".");
            var stint = await lifecycle.OpenStintAsync(tireId, ct);
            var asset = await lifecycle.AssetOfAsync(stint!, ct);
            await lifecycle.RunAsync(async () =>
            {
                order = Open(db, tire, kind, at, request.WorkshopId, request.ProviderName.TrimToNull(), request.RepairType, request.Description.TrimToNull());
                order.InPlace = true;
                tire.Status = TireStatus.Installed; // Open() moved it to "under repair": a repair in place never leaves the vehicle
                await CompleteCoreAsync(tire, order, at, TireServiceResult.Approved, request.Cost, request.WarrantyUntil, request.ResultNotes, null, null, ct);
                await files.AttachAsync(request.FileIds, FileOwnerType.TireServiceOrder, order.Id, "fileIds", ct);
                lifecycle.Record(OperationalEventType.TireRepairCompleted, tire, asset, at,
                    $"Conserto do pneu {tire.Code} feito no veículo ({asset.Label}, {stint!.PositionLabel})" +
                    (order.RepairType is { } r ? $": {RepairText(r)}." : "."),
                    new { tireId = tire.Id, serviceOrderId = order.Id, inPlace = true });
                await db.SaveChangesAsync(ct);
            }, ct);
            return await GetAsync(order.Id, ct);
        }

        if (!TireWorkflow.CanSendToService(tire.Status))
            throw new BusinessRuleException($"O pneu {tire.Code} está {TireOperationsService.StatusText(tire.Status)} e não pode ser enviado agora.");
        await lifecycle.RunAsync(async () =>
        {
            order = Open(db, tire, kind, at, request.WorkshopId, request.ProviderName.TrimToNull(), request.RepairType, request.Description.TrimToNull());
            await files.AttachAsync(request.FileIds, FileOwnerType.TireServiceOrder, order.Id, "fileIds", ct);
            var provider = await ProviderAsync(order, ct);
            lifecycle.Record(kind == TireServiceKind.Retread ? OperationalEventType.TireRetreadStarted : OperationalEventType.TireRepairStarted,
                tire, null, at, StartedSummary(tire, order, provider), new { tireId = tire.Id, serviceOrderId = order.Id }, tireOnly: true);
            await db.SaveChangesAsync(ct);
        }, ct);
        return await GetAsync(order.Id, ct);
    }

    public async Task<TireServiceOrderResponse> CompleteAsync(Guid orderId, TireServiceCompletionRequest request, CancellationToken ct)
    {
        await completionValidator.ValidateAndThrowAsync(request, ct);
        var order = await LoadOpenAsync(orderId, ct);
        if (request.Cost is not null && !lifecycle.CanSeeCosts)
            throw new ForbiddenException("Informar o valor do serviço exige a permissão de visualizar custos de pneus.");
        if (order.Kind == TireServiceKind.Retread && request.Result == TireServiceResult.Approved && request.NewTreadDepthMm is null)
            throw ValidationErrors.ForField("newTreadDepthMm", "Informe o sulco da banda nova: ele passa a ser o sulco atual do pneu.");
        await EnsureWorkshopAsync(request.WorkshopId, ct);
        var tire = order.Tire;
        var at = lifecycle.ResolveTime(request.CompletedAt, tire, "completedAt");

        await lifecycle.RunAsync(async () =>
        {
            if (request.WorkshopId is not null) order.WorkshopId = request.WorkshopId;
            if (request.ProviderName.TrimToNull() is { } name) order.ProviderName = name;
            if (request.RepairType is not null) order.RepairType = request.RepairType;
            await CompleteCoreAsync(tire, order, at, request.Result!.Value, request.Cost, request.WarrantyUntil, request.ResultNotes,
                request.TreadPattern, request.NewTreadDepthMm, ct);
            tire.Status = TireWorkflow.AfterService(order.Status, order.Result);
            if (request.StorageLocation.TrimToNull() is { } location) tire.StorageLocation = location;
            await files.AttachAsync(request.FileIds, FileOwnerType.TireServiceOrder, order.Id, "fileIds", ct);
            var approved = order.Result == TireServiceResult.Approved;
            lifecycle.Record(order.Kind == TireServiceKind.Retread ? OperationalEventType.TireRetreadCompleted : OperationalEventType.TireRepairCompleted,
                tire, null, at,
                order.Kind == TireServiceKind.Retread
                    ? approved
                        ? $"Recapagem nº {order.RetreadNumber} do pneu {tire.Code} concluída" +
                          (order.NewTreadDepthMm is { } t ? $" (sulco novo {TireLifecycle.Mm(t)})" : "") + ". Pneu de volta ao estoque."
                        : $"Recapagem do pneu {tire.Code} reprovada: {order.ResultNotes}. Pneu aguardando decisão."
                    : approved
                        ? $"Conserto do pneu {tire.Code} concluído. Pneu de volta ao estoque."
                        : $"Conserto do pneu {tire.Code} reprovado: {order.ResultNotes}. Pneu aguardando decisão.",
                new { tireId = tire.Id, serviceOrderId = order.Id, order.Result }, tireOnly: true);
            await db.SaveChangesAsync(ct);
        }, ct);
        return await GetAsync(orderId, ct);
    }

    public async Task<TireServiceOrderResponse> CancelAsync(Guid orderId, TireServiceCancelRequest request, CancellationToken ct)
    {
        var reason = request.Reason.TrimToNull() ?? throw ValidationErrors.ForField("reason", "Informe o motivo do cancelamento.");
        if (reason.Length > TireServiceOrder.TextMaxLength) throw ValidationErrors.ForField("reason", $"Use no máximo {TireServiceOrder.TextMaxLength} caracteres.");
        var order = await LoadOpenAsync(orderId, ct);
        var tire = order.Tire;
        var at = lifecycle.ResolveTime(null, tire);
        order.Status = TireServiceStatus.Cancelled;
        order.CancellationReason = reason;
        order.CompletedAt = at;
        // Back to evaluation: someone decides what happens to the tire next (stock, another provider, disposal).
        tire.Status = TireWorkflow.AfterService(order.Status, null);
        TireLifecycle.Touch(tire, at);
        lifecycle.Record(OperationalEventType.TireServiceCancelled, tire, null, at,
            $"{(order.Kind == TireServiceKind.Retread ? "Recapagem" : "Conserto")} do pneu {tire.Code} cancelado: {reason}. Pneu aguardando decisão.",
            new { tireId = tire.Id, serviceOrderId = order.Id }, tireOnly: true);
        await lifecycle.RunAsync(() => db.SaveChangesAsync(ct), ct);
        return await GetAsync(orderId, ct);
    }

    public async Task<IReadOnlyList<TireServiceOrderResponse>> ListAsync(Guid tireId, CancellationToken ct)
    {
        if (!await db.Tires.AnyAsync(t => t.Id == tireId, ct)) throw new NotFoundException("Pneu não encontrado. Ele pode ter sido excluído.");
        var ids = await db.TireServiceOrders.Where(o => o.TireId == tireId).OrderByDescending(o => o.SentAt).Select(o => o.Id).ToListAsync(ct);
        return await ToResponsesAsync(ids, ct);
    }

    public async Task<TireServiceOrderResponse> GetAsync(Guid orderId, CancellationToken ct) =>
        (await ToResponsesAsync([orderId], ct)).SingleOrDefault() ?? throw new NotFoundException("Serviço do pneu não encontrado.");

    // ---------- costs ----------

    public async Task<IReadOnlyList<TireCostResponse>> ListCostsAsync(Guid tireId, CancellationToken ct)
    {
        if (!await db.Tires.AnyAsync(t => t.Id == tireId, ct)) throw new NotFoundException("Pneu não encontrado. Ele pode ter sido excluído.");
        var costs = await db.TireCosts.Where(c => c.TireId == tireId).OrderByDescending(c => c.IncurredOn).ThenByDescending(c => c.CreatedAt).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, costs.Select(c => c.CreatedBy), ct);
        var canDelete = lifecycle.Can(Permissions.Tires.Edit);
        return costs.Select(c => new TireCostResponse(c.Id, c.Type, c.IncurredOn, c.Amount, c.Description, c.ServiceOrderId,
            canDelete && c.ServiceOrderId is null, names.Get(c.CreatedBy))).ToList();
    }

    public async Task<TireResponse> AddCostAsync(Guid tireId, TireCostRequest request, CancellationToken ct)
    {
        await costValidator.ValidateAndThrowAsync(request, ct);
        RequireCostEditor();
        var tire = await lifecycle.LoadTireAsync(tireId, ct);
        if (TireWorkflow.IsFinal(tire.Status)) throw new BusinessRuleException("Um pneu baixado não recebe novos custos.");
        db.TireCosts.Add(new TireCost
        {
            TireId = tire.Id, Type = request.Type!.Value, IncurredOn = request.IncurredOn!.Value, Amount = request.Amount!.Value,
            Description = request.Description.TrimToNull(),
        });
        // Summaries never carry R$ (ADR-034): the timeline is visible to people without the cost permission.
        lifecycle.Record(OperationalEventType.TireCostRecorded, tire, null, lifecycle.Now,
            $"Custo de {(request.Type == TireCostType.Installation ? "montagem" : "outro tipo")} registrado no pneu {tire.Code}" +
            (request.Description.TrimToNull() is { } d ? $": {d}." : "."), new { tireId = tire.Id, request.Type }, tireOnly: true);
        await db.SaveChangesAsync(ct);
        return await tires.GetAsync(tireId, ct);
    }

    public async Task<TireResponse> DeleteCostAsync(Guid tireId, Guid costId, CancellationToken ct)
    {
        RequireCostEditor();
        var cost = await db.TireCosts.SingleOrDefaultAsync(c => c.Id == costId && c.TireId == tireId, ct)
            ?? throw new NotFoundException("Custo não encontrado.");
        if (cost.ServiceOrderId is not null)
            throw new BusinessRuleException("Este custo veio de um conserto/recapagem concluído e faz parte do histórico do serviço.");
        db.TireCosts.Remove(cost);
        await db.SaveChangesAsync(ct);
        return await tires.GetAsync(tireId, ct);
    }

    // ---------- shared ----------

    /// <summary>Creates an open order in the unit of work and moves the tire to "under repair/retread" (also used by the removal).</summary>
    public static TireServiceOrder Open(IFleetDbContext db, Tire tire, TireServiceKind kind, DateTime at, Guid? workshopId, string? providerName,
        TireRepairType? repairType, string? description)
    {
        var order = new TireServiceOrder
        {
            TireId = tire.Id,
            Tire = tire,
            Kind = kind,
            SentAt = at,
            WorkshopId = workshopId,
            ProviderName = providerName,
            RepairType = kind == TireServiceKind.Repair ? repairType : null,
            RetreadNumber = kind == TireServiceKind.Retread ? tire.RetreadCount + 1 : null,
            Description = description,
        };
        db.TireServiceOrders.Add(order);
        tire.Status = TireWorkflow.WhileInService(kind);
        tire.StorageLocation = null;
        TireLifecycle.Touch(tire, at);
        return order;
    }

    public static string StartedSummary(Tire tire, TireServiceOrder order, string? provider) =>
        (order.Kind == TireServiceKind.Retread ? $"Pneu {tire.Code} enviado para a recapagem nº {order.RetreadNumber}" : $"Pneu {tire.Code} enviado para conserto") +
        (provider is not null ? $" ({provider})." : ".");

    private async Task CompleteCoreAsync(Tire tire, TireServiceOrder order, DateTime at, TireServiceResult result, decimal? cost,
        DateOnly? warrantyUntil, string? resultNotes, string? treadPattern, decimal? newTread, CancellationToken ct)
    {
        order.Status = TireServiceStatus.Completed;
        order.Result = result;
        order.CompletedAt = at;
        order.Cost = cost;
        order.WarrantyUntil = warrantyUntil;
        order.ResultNotes = resultNotes.TrimToNull();
        order.TreadPattern = treadPattern.TrimToNull();
        order.NewTreadDepthMm = newTread;
        TireLifecycle.Touch(tire, at);
        if (cost is > 0)
            db.TireCosts.Add(new TireCost
            {
                TireId = tire.Id, Type = order.Kind == TireServiceKind.Retread ? TireCostType.Retread : TireCostType.Repair,
                IncurredOn = lifecycle.Clock.ToBusinessDate(at), Amount = cost.Value, ServiceOrderId = order.Id,
                Description = order.Kind == TireServiceKind.Retread ? $"Recapagem nº {order.RetreadNumber}" : "Conserto",
            });
        if (result != TireServiceResult.Approved) return;

        // The finding that sent the tire away was handled: the alert it raised no longer describes the tire.
        tire.LastInspectionHasDamage = false;
        tire.LastWearPattern = null;
        var settings = await settingsService.CurrentAsync(ct);
        if (order.Kind == TireServiceKind.Retread)
        {
            tire.RetreadCount++;
            if (newTread is { } tread)
                await monitoring.RecordAsync(tire, null, null, at, new TireMeasurementInput(TireInspectionSource.Retread, null, tread, null, null,
                    TireCondition.Good, TireWearPattern.Normal, [], $"Banda nova da recapagem nº {order.RetreadNumber}."), settings, ct);
        }
        else
        {
            await monitoring.CheckRepeatedRepairsAsync(tire, at, ct);
            if (order.RepairType == TireRepairType.Puncture) await monitoring.CheckRepeatedPuncturesAsync(tire, at, fromRepair: true, ct);
            tire.RepairCount++;
        }
    }

    /// <summary>Typing money needs both: editing the tire and seeing its costs (the route already asks for tires.viewcosts).</summary>
    private void RequireCostEditor()
    {
        if (!lifecycle.Can(Permissions.Tires.Edit) || !lifecycle.CanSeeCosts)
            throw new ForbiddenException("Registrar ou excluir custos de pneus exige as permissões de editar pneus e de visualizar custos.");
    }

    private void RequireKindPermission(TireServiceKind kind)
    {
        if (kind == TireServiceKind.Retread && !lifecycle.Can(Permissions.Tires.Retread))
            throw new ForbiddenException("Você não tem permissão para registrar recapagens.");
        if (kind == TireServiceKind.Repair && !lifecycle.Can(Permissions.Tires.Repair))
            throw new ForbiddenException("Você não tem permissão para registrar consertos.");
    }

    private async Task<TireServiceOrder> LoadOpenAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.TireServiceOrders.Include(o => o.Tire).ThenInclude(t => t.Model).SingleOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException("Serviço do pneu não encontrado.");
        RequireKindPermission(order.Kind);
        if (order.Status != TireServiceStatus.Open) throw new BusinessRuleException("Este serviço já foi encerrado.");
        return order;
    }

    private async Task EnsureWorkshopAsync(Guid? workshopId, CancellationToken ct)
    {
        if (workshopId is { } id && !await db.Workshops.AnyAsync(w => w.Id == id, ct))
            throw ValidationErrors.ForField("workshopId", "Fornecedor não encontrado. Selecione uma oficina da lista.");
    }

    private async Task<string?> ProviderAsync(TireServiceOrder order, CancellationToken ct) =>
        order.WorkshopId is { } id ? await db.Workshops.Where(w => w.Id == id).Select(w => w.Name).SingleAsync(ct) : order.ProviderName;

    private static string RepairText(TireRepairType r) => r switch
    {
        TireRepairType.Puncture => "furo",
        TireRepairType.Vulcanization => "vulcanização",
        TireRepairType.BeadRepair => "talão",
        TireRepairType.SidewallRepair => "lateral",
        _ => "outro",
    };

    private async Task<IReadOnlyList<TireServiceOrderResponse>> ToResponsesAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var rows = await db.TireServiceOrders.Where(o => ids.Contains(o.Id))
            .Select(o => new { Order = o, o.Tire.Code, Workshop = o.Workshop != null ? o.Workshop.Name : null }).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, rows.Select(r => r.Order.CreatedBy), ct);
        var attachments = await files.ListByOwnersAsync(FileOwnerType.TireServiceOrder, ids.ToList(), ct);
        var costs = lifecycle.CanSeeCosts;
        return ids.Select(id => rows.SingleOrDefault(r => r.Order.Id == id)).Where(r => r is not null).Select(r =>
        {
            var o = r!.Order;
            var canManage = o.Status == TireServiceStatus.Open &&
                            lifecycle.Can(o.Kind == TireServiceKind.Retread ? Permissions.Tires.Retread : Permissions.Tires.Repair);
            return new TireServiceOrderResponse(o.Id, o.TireId, r.Code, o.Kind, o.Status, o.Result, o.InPlace, o.WorkshopId, r.Workshop ?? o.ProviderName,
                o.SentAt, o.CompletedAt, o.RepairType, o.RetreadNumber, o.TreadPattern, o.NewTreadDepthMm, costs ? o.Cost : null, o.WarrantyUntil,
                o.Description, o.ResultNotes, o.CancellationReason, names.Get(o.CreatedBy), attachments.GetValueOrDefault(o.Id) ?? [], canManage);
        }).ToList();
    }
}
