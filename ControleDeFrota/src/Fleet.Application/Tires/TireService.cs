using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Application.Operations;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Files;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tires;

public sealed record TireRequest
{
    /// <summary>Número de fogo. Blank = generated (PN-000001…).</summary>
    public string? Code { get; init; }
    public Guid? TireModelId { get; init; }
    public string? SerialNumber { get; init; }
    public string? Dot { get; init; }
    /// <summary>Blank = taken from the DOT date code when it has one.</summary>
    public DateOnly? ManufacturedOn { get; init; }
    public DateOnly? PurchasedOn { get; init; }
    /// <summary>Needs tires.viewcosts. Ignored on update for users without it (they never saw the value).</summary>
    public decimal? PurchasePrice { get; init; }
    public string? Supplier { get; init; }
    public decimal? OriginalTreadDepthMm { get; init; }
    /// <summary>Create only: tread of a used tire being registered (fleet migration). Blank = the original tread.</summary>
    public decimal? CurrentTreadDepthMm { get; init; }
    /// <summary>Create only: retreads the tire already had before being registered.</summary>
    public int RetreadCount { get; init; }
    public string? StorageLocation { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public enum TireInspectionFilter
{
    Overdue,
    UpToDate,
    Never,
}

public enum TireAlertFilter
{
    /// <summary>Tread at or below the warning depth (includes below minimum).</summary>
    NearReplacement,
    BelowMinimum,
    InspectionOverdue,
    DamageOrUnevenWear,
    AgeExceeded,
    /// <summary>Has an anomaly nobody reviewed.</summary>
    RequiresReview,
}

public sealed class TireListRequest : ListRequest
{
    public TireStatus? Status { get; set; }
    public Guid? TireModelId { get; set; }
    public string? Brand { get; set; }
    public string? Size { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? ImplementId { get; set; }
    public string? PositionCode { get; set; }
    public string? StorageLocation { get; set; }
    public DateOnly? ManufacturedFrom { get; set; }
    public DateOnly? ManufacturedTo { get; set; }
    /// <summary>Installed (current stint) in the period.</summary>
    public DateOnly? InstalledFrom { get; set; }
    public DateOnly? InstalledTo { get; set; }
    public int? MinRetreads { get; set; }
    public int? MaxRetreads { get; set; }
    public TireInspectionFilter? Inspection { get; set; }
    public TireAlertFilter? Alert { get; set; }
}

public sealed record TireLocationResponse(
    Guid InstallationId, Guid? VehicleId, Guid? ImplementId, string AssetLabel, string PositionCode, string PositionLabel, bool IsSpare,
    DateTime InstalledAt, int? InstalledOdometerKm);

public sealed record TireListItemResponse(
    Guid Id, string Code, Guid TireModelId, string Brand, string ModelName, string Size, TireStatus Status, string? SerialNumber, string? Dot,
    DateOnly? ManufacturedOn, TireLocationResponse? Location, string? StorageLocation, decimal? CurrentTreadDepthMm, int? CurrentKm,
    int RetreadCount, DateTime? LastInspectedAt, int AlertCount, TireAlertSeverity? TopAlertSeverity, int OpenAnomalyCount);

public sealed record TireCostSummaryResponse(
    decimal? Purchase, decimal Repairs, decimal Retreads, decimal Installation, decimal Other, decimal Total, int? Km, decimal? CostPerKm,
    string? CostPerKmNote);

public sealed record TireAnomalyResponse(
    Guid Id, TireAnomalyType Type, string Message, DateTime DetectedAt, DateTime? ReviewedAt, string? ReviewedByName, string? ReviewNotes);

public sealed record TireServiceOrderSummary(Guid Id, TireServiceKind Kind, DateTime SentAt, string? ProviderName, int? RetreadNumber);

public sealed record TireActions(
    bool CanEdit, bool CanDelete, bool CanInstall, bool CanRemove, bool CanReplace, bool CanTransfer, bool CanInspect,
    bool CanSendToRepair, bool CanSendToRetread, bool CanRepairInPlace, bool CanSendToEvaluation, bool CanReturnToStock, bool CanDispose,
    bool CanManageService, bool CanAddCost, bool CanReviewAnomalies, bool CanCorrectHistory);

public sealed record TireResponse(
    Guid Id, string Code, Guid TireModelId, string Brand, string ModelName, string Size, TireApplication Application,
    TireConstruction Construction, string? LoadIndex, string? SpeedRating, string? SerialNumber, string? Dot, DateOnly? ManufacturedOn,
    DateOnly? PurchasedOn, decimal? PurchasePrice, string? Supplier, decimal? OriginalTreadDepthMm, string? StorageLocation, string? Notes,
    TireStatus Status, TireLocationResponse? Location, TireServiceOrderSummary? OpenServiceOrder,
    int? CurrentKm, int AccumulatedKm, bool HasUnmeasuredDistance, int RetreadCount, int RepairCount,
    decimal? CurrentTreadDepthMm, DateTime? TreadMeasuredAt, decimal? TreadUsedPercent, DateTime? LastInspectedAt, DateTime? NextInspectionDue,
    bool CanSeeCosts, TireCostSummaryResponse? Costs,
    IReadOnlyList<TireAlert> Alerts, IReadOnlyList<TireAnomalyResponse> Anomalies,
    DateTime? DisposedAt, TireDisposalReason? DisposalReason, string? DisposalDestination, string? DisposalNotes, string? DisposedByName,
    IReadOnlyList<FileResponse> Files, TireActions Actions, string? CreatedByName, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record TireInstallationResponse(
    Guid Id, Guid? VehicleId, Guid? ImplementId, string AssetLabel, string PositionCode, string PositionLabel, bool IsSpare,
    DateTime InstalledAt, int? InstalledOdometerKm, TireInstallReason InstallReason, string? InstalledByName,
    DateTime? RemovedAt, int? RemovedOdometerKm, TireRemovalReason? RemovalReason, TireRemovalDestination? RemovalDestination,
    string? RemovedByName, int? DistanceKm, bool IsRotation, string? Notes, string? RemovalNotes);

public sealed record TireCompatibilityResponse(TireCompatibilityStatus Status, IReadOnlyList<string> Messages, bool PositionOccupied, string? OccupiedBy);

public sealed class TireRequestValidator : AbstractValidator<TireRequest>
{
    public TireRequestValidator(IClock clock)
    {
        RuleFor(x => x.Code).MaxLen(Tire.CodeMaxLength)
            .Matches(@"^[0-9A-Za-z\-./ ]*$").WithMessage("Número de fogo: use letras, números, hífen, ponto ou barra.");
        RuleFor(x => x.TireModelId).NotNull().WithMessage("Modelo: campo obrigatório.");
        RuleFor(x => x.SerialNumber).MaxLen(Tire.SerialMaxLength);
        RuleFor(x => x.Dot).MaxLen(Tire.DotMaxLength);
        RuleFor(x => x.ManufacturedOn).Must(d => d is null || d <= clock.Today).WithMessage("A data de fabricação não pode ser futura.");
        RuleFor(x => x.PurchasedOn).Must(d => d is null || d <= clock.Today).WithMessage("A data de compra não pode ser futura.");
        RuleFor(x => x.PurchasePrice).InclusiveBetween(0m, TireCost.MaxAmount).WithMessage("Valor de compra inválido.");
        RuleFor(x => x.Supplier).MaxLen(Tire.SupplierMaxLength);
        RuleFor(x => x.OriginalTreadDepthMm).InclusiveBetween(1m, 40m).WithMessage("Sulco original deve estar entre 1 e 40 mm.");
        RuleFor(x => x.CurrentTreadDepthMm).InclusiveBetween(0m, 40m).WithMessage("Sulco atual deve estar entre 0 e 40 mm.");
        RuleFor(x => x.RetreadCount).InclusiveBetween(0, 10).WithMessage("Recapagens anteriores: de 0 a 10.");
        RuleFor(x => x.StorageLocation).MaxLen(Tire.StorageMaxLength);
        RuleFor(x => x.Notes).MaxLen(Tire.NotesMaxLength);
    }
}

public sealed class TireListRequestValidator : AbstractValidator<TireListRequest>
{
    public TireListRequestValidator()
    {
        this.ValidPeriod(x => x.ManufacturedFrom, x => x.ManufacturedTo, "manufacturedTo");
        this.ValidPeriod(x => x.InstalledFrom, x => x.InstalledTo, "installedTo");
        RuleFor(x => x.MaxRetreads).Must((x, max) => max is null || x.MinRetreads is null || x.MinRetreads <= max)
            .WithMessage("O máximo de recapagens deve ser maior ou igual ao mínimo.");
    }
}

/// <summary>
/// Tire registry and read models (ADR-035/036): identity, location, lifecycle cost, alerts. The lifecycle operations are in
/// TireOperationsService; this service never changes status or the fast-read fields.
/// </summary>
public sealed class TireService(
    IFleetDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    FileService files,
    TireLifecycle lifecycle,
    TireMonitoring monitoring,
    TireSettingsService settingsService,
    OperationalHistoryService history,
    IValidator<TireRequest> validator,
    IValidator<TireListRequest> listValidator)
{
    private static readonly SortMap<Tire> Sorts = new SortMap<Tire>("code")
        .Add("code", t => t.Code)
        .Add("status", t => t.Status)
        .Add("brand", t => t.Model.Brand)
        .Add("size", t => t.Model.Size)
        .Add("treadDepth", t => t.CurrentTreadDepthMm)
        .Add("km", t => t.AccumulatedKm)
        .Add("retreads", t => t.RetreadCount)
        .Add("lastInspectedAt", t => t.LastInspectedAt)
        .Add("manufacturedOn", t => t.ManufacturedOn)
        .Add("createdAt", t => t.CreatedAt);

    public async Task<PagedResult<TireListItemResponse>> ListAsync(TireListRequest request, CancellationToken ct)
    {
        await listValidator.ValidateAndThrowAsync(request, ct);
        var settings = await settingsService.CurrentAsync(ct);
        // A model with tires is never deleted (TireModelService), so the Include never hides a tire.
        var query = Filter(db.Tires.Include(t => t.Model), request, settings);
        var page = await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, t => t, ct);
        return new PagedResult<TireListItemResponse>(await ToListItemsAsync(page.Items, settings, ct), page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>Shared by the list and the reports: every filter is SQL (seção 46).</summary>
    public IQueryable<Tire> Filter(IQueryable<Tire> query, TireListRequest r, TireSettings settings)
    {
        if (r.Status is { } status) query = query.Where(t => t.Status == status);
        if (r.TireModelId is { } modelId) query = query.Where(t => t.TireModelId == modelId);
        if (r.Brand.TrimToNull() is { } brand) query = query.Where(t => t.Model.Brand == brand);
        if (r.Size.TrimToNull() is { } size)
        {
            var normalized = TireSizes.Normalize(size);
            query = query.Where(t => t.Model.Size.Contains(normalized));
        }
        if (r.VehicleId is { } vehicleId)
            query = query.Where(t => db.TireInstallations.Any(i => i.TireId == t.Id && i.RemovedAt == null && i.VehicleId == vehicleId));
        if (r.ImplementId is { } implementId)
            query = query.Where(t => db.TireInstallations.Any(i => i.TireId == t.Id && i.RemovedAt == null && i.ImplementId == implementId));
        if (r.PositionCode.TrimToNull() is { } position)
        {
            var code = position.ToUpperInvariant();
            query = query.Where(t => db.TireInstallations.Any(i => i.TireId == t.Id && i.RemovedAt == null && i.PositionCode == code));
        }
        if (r.StorageLocation.TrimToNull() is { } storage) query = query.Where(t => t.StorageLocation != null && t.StorageLocation.Contains(storage));
        if (r.ManufacturedFrom is { } mFrom) query = query.Where(t => t.ManufacturedOn >= mFrom);
        if (r.ManufacturedTo is { } mTo) query = query.Where(t => t.ManufacturedOn <= mTo);
        if (r.InstalledFrom is { } iFrom)
        {
            var start = clock.StartOfBusinessDayUtc(iFrom);
            query = query.Where(t => db.TireInstallations.Any(i => i.TireId == t.Id && i.RemovedAt == null && i.InstalledAt >= start));
        }
        if (r.InstalledTo is { } iTo)
        {
            var end = clock.StartOfBusinessDayUtc(iTo.AddDays(1));
            query = query.Where(t => db.TireInstallations.Any(i => i.TireId == t.Id && i.RemovedAt == null && i.InstalledAt < end));
        }
        if (r.MinRetreads is { } minR) query = query.Where(t => t.RetreadCount >= minR);
        if (r.MaxRetreads is { } maxR) query = query.Where(t => t.RetreadCount <= maxR);
        var dueBefore = TireAlertPolicy.InspectionDueBefore(settings, clock.UtcNow);
        query = r.Inspection switch
        {
            TireInspectionFilter.Overdue => WhereInspectionOverdue(query, settings, clock.UtcNow),
            TireInspectionFilter.UpToDate => query.Where(t => t.Status == TireStatus.Installed && t.InspectionReferenceAt >= dueBefore),
            TireInspectionFilter.Never => query.Where(t => t.LastInspectedAt == null && t.Status != TireStatus.Disposed),
            _ => query,
        };
        if (r.Alert is { } alert) query = WhereAlert(query, alert, settings);
        if (r.SearchTerm is { } term)
        {
            var upper = term.ToUpperInvariant();
            query = query.Where(t => t.Code.Contains(upper) || (t.SerialNumber != null && t.SerialNumber.Contains(term)) ||
                                     (t.Dot != null && t.Dot.Contains(upper)) || t.Model.Brand.Contains(term) || t.Model.Name.Contains(term));
        }
        return query;
    }

    /// <summary>SQL forms of the alert policy — the same thresholds as <see cref="TireAlertPolicy"/> (tested together).</summary>
    public IQueryable<Tire> WhereAlert(IQueryable<Tire> query, TireAlertFilter alert, TireSettings settings)
    {
        var active = query.Where(t => t.Status != TireStatus.Disposed);
        var ageLimit = TireAlertPolicy.AgeLimit(settings, clock.Today);
        return alert switch
        {
            TireAlertFilter.NearReplacement => active.Where(t => t.CurrentTreadDepthMm != null && t.CurrentTreadDepthMm <= settings.TreadWarningDepthMm),
            TireAlertFilter.BelowMinimum => active.Where(t => t.CurrentTreadDepthMm != null && t.CurrentTreadDepthMm <= settings.MinTreadDepthMm),
            TireAlertFilter.InspectionOverdue => WhereInspectionOverdue(active, settings, clock.UtcNow),
            TireAlertFilter.DamageOrUnevenWear => active.Where(t => t.LastInspectionHasDamage ||
                (t.LastWearPattern != null && t.LastWearPattern != TireWearPattern.Normal && t.LastWearPattern != TireWearPattern.Unknown)),
            TireAlertFilter.AgeExceeded => settings.MaxAgeYears > 0 ? active.Where(t => t.ManufacturedOn != null && t.ManufacturedOn < ageLimit) : active.Where(_ => false),
            TireAlertFilter.RequiresReview => active.Where(t => db.TireAnomalies.Any(a => a.TireId == t.Id && a.ReviewedAt == null)),
            _ => query,
        };
    }

    public static IQueryable<Tire> WhereInspectionOverdue(IQueryable<Tire> query, TireSettings settings, DateTime now)
    {
        if (settings.InspectionIntervalDays <= 0) return query.Where(_ => false);
        var dueBefore = TireAlertPolicy.InspectionDueBefore(settings, now);
        return query.Where(t => t.Status == TireStatus.Installed && t.InspectionReferenceAt != null && t.InspectionReferenceAt < dueBefore);
    }

    public async Task<TireResponse> GetAsync(Guid id, CancellationToken ct) => await ToResponseAsync(await lifecycle.LoadTireAsync(id, ct), ct);

    public async Task<TireResponse> CreateAsync(TireRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        if (request.PurchasePrice is not null && !lifecycle.CanSeeCosts)
            throw new ForbiddenException("Informar o valor de compra exige a permissão de visualizar custos de pneus.");
        var model = await LoadModelAsync(request.TireModelId, previous: null, ct);
        var settings = await settingsService.CurrentAsync(ct);
        var sequence = (await db.Tires.MaxAsync(t => (int?)t.Sequence, ct) ?? 0) + 1;
        var code = request.Code.TrimToNull()?.ToUpperInvariant();
        if (code is null)
            while (await db.Tires.AnyAsync(t => t.Code == Tire.FormatCode(sequence), ct)) sequence++;

        var tire = new Tire
        {
            Sequence = sequence,
            Code = code ?? Tire.FormatCode(sequence),
            TireModelId = model.Id,
            Model = model,
            RetreadCount = request.RetreadCount,
            PurchasePrice = request.PurchasePrice,
        };
        await ApplyAsync(request, tire, ct);
        tire.CurrentTreadDepthMm = tire.OriginalTreadDepthMm;
        db.Tires.Add(tire);

        await lifecycle.RunAsync(async () =>
        {
            var now = lifecycle.Now;
            if (request.CurrentTreadDepthMm is { } tread && tread != tire.OriginalTreadDepthMm)
                await monitoring.RecordAsync(tire, null, null, now,
                    new TireMeasurementInput(TireInspectionSource.Registration, null, tread, null, null, TireCondition.Good,
                        TireWearPattern.Unknown, [], "Sulco informado no cadastro."), settings, ct);
            await files.AttachAsync(request.FileIds, FileOwnerType.Tire, tire.Id, "fileIds", ct);
            lifecycle.Record(OperationalEventType.TireRegistered, tire, null, now,
                $"Pneu {TireLifecycle.Describe(tire)} cadastrado" + (tire.StorageLocation is { } s ? $" em {s}." : "."),
                new { tireId = tire.Id, tire.TireModelId, tire.RetreadCount });
            await db.SaveChangesAsync(ct);
        }, ct);
        return await GetAsync(tire.Id, ct);
    }

    public async Task<TireResponse> UpdateAsync(Guid id, TireRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var tire = await lifecycle.LoadTireAsync(id, ct);
        if (!TireWorkflow.CanEdit(tire.Status))
            throw new BusinessRuleException("Um pneu baixado não pode ser editado: o cadastro faz parte do histórico.");
        if (request.TireModelId != tire.TireModelId)
        {
            // The model carries the size every position was checked against.
            if (tire.Status == TireStatus.Installed)
                throw ValidationErrors.ForField("tireModelId", "Remova o pneu antes de trocar o modelo: a medida dele foi conferida com a posição atual.");
            tire.Model = await LoadModelAsync(request.TireModelId, tire.TireModelId, ct);
            tire.TireModelId = tire.Model.Id;
        }
        if (request.Code.TrimToNull() is { } code) tire.Code = code.ToUpperInvariant();
        // Without the cost permission the price was never shown, so the (empty) value sent back is not a change.
        if (lifecycle.CanSeeCosts) tire.PurchasePrice = request.PurchasePrice;
        await ApplyAsync(request, tire, ct);
        tire.Version++;
        await lifecycle.RunAsync(() => db.SaveChangesAsync(ct), ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Only a tire registered by mistake (no lifecycle at all) is deleted; anything with history is disposed instead.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var tire = await lifecycle.LoadTireAsync(id, ct);
        if (await db.TireInstallations.AnyAsync(i => i.TireId == id, ct) || await db.TireServiceOrders.AnyAsync(o => o.TireId == id, ct) ||
            await db.TireInspections.AnyAsync(i => i.TireId == id && i.Source != TireInspectionSource.Registration, ct) ||
            await db.TireCosts.AnyAsync(c => c.TireId == id, ct))
            throw new BusinessRuleException("Este pneu já tem histórico (instalação, inspeção, serviço ou custo) e não pode ser excluído. Dê baixa nele.");
        db.Tires.Remove(tire);
        await db.SaveChangesAsync(ct);
    }

    public Task<PagedResult<HistoryEntryResponse>> HistoryAsync(Guid id, HistoryRequest request, CancellationToken ct) =>
        history.ForTireAsync(id, request, ct);

    public async Task<PagedResult<TireInstallationResponse>> InstallationsAsync(Guid id, ListRequest request, CancellationToken ct)
    {
        if (!await db.Tires.AnyAsync(t => t.Id == id, ct)) throw new NotFoundException("Pneu não encontrado. Ele pode ter sido excluído.");
        var page = await db.TireInstallations.Where(i => i.TireId == id).OrderByDescending(i => i.InstalledAt).ThenByDescending(i => i.CreatedAt)
            .ToPagedResultAsync(request, i => new
            {
                Stint = i,
                Plate = i.Vehicle != null ? i.Vehicle.LicensePlate : i.Implement != null ? i.Implement.LicensePlate : "",
            }, ct);
        var names = await UserNames.LoadAsync(db, page.Items.SelectMany(x => new[] { x.Stint.CreatedBy, x.Stint.RemovedBy }), ct);
        return new PagedResult<TireInstallationResponse>(page.Items.Select(x => ToResponse(x.Stint, x.Plate, names)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }

    public static TireInstallationResponse ToResponse(TireInstallation i, string plate, IReadOnlyDictionary<Guid, string> names) => new(
        i.Id, i.VehicleId, i.ImplementId, LicensePlate.Format(plate), i.PositionCode, i.PositionLabel, i.IsSpare, i.InstalledAt,
        i.InstalledOdometerKm, i.InstallReason, names.Get(i.CreatedBy), i.RemovedAt, i.RemovedOdometerKm, i.RemovalReason, i.RemovalDestination,
        names.Get(i.RemovedBy), i.DistanceKm, i.RotationId is not null || i.RemovalRotationId is not null, i.Notes, i.RemovalNotes);

    /// <summary>Preview for the installation screens (seção 33): what can and cannot be verified, before anything is saved.</summary>
    public async Task<TireCompatibilityResponse> CompatibilityAsync(Guid id, Guid? vehicleId, Guid? implementId, string? positionCode, CancellationToken ct)
    {
        var tire = await lifecycle.LoadTireAsync(id, ct);
        var asset = await lifecycle.LoadAssetAsync(vehicleId, implementId, ct);
        var position = TireLifecycle.RequirePosition(asset, positionCode);
        var occupant = await asset.OpenStints(db.TireInstallations).Where(i => i.PositionCode == position.Code)
            .Select(i => i.Tire.Code).FirstOrDefaultAsync(ct);
        var compatibility = await CheckAsync(tire, asset, position, ct);
        return new TireCompatibilityResponse(compatibility.Status, compatibility.Messages, occupant is not null, occupant);
    }

    /// <summary>Compatibility of a tire with a position, including the size of the dual partner currently installed.</summary>
    public async Task<TireCompatibility> CheckAsync(Tire tire, TireAsset asset, TirePosition position, CancellationToken ct, ISet<string>? vacated = null)
    {
        string? partnerSize = null;
        if (TirePositions.DualPartner(position) is { } partner && vacated?.Contains(partner) != true)
            partnerSize = await asset.OpenStints(db.TireInstallations).Where(i => i.PositionCode == partner && i.TireId != tire.Id)
                .Select(i => i.Tire.Model.Size).FirstOrDefaultAsync(ct);
        return TireCompatibility.Check(position, tire.Model.Size, tire.Model.Application, partnerSize);
    }

    // ---------- helpers ----------

    private async Task ApplyAsync(TireRequest request, Tire tire, CancellationToken ct)
    {
        if (await db.Tires.AnyAsync(t => t.Code == tire.Code && t.Id != tire.Id, ct))
            throw new ConflictException($"Já existe um pneu com o número de fogo {tire.Code}.", "code");
        tire.SerialNumber = request.SerialNumber.TrimToNull();
        tire.Dot = TireDot.Normalize(request.Dot);
        tire.ManufacturedOn = request.ManufacturedOn ?? TireDot.ManufacturedOn(tire.Dot);
        if (tire.ManufacturedOn > clock.Today)
            throw ValidationErrors.ForField("dot", "O código DOT indica uma data de fabricação futura. Confira os quatro últimos dígitos (semana e ano).");
        tire.PurchasedOn = request.PurchasedOn;
        if (tire.PurchasedOn is { } bought && tire.ManufacturedOn is { } made && bought < made)
            throw ValidationErrors.ForField("purchasedOn", "A data de compra não pode ser anterior à data de fabricação.");
        tire.Supplier = request.Supplier.TrimToNull();
        tire.OriginalTreadDepthMm = request.OriginalTreadDepthMm ?? tire.Model.OriginalTreadDepthMm;
        tire.StorageLocation = tire.Status == TireStatus.Installed ? null : request.StorageLocation.TrimToNull();
        tire.Notes = request.Notes.TrimToNull();
    }

    private async Task<TireModel> LoadModelAsync(Guid? modelId, Guid? previous, CancellationToken ct)
    {
        var model = await db.TireModels.SingleOrDefaultAsync(m => m.Id == modelId, ct)
            ?? throw ValidationErrors.ForField("tireModelId", "Modelo de pneu não encontrado. Selecione um modelo da lista.");
        if (!model.IsActive && model.Id != previous)
            throw ValidationErrors.ForField("tireModelId", $"O modelo {model.Brand} {model.Name} está inativo. Escolha outro ou reative-o.");
        return model;
    }

    private sealed record StintView(Guid TireId, TireLocationResponse Location, int? VehicleCurrentKm);

    private async Task<Dictionary<Guid, StintView>> OpenStintsAsync(IReadOnlyCollection<Guid> tireIds, CancellationToken ct) =>
        (await db.TireInstallations.Where(i => tireIds.Contains(i.TireId) && i.RemovedAt == null)
            .Select(i => new
            {
                i.TireId, i.Id, i.VehicleId, i.ImplementId, i.PositionCode, i.PositionLabel, i.IsSpare, i.InstalledAt, i.InstalledOdometerKm,
                Plate = i.Vehicle != null ? i.Vehicle.LicensePlate : i.Implement!.LicensePlate,
                VehicleKm = i.Vehicle != null ? (int?)i.Vehicle.CurrentOdometerKm : null,
            }).ToListAsync(ct))
        .ToDictionary(x => x.TireId, x => new StintView(x.TireId,
            new TireLocationResponse(x.Id, x.VehicleId, x.ImplementId, LicensePlate.Format(x.Plate), x.PositionCode, x.PositionLabel, x.IsSpare,
                x.InstalledAt, x.InstalledOdometerKm), x.VehicleKm));

    private static int? CurrentKm(Tire tire, StintView? stint) =>
        stint is null ? tire.AccumulatedKm
        : TireMileage.Current(tire.AccumulatedKm, true, stint.Location.IsSpare, stint.Location.InstalledOdometerKm, stint.VehicleCurrentKm);

    public async Task<IReadOnlyList<TireListItemResponse>> ToListItemsAsync(IReadOnlyList<Tire> tires, TireSettings settings, CancellationToken ct)
    {
        var ids = tires.Select(t => t.Id).ToList();
        var stints = await OpenStintsAsync(ids, ct);
        var anomalies = await db.TireAnomalies.Where(a => ids.Contains(a.TireId) && a.ReviewedAt == null)
            .GroupBy(a => a.TireId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return tires.Select(t =>
        {
            var model = t.Model;
            var stint = stints.GetValueOrDefault(t.Id);
            var alerts = TireAlertPolicy.Evaluate(t, settings, clock.UtcNow, clock.Today);
            return new TireListItemResponse(t.Id, t.Code, t.TireModelId, model.Brand, model.Name, model.Size, t.Status, t.SerialNumber, t.Dot,
                t.ManufacturedOn, stint?.Location, t.StorageLocation, t.CurrentTreadDepthMm, CurrentKm(t, stint), t.RetreadCount, t.LastInspectedAt,
                alerts.Count, alerts.Count == 0 ? null : alerts.Max(a => a.Severity), anomalies.GetValueOrDefault(t.Id));
        }).ToList();
    }

    public async Task<TireResponse> ToResponseAsync(Tire tire, CancellationToken ct)
    {
        var settings = await settingsService.CurrentAsync(ct);
        var stint = (await OpenStintsAsync([tire.Id], ct)).GetValueOrDefault(tire.Id);
        var order = await db.TireServiceOrders.Where(o => o.TireId == tire.Id && o.Status == TireServiceStatus.Open)
            .Select(o => new TireServiceOrderSummary(o.Id, o.Kind, o.SentAt, o.Workshop != null ? o.Workshop.Name : o.ProviderName, o.RetreadNumber))
            .FirstOrDefaultAsync(ct);
        var anomalies = await db.TireAnomalies.Where(a => a.TireId == tire.Id)
            .OrderBy(a => a.ReviewedAt != null).ThenByDescending(a => a.DetectedAt).Take(20).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, anomalies.Select(a => a.ReviewedBy).Append(tire.CreatedBy).Append(tire.DisposedBy), ct);
        var attachments = await files.ListByOwnerAsync(FileOwnerType.Tire, tire.Id, ct);
        var km = CurrentKm(tire, stint);
        var costs = lifecycle.CanSeeCosts ? await CostSummaryAsync(tire, km, ct) : null;
        var nextInspection = tire.Status == TireStatus.Installed && settings.InspectionIntervalDays > 0 && tire.InspectionReferenceAt is { } reference
            ? reference.AddDays(settings.InspectionIntervalDays) : (DateTime?)null;
        decimal? usedPercent = tire.OriginalTreadDepthMm is > 0 && tire.CurrentTreadDepthMm is { } current
            ? Math.Clamp(Math.Round((tire.OriginalTreadDepthMm.Value - current) / tire.OriginalTreadDepthMm.Value * 100m, 0), 0, 100) : null;

        return new TireResponse(
            tire.Id, tire.Code, tire.TireModelId, tire.Model.Brand, tire.Model.Name, tire.Model.Size, tire.Model.Application, tire.Model.Construction,
            tire.Model.LoadIndex, tire.Model.SpeedRating, tire.SerialNumber, tire.Dot, tire.ManufacturedOn, tire.PurchasedOn,
            lifecycle.CanSeeCosts ? tire.PurchasePrice : null, tire.Supplier, tire.OriginalTreadDepthMm, tire.StorageLocation, tire.Notes,
            tire.Status, stint?.Location, order, km, tire.AccumulatedKm, tire.HasUnmeasuredDistance, tire.RetreadCount, tire.RepairCount,
            tire.CurrentTreadDepthMm, tire.TreadMeasuredAt, usedPercent, tire.LastInspectedAt, nextInspection,
            lifecycle.CanSeeCosts, costs, TireAlertPolicy.Evaluate(tire, settings, clock.UtcNow, clock.Today),
            anomalies.Select(a => new TireAnomalyResponse(a.Id, a.Type, a.Message, a.DetectedAt, a.ReviewedAt, names.Get(a.ReviewedBy), a.ReviewNotes)).ToList(),
            tire.DisposedAt, tire.DisposalReason, tire.DisposalDestination, tire.DisposalNotes, names.Get(tire.DisposedBy),
            attachments, Actions(tire, order, anomalies.Any(a => a.ReviewedAt is null)), names.Get(tire.CreatedBy), tire.CreatedAt, tire.UpdatedAt);
    }

    /// <summary>Lifecycle cost (seções 25/26): purchase + Σ cost rows by type, summed in the database.</summary>
    public async Task<TireCostSummaryResponse> CostSummaryAsync(Tire tire, int? km, CancellationToken ct)
    {
        var byType = await db.TireCosts.Where(c => c.TireId == tire.Id).GroupBy(c => c.Type)
            .Select(g => new { g.Key, Total = g.Sum(c => c.Amount) }).ToListAsync(ct);
        decimal Of(TireCostType type) => byType.SingleOrDefault(x => x.Key == type)?.Total ?? 0m;
        var total = (tire.PurchasePrice ?? 0) + byType.Sum(x => x.Total);
        var kmValue = km ?? 0;
        var perKm = TireCostPolicy.CostPerKm(total, kmValue, tire.HasUnmeasuredDistance);
        string? note = perKm is not null ? null
            : tire.HasUnmeasuredDistance ? "Parte da vida do pneu foi em implemento ou sem hodômetro: o km acumulado está incompleto."
            : total <= 0 ? "Sem custos registrados."
            : $"Disponível a partir de {BrazilianFormat.Number(TireCostPolicy.MinKmForCostPerKm)} km rodados.";
        return new TireCostSummaryResponse(tire.PurchasePrice, Of(TireCostType.Repair), Of(TireCostType.Retread), Of(TireCostType.Installation),
            Of(TireCostType.Other), total, km, perKm, note);
    }

    private TireActions Actions(Tire tire, TireServiceOrderSummary? order, bool hasOpenAnomaly)
    {
        bool Can(string p) => currentUser.HasPermission(p);
        var s = tire.Status;
        return new TireActions(
            CanEdit: Can(Permissions.Tires.Edit) && TireWorkflow.CanEdit(s),
            CanDelete: Can(Permissions.Tires.Edit) && s == TireStatus.InStock && tire.LastMovementAt is null,
            CanInstall: Can(Permissions.Tires.Install) && TireWorkflow.CanInstall(s),
            CanRemove: Can(Permissions.Tires.Remove) && TireWorkflow.CanRemove(s),
            CanReplace: Can(Permissions.Tires.Remove) && Can(Permissions.Tires.Install) && TireWorkflow.CanRemove(s),
            CanTransfer: Can(Permissions.Tires.Remove) && Can(Permissions.Tires.Install) && TireWorkflow.CanRemove(s),
            CanInspect: Can(Permissions.Tires.Inspect) && TireWorkflow.CanInspect(s),
            CanSendToRepair: Can(Permissions.Tires.Repair) && TireWorkflow.CanSendToService(s) && order is null,
            CanSendToRetread: Can(Permissions.Tires.Retread) && TireWorkflow.CanSendToService(s) && order is null,
            CanRepairInPlace: Can(Permissions.Tires.Repair) && TireWorkflow.CanRepairInPlace(s),
            CanSendToEvaluation: Can(Permissions.Tires.Inspect) && TireWorkflow.CanSendToEvaluation(s),
            CanReturnToStock: Can(Permissions.Tires.Inspect) && TireWorkflow.CanReturnToStock(s),
            CanDispose: Can(Permissions.Tires.Dispose) && TireWorkflow.CanDispose(s),
            CanManageService: order is not null && Can(order.Kind == TireServiceKind.Retread ? Permissions.Tires.Retread : Permissions.Tires.Repair),
            CanAddCost: Can(Permissions.Tires.Edit) && Can(Permissions.Tires.ViewCosts) && !TireWorkflow.IsFinal(s),
            CanReviewAnomalies: Can(Permissions.Tires.Edit) && hasOpenAnomaly,
            CanCorrectHistory: Can(Permissions.Tires.Edit));
    }
}
