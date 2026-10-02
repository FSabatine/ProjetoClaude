using System.Text.Json;
using Fleet.Application.Assignments;
using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Application.Mileage;
using Fleet.Application.Operations;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Drivers;
using Fleet.Domain.Files;
using Fleet.Domain.Fuel;
using Fleet.Domain.Mileage;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Fuel;

/// <summary>Fields shared by the create and the correction requests (one validator for both).</summary>
public interface IFuelingFields
{
    Guid? DriverId { get; }
    Guid? FuelStationId { get; }
    Guid? FuelTypeId { get; }
    DateTime? FueledAt { get; }
    int? OdometerKm { get; }
    decimal? Quantity { get; }
    decimal? UnitPrice { get; }
    /// <summary>Optional total typed from the receipt: only checked against quantity × price, never stored as is.</summary>
    decimal? TotalAmount { get; }
    bool IsFullTank { get; }
    PaymentMethod? PaymentMethod { get; }
    string? ReceiptNumber { get; }
    string? Notes { get; }
}

public sealed record FuelingRequest : IFuelingFields
{
    public Guid? VehicleId { get; init; }
    public Guid? DriverId { get; init; }
    public Guid? FuelStationId { get; init; }
    public Guid? FuelTypeId { get; init; }
    public DateTime? FueledAt { get; init; }
    public int? OdometerKm { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? TotalAmount { get; init; }
    public bool IsFullTank { get; init; } = true;
    public PaymentMethod? PaymentMethod { get; init; }
    public string? ReceiptNumber { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public sealed record FuelingCorrectionRequest : IFuelingFields
{
    public Guid? DriverId { get; init; }
    public Guid? FuelStationId { get; init; }
    public Guid? FuelTypeId { get; init; }
    public DateTime? FueledAt { get; init; }
    public int? OdometerKm { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? TotalAmount { get; init; }
    public bool IsFullTank { get; init; } = true;
    public PaymentMethod? PaymentMethod { get; init; }
    public string? ReceiptNumber { get; init; }
    public string? Notes { get; init; }
    public string? Reason { get; init; }
}

public sealed record FuelingReasonRequest
{
    public string? Reason { get; init; }
}

public sealed record FuelingFilesRequest
{
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public sealed class FuelingListRequest : ListRequest
{
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? FuelStationId { get; set; }
    public Guid? FuelTypeId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public FuelingStatus? Status { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    /// <summary>Price filters reveal prices: they need fuel.viewcosts.</summary>
    public decimal? MinUnitPrice { get; set; }
    public decimal? MaxUnitPrice { get; set; }
    /// <summary>true = only fuelings with at least one anomaly (reviewed or not).</summary>
    public bool? HasAnomaly { get; set; }
    public FuelAnomalyType? AnomalyType { get; set; }
}

public sealed record FuelingListItemResponse(
    Guid Id, Guid VehicleId, string LicensePlate, Guid? DriverId, string? DriverName, Guid? FuelStationId, string? FuelStationName,
    Guid FuelTypeId, string FuelTypeName, FuelUnit Unit, DateTime FueledAt, int OdometerKm, decimal Quantity,
    decimal? UnitPrice, decimal? TotalAmount, bool IsFullTank, PaymentMethod PaymentMethod, FuelingStatus Status,
    ConsumptionResult ConsumptionResult, decimal? Consumption, int AnomalyCount);

public sealed record FuelingAnomalyResponse(
    Guid Id, FuelAnomalyType Type, string Message, decimal? ExpectedValue, decimal? ActualValue, DateTime DetectedAt,
    DateTime? ReviewedAt, string? ReviewedByName);

public sealed record FuelingChange(string Field, string Label, string? From, string? To);

public sealed record FuelingCorrectionResponse(
    Guid Id, DateTime CorrectedAt, string? CorrectedByName, string Reason, IReadOnlyList<FuelingChange> Changes);

public sealed record FuelingActions(bool CanCorrect, bool CanCancel, bool CanReview);

public sealed record FuelingResponse(
    Guid Id, Guid VehicleId, string LicensePlate, Guid? DriverId, string? DriverName, Guid? FuelStationId, string? FuelStationName,
    Guid FuelTypeId, string FuelTypeName, FuelUnit Unit, DateTime FueledAt, int OdometerKm, decimal Quantity,
    decimal? UnitPrice, decimal? TotalAmount, bool IsFullTank, PaymentMethod PaymentMethod, string? ReceiptNumber, string? Notes,
    FuelingSource Source, FuelingStatus Status, bool CanSeeCosts,
    ConsumptionResult ConsumptionResult, int? SegmentDistanceKm, decimal? SegmentQuantity, decimal? SegmentCost, decimal? Consumption,
    decimal? ExpectedConsumption, ConsumptionBaselineSource? BaselineSource, decimal? ConsumptionDeviationPercent,
    OdometerReadingStatus? OdometerReadingStatus,
    DateTime? ReviewedAt, string? ReviewedByName, string? ReviewNotes,
    DateTime? CancelledAt, string? CancelledByName, string? CancellationReason,
    string? CreatedByName, DateTime CreatedAt,
    IReadOnlyList<FuelingAnomalyResponse> Anomalies, IReadOnlyList<FuelingCorrectionResponse> Corrections,
    IReadOnlyList<FileResponse> Files, FuelingActions Actions);

/// <summary>What the quick form can reuse for a vehicle (seção 27). Suggestions only — the user confirms every value.</summary>
public sealed record FuelingFormDefaultsResponse(
    Guid VehicleId, string LicensePlate, VehicleFuelType VehicleFuelType, VehicleStatus VehicleStatus, int CurrentOdometerKm,
    DateTime? OdometerUpdatedAt, decimal? TankCapacity, Guid? CurrentDriverId, string? CurrentDriverName,
    Guid? LastFuelTypeId, Guid? LastFuelStationId, DateTime? LastFueledAt, int? LastFuelingOdometerKm, bool RequireDriver);

public sealed record FuelPriceHintResponse(decimal Price, DateOnly Date, FuelPriceHintSource Source);

public enum FuelPriceHintSource
{
    /// <summary>The station's reference price in force.</summary>
    Reference,
    /// <summary>The last price paid at the station for the product.</summary>
    LastFueling,
}

public sealed class FuelingFieldsValidator : AbstractValidator<IFuelingFields>
{
    public FuelingFieldsValidator(IClock clock)
    {
        RuleFor(x => x.FuelTypeId).NotNull().WithMessage("Combustível: campo obrigatório.");
        RuleFor(x => x.FueledAt)
            .Must(d => d is null || d.Value <= clock.UtcNow.Add(MileageService.ClockTolerance))
            .WithMessage("A data do abastecimento não pode ser futura.");
        RuleFor(x => x.OdometerKm)
            .NotNull().WithMessage("Hodômetro: informe a quilometragem no momento do abastecimento.")
            .InclusiveBetween(0, OdometerPolicy.MaxOdometerKm)
            .WithMessage($"Hodômetro deve estar entre 0 e {BrazilianFormat.Number(OdometerPolicy.MaxOdometerKm)} km.");
        RuleFor(x => x.Quantity)
            .NotNull().WithMessage("Quantidade: campo obrigatório.")
            .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.")
            .LessThanOrEqualTo(FuelingAmounts.MaxQuantity).WithMessage("Quantidade muito alta. Confira o valor informado.")
            .Must(q => q is null || decimal.Round(q.Value, 3) == q.Value).WithMessage("Use no máximo 3 casas decimais na quantidade.");
        RuleFor(x => x.UnitPrice)
            .NotNull().WithMessage("Preço por unidade: campo obrigatório.")
            .GreaterThan(0).WithMessage("O preço deve ser maior que zero.")
            .LessThanOrEqualTo(FuelingAmounts.MaxUnitPrice).WithMessage("Preço muito alto. Confira o valor por litro (ex.: 6,199).")
            .Must(p => p is null || decimal.Round(p.Value, 4) == p.Value).WithMessage("Use no máximo 4 casas decimais no preço.");
        RuleFor(x => x.TotalAmount)
            .Must((x, total) => total is null || x.Quantity is not > 0 || x.UnitPrice is not > 0 ||
                                FuelingAmounts.TotalMatches(total.Value, x.Quantity.Value, x.UnitPrice.Value))
            .WithMessage(x => "O total não confere com quantidade × preço" +
                              (x.Quantity is > 0 && x.UnitPrice is > 0
                                  ? $" (R$ {BrazilianFormat.Number(FuelingAmounts.Total(x.Quantity.Value, x.UnitPrice.Value), 2)})."
                                  : ".") + " Confira os valores do cupom.");
        RuleFor(x => x.PaymentMethod).NotNull().WithMessage("Forma de pagamento: campo obrigatório.")
            .IsInEnum().WithMessage("Forma de pagamento inválida.");
        RuleFor(x => x.ReceiptNumber).MaxLen(Fueling.ReceiptMaxLength);
        RuleFor(x => x.Notes).MaxLen(Fueling.NotesMaxLength);
    }
}

public sealed class FuelingRequestValidator : AbstractValidator<FuelingRequest>
{
    public FuelingRequestValidator(IClock clock)
    {
        RuleFor(x => x.VehicleId).NotNull().WithMessage("Veículo: campo obrigatório.");
        Include(new FuelingFieldsValidator(clock));
    }
}

public sealed class FuelingCorrectionRequestValidator : AbstractValidator<FuelingCorrectionRequest>
{
    public FuelingCorrectionRequestValidator(IClock clock)
    {
        Include(new FuelingFieldsValidator(clock));
        RuleFor(x => x.Reason).Required("Motivo da correção").MaxLen(Fueling.ReasonMaxLength);
    }
}

public sealed class FuelingListRequestValidator : AbstractValidator<FuelingListRequest>
{
    public FuelingListRequestValidator()
    {
        this.ValidPeriod(x => x.From, x => x.To, "to");
        RuleFor(x => x.MaxQuantity).Must((x, max) => max is null || x.MinQuantity is null || x.MinQuantity <= max)
            .WithMessage("A quantidade máxima deve ser maior ou igual à mínima.");
        RuleFor(x => x.MaxUnitPrice).Must((x, max) => max is null || x.MinUnitPrice is null || x.MinUnitPrice <= max)
            .WithMessage("O preço máximo deve ser maior ou igual ao mínimo.");
    }
}

/// <summary>
/// Fuelings (ADR-032): the central record of fuel management. Every odometer goes through MileageService (single source
/// of truth); totals are calculated here; anomalies and consumption come from FuelConsumptionService. Nothing is deleted:
/// a wrong record is corrected (with reason and history) or cancelled.
/// </summary>
public sealed class FuelingService(
    IFleetDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    MileageService mileage,
    FileService files,
    FuelConsumptionService consumption,
    FuelSettingsService settingsService,
    OperationalEventLog events,
    IValidator<FuelingRequest> validator,
    IValidator<FuelingCorrectionRequest> correctionValidator,
    IValidator<FuelingListRequest> listValidator)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly SortMap<Fueling> Sorts = new SortMap<Fueling>("fueledAt")
        .Add("fueledAt", f => f.FueledAt)
        .Add("odometerKm", f => f.OdometerKm)
        .Add("quantity", f => f.Quantity)
        .Add("status", f => f.Status)
        .Add("licensePlate", f => f.Vehicle.LicensePlate);

    private bool CanSeeAllCosts => currentUser.HasPermission(Permissions.Fuel.ViewCosts);

    public async Task<PagedResult<FuelingListItemResponse>> ListAsync(FuelingListRequest request, CancellationToken ct)
    {
        await listValidator.ValidateAndThrowAsync(request, ct);
        if ((request.MinUnitPrice is not null || request.MaxUnitPrice is not null) && !CanSeeAllCosts)
            throw new ForbiddenException("Filtrar por preço exige a permissão de visualizar custos de combustível.");

        var query = Filter(db.Fuelings.AsQueryable(), request);
        if (request.SortBy is null) request.SortDirection = SortDirection.Desc;
        var allCosts = CanSeeAllCosts;
        var me = currentUser.UserId;
        return await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, f => new FuelingListItemResponse(
            f.Id, f.VehicleId, f.Vehicle.LicensePlate, f.DriverId, f.Driver != null ? f.Driver.FullName : null,
            f.FuelStationId, f.FuelStation != null ? f.FuelStation.Name : null, f.FuelTypeId, f.FuelType.Name, f.FuelType.Unit,
            f.FueledAt, f.OdometerKm, f.Quantity,
            allCosts || f.CreatedBy == me ? f.UnitPrice : null,
            allCosts || f.CreatedBy == me ? f.TotalAmount : null,
            f.IsFullTank, f.PaymentMethod, f.Status, f.ConsumptionResult, f.Consumption, f.Anomalies.Count), ct);
    }

    /// <summary>Shared by the list and the fueling report: every filter is applied in SQL (seção 31/44).</summary>
    public static IQueryable<Fueling> Filter(IQueryable<Fueling> query, FuelingListRequest r)
    {
        if (r.VehicleId is { } vehicleId) query = query.Where(f => f.VehicleId == vehicleId);
        if (r.DriverId is { } driverId) query = query.Where(f => f.DriverId == driverId);
        if (r.FuelStationId is { } stationId) query = query.Where(f => f.FuelStationId == stationId);
        if (r.FuelTypeId is { } typeId) query = query.Where(f => f.FuelTypeId == typeId);
        if (r.From is { } from) query = query.Where(f => f.FueledOn >= from);
        if (r.To is { } to) query = query.Where(f => f.FueledOn <= to);
        if (r.PaymentMethod is { } payment) query = query.Where(f => f.PaymentMethod == payment);
        if (r.Status is { } status) query = query.Where(f => f.Status == status);
        if (r.MinQuantity is { } minQ) query = query.Where(f => f.Quantity >= minQ);
        if (r.MaxQuantity is { } maxQ) query = query.Where(f => f.Quantity <= maxQ);
        if (r.MinUnitPrice is { } minP) query = query.Where(f => f.UnitPrice >= minP);
        if (r.MaxUnitPrice is { } maxP) query = query.Where(f => f.UnitPrice <= maxP);
        if (r.HasAnomaly is { } hasAnomaly) query = query.Where(f => f.Anomalies.Any() == hasAnomaly);
        if (r.AnomalyType is { } anomalyType) query = query.Where(f => f.Anomalies.Any(a => a.Type == anomalyType));
        if (r.SearchTerm is { } term)
        {
            var plate = LicensePlate.Normalize(term);
            query = query.Where(f => f.Vehicle.LicensePlate.Contains(plate) || (f.ReceiptNumber != null && f.ReceiptNumber.Contains(term)) ||
                                     (f.Driver != null && f.Driver.FullName.Contains(term)) ||
                                     (f.FuelStation != null && f.FuelStation.Name.Contains(term)));
        }
        return query;
    }

    public async Task<FuelingResponse> GetAsync(Guid id, CancellationToken ct) => await ToResponseAsync(await LoadAsync(id, ct), ct);

    public async Task<FuelingFormDefaultsResponse> GetFormDefaultsAsync(Guid vehicleId, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
        var driver = await AssignmentService.Active(db).Where(a => a.VehicleId == vehicleId)
            .Select(a => new { a.DriverId, a.Driver.FullName }).FirstOrDefaultAsync(ct);
        var last = await db.Fuelings.Where(f => f.VehicleId == vehicleId && f.Status != FuelingStatus.Cancelled)
            .OrderByDescending(f => f.FueledAt)
            .Select(f => new { f.FuelTypeId, f.FuelStationId, f.FueledAt, f.OdometerKm, TypeActive = f.FuelType.IsActive,
                StationActive = f.FuelStation == null || f.FuelStation.IsActive })
            .FirstOrDefaultAsync(ct);
        var settings = await settingsService.CurrentAsync(ct);
        var tank = vehicle.FuelTankCapacity is null && vehicle.SecondaryFuelTankCapacity is null
            ? (decimal?)null : (vehicle.FuelTankCapacity ?? 0) + (vehicle.SecondaryFuelTankCapacity ?? 0);

        return new FuelingFormDefaultsResponse(
            vehicle.Id, vehicle.LicensePlate, vehicle.FuelType, vehicle.Status, vehicle.CurrentOdometerKm, vehicle.OdometerUpdatedAt, tank,
            driver?.DriverId, driver?.FullName,
            // An inactive type/station is not suggested: it would be refused on save.
            last is { TypeActive: true } ? last.FuelTypeId : null,
            last is { StationActive: true } ? last.FuelStationId : null,
            last?.FueledAt, last?.OdometerKm, settings.RequireDriver);
    }

    public async Task<FuelPriceHintResponse?> GetPriceHintAsync(Guid stationId, Guid fuelTypeId, CancellationToken ct)
    {
        var reference = await db.FuelPrices
            .Where(p => p.FuelStationId == stationId && p.FuelTypeId == fuelTypeId && p.EffectiveFrom <= clock.Today)
            .OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.CreatedAt)
            .Select(p => new FuelPriceHintResponse(p.Price, p.EffectiveFrom, FuelPriceHintSource.Reference))
            .FirstOrDefaultAsync(ct);
        return reference ?? await db.Fuelings
            .Where(f => f.FuelStationId == stationId && f.FuelTypeId == fuelTypeId && f.Status != FuelingStatus.Cancelled)
            .OrderByDescending(f => f.FueledAt)
            .Select(f => new FuelPriceHintResponse(f.UnitPrice, f.FueledOn, FuelPriceHintSource.LastFueling))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<FuelingResponse> CreateAsync(FuelingRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var settings = await settingsService.CurrentAsync(ct);
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == request.VehicleId, ct)
            ?? throw ValidationErrors.ForField("vehicleId", "Veículo não encontrado. Ele pode ter sido excluído.");
        // seção 37: every condition except Inactive may be fueled (a vehicle in maintenance or waiting for paperwork still
        // gets fuel for road tests or yard moves). Same line the odometer history draws (ADR-019).
        if (vehicle.Status == VehicleStatus.Inactive)
            throw new BusinessRuleException("Este veículo está inativo e não pode receber abastecimentos. Reative-o para registrar.");

        var fueling = new Fueling { VehicleId = vehicle.Id, Vehicle = vehicle };
        var type = await ApplyFieldsAsync(request, fueling, previous: null, settings, ct);
        db.Fuelings.Add(fueling);

        await db.InTransactionAsync(async () =>
        {
            var mileageAnomaly = await RecordMileageAsync(fueling, vehicle, ct);
            await files.AttachAsync(request.FileIds, FileOwnerType.Fueling, fueling.Id, "fileIds", ct);
            var added = await consumption.EvaluateRecordAsync(fueling, vehicle, type, mileageAnomaly, settings, ct);
            fueling.RefreshStatus();

            events.Record(OperationalEventType.FuelingRecorded, FuelConsumptionService.Subject(fueling, vehicle),
                $"Abastecimento de {Qty(fueling.Quantity, type.Unit)} de {type.Name} em {FuelConsumptionService.Plate(vehicle)} " +
                $"com {Km(fueling.OdometerKm)}" + (fueling.FuelStation is { } s ? $" ({s.Name})." : "."),
                new { fuelingId = fueling.Id, fueling.Quantity, fueling.OdometerKm, fueling.FuelTypeId, fueling.FuelStationId, fueling.IsFullTank });
            if (mileageAnomaly is not null)
                events.Record(OperationalEventType.FuelingMileageInconsistencyDetected, FuelConsumptionService.Subject(fueling, vehicle),
                    $"Hodômetro informado no abastecimento de {FuelConsumptionService.Plate(vehicle)} ({Km(fueling.OdometerKm)}) em revisão: {mileageAnomaly}",
                    new { fuelingId = fueling.Id, fueling.OdometerKm });
            RecordMarkedForReview(fueling, vehicle, added);

            await db.SaveChangesAsync(ct);
            await consumption.RecalculateAsync(vehicle, fueling.FueledAt, settings, ct);
            await db.SaveChangesAsync(ct);
        }, ct);

        return await GetAsync(fueling.Id, ct);
    }

    /// <summary>
    /// Audited correction (seção 14): reason required, each changed field recorded from → to, checks re-run, consumption
    /// recalculated. Odometer changes follow the mileage rules (ADR-019/032) — see <see cref="CorrectMileageAsync"/>.
    /// </summary>
    public async Task<FuelingResponse> CorrectAsync(Guid id, FuelingCorrectionRequest request, CancellationToken ct)
    {
        await correctionValidator.ValidateAndThrowAsync(request, ct);
        var fueling = await LoadAsync(id, ct);
        if (!FuelingWorkflow.CanCorrect(fueling.Status))
            throw new BusinessRuleException("Um abastecimento cancelado não pode ser corrigido. Registre um novo abastecimento.");

        var settings = await settingsService.CurrentAsync(ct);
        var before = Snapshot(fueling);
        var previousAt = fueling.FueledAt;
        var type = await ApplyFieldsAsync(request, fueling, before, settings, ct);
        var changes = Diff(before, Snapshot(fueling));
        if (changes.Count == 0)
            throw new BusinessRuleException("Nenhum dado foi alterado. Mude ao menos um campo para registrar a correção.");

        var reason = request.Reason!.Trim();
        await db.InTransactionAsync(async () =>
        {
            string? mileageAnomaly = null;
            if (before.OdometerKm != fueling.OdometerKm || before.FueledAt != fueling.FueledAt)
                mileageAnomaly = await CorrectMileageAsync(fueling, before, reason, ct);
            else
                mileageAnomaly = fueling.Anomalies.FirstOrDefault(a => a.Type == FuelAnomalyType.MileageJump)?.Message;

            var added = await consumption.EvaluateRecordAsync(fueling, fueling.Vehicle, type, StripPrefix(mileageAnomaly), settings, ct);
            fueling.RefreshStatus();
            fueling.Corrections.Add(new FuelingCorrection
            {
                FuelingId = fueling.Id, CorrectedAt = clock.UtcNow, CorrectedBy = currentUser.UserId, Reason = reason,
                Changes = JsonSerializer.Serialize(changes, Json),
            });
            events.Record(OperationalEventType.FuelingCorrected, FuelConsumptionService.Subject(fueling, fueling.Vehicle),
                $"Abastecimento de {FuelConsumptionService.Plate(fueling.Vehicle)} corrigido ({string.Join(", ", changes.Select(c => c.Label.ToLowerInvariant()))}). Motivo: {reason}",
                new { fuelingId = fueling.Id, fields = changes.Select(c => c.Field) });
            RecordMarkedForReview(fueling, fueling.Vehicle, added);

            await db.SaveChangesAsync(ct);
            // Both positions of the chain: where the fueling was and where it is now.
            await consumption.RecalculateAsync(fueling.Vehicle, previousAt < fueling.FueledAt ? previousAt : fueling.FueledAt, settings, ct);
            if (previousAt != fueling.FueledAt)
                await consumption.RecalculateAsync(fueling.Vehicle, previousAt < fueling.FueledAt ? fueling.FueledAt : previousAt, settings, ct);
            await db.SaveChangesAsync(ct);
        }, ct);

        return await GetAsync(id, ct);
    }

    public async Task<FuelingResponse> CancelAsync(Guid id, FuelingReasonRequest request, CancellationToken ct)
    {
        var reason = RequireReason(request, "Informe o motivo do cancelamento.");
        var fueling = await LoadAsync(id, ct);
        if (!FuelingWorkflow.CanCancel(fueling.Status))
            throw new BusinessRuleException("Este abastecimento já está cancelado.");
        var settings = await settingsService.CurrentAsync(ct);

        await db.InTransactionAsync(async () =>
        {
            fueling.Status = FuelingStatus.Cancelled;
            fueling.CancelledAt = clock.UtcNow;
            fueling.CancelledBy = currentUser.UserId;
            fueling.CancellationReason = reason;
            // A suspicious odometer that was never applied goes away with the record. A valid one stays: the history is
            // never rewritten — a wrong km is fixed with an odometer correction (the response says so).
            if (await LinkedReadingAsync(fueling.Id, ct) is { Status: OdometerReadingStatus.PendingReview } pending)
                mileage.RejectPending(pending, $"Abastecimento cancelado: {reason}");
            events.Record(OperationalEventType.FuelingCancelled, FuelConsumptionService.Subject(fueling, fueling.Vehicle),
                $"Abastecimento de {Qty(fueling.Quantity, fueling.FuelType.Unit)} em {FuelConsumptionService.Plate(fueling.Vehicle)} cancelado. Motivo: {reason}",
                new { fuelingId = fueling.Id });
            await db.SaveChangesAsync(ct);
            await consumption.RecalculateAsync(fueling.Vehicle, fueling.FueledAt, settings, ct);
            await db.SaveChangesAsync(ct);
        }, ct);
        return await GetAsync(id, ct);
    }

    /// <summary>
    /// "Reviewed — the record is right": marks every anomaly as reviewed (seção 54). A suspicious odometer is approved too,
    /// which needs mileage.manage, and the consumption that depended on it is recalculated. Never creates a work order.
    /// </summary>
    public async Task<FuelingResponse> ReviewAsync(Guid id, FuelingReasonRequest request, CancellationToken ct)
    {
        var notes = RequireReason(request, "Descreva o que foi verificado (ex.: \"conferido com o cupom\").");
        var fueling = await LoadAsync(id, ct);
        if (!FuelingWorkflow.CanReview(fueling.Status))
            throw new BusinessRuleException("Este abastecimento não tem alertas pendentes de revisão.");
        var settings = await settingsService.CurrentAsync(ct);

        var pending = await LinkedReadingAsync(fueling.Id, ct) is { Status: OdometerReadingStatus.PendingReview } r ? r : null;
        if (pending is not null && !currentUser.HasPermission(Permissions.Mileage.Manage))
            throw new ForbiddenException(
                "O hodômetro deste abastecimento está em revisão. Confirmá-lo exige a permissão de revisar leituras de hodômetro; peça a um gestor.");

        await db.InTransactionAsync(async () =>
        {
            if (pending is not null) await mileage.ApprovePendingAsync(pending, $"Confirmado na revisão do abastecimento: {notes}", ct);
            var now = clock.UtcNow;
            foreach (var anomaly in fueling.Anomalies.Where(a => a.ReviewedAt is null))
            {
                anomaly.ReviewedAt = now;
                anomaly.ReviewedBy = currentUser.UserId;
            }
            fueling.ReviewedAt = now;
            fueling.ReviewedBy = currentUser.UserId;
            fueling.ReviewNotes = notes;
            fueling.RefreshStatus();
            events.Record(OperationalEventType.FuelingReviewed, FuelConsumptionService.Subject(fueling, fueling.Vehicle),
                $"Abastecimento de {FuelConsumptionService.Plate(fueling.Vehicle)} revisado: {notes}",
                new { fuelingId = fueling.Id, mileageApproved = pending is not null });
            await db.SaveChangesAsync(ct);
            if (pending is not null)
            {
                await consumption.RecalculateAsync(fueling.Vehicle, fueling.FueledAt, settings, ct);
                await db.SaveChangesAsync(ct);
            }
        }, ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Adds receipts/photos after the fact — by the author or by whoever may correct fuelings.</summary>
    public async Task<FuelingResponse> AttachFilesAsync(Guid id, FuelingFilesRequest request, CancellationToken ct)
    {
        var fueling = await LoadAsync(id, ct);
        if (fueling.CreatedBy != currentUser.UserId && !currentUser.HasPermission(Permissions.Fuel.Correct))
            throw new ForbiddenException("Somente quem registrou o abastecimento ou quem pode corrigi-lo anexa arquivos.");
        await files.AttachAsync(request.FileIds, FileOwnerType.Fueling, fueling.Id, "fileIds", ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ---------- helpers ----------

    /// <summary>Validates references and copies the fields; the total is always calculated here (seção 8).</summary>
    private async Task<FuelType> ApplyFieldsAsync(IFuelingFields request, Fueling fueling, FuelingSnapshot? previous, FuelSettings settings, CancellationToken ct)
    {
        var type = await db.FuelTypes.SingleOrDefaultAsync(t => t.Id == request.FuelTypeId, ct)
            ?? throw ValidationErrors.ForField("fuelTypeId", "Combustível não encontrado. Ele pode ter sido excluído.");
        if (!type.IsActive && previous?.FuelTypeId != type.Id)
            throw ValidationErrors.ForField("fuelTypeId", $"O combustível {type.Name} está inativo. Escolha outro ou reative-o no cadastro.");

        if (request.FuelStationId is { } stationId)
        {
            var station = await db.FuelStations.SingleOrDefaultAsync(s => s.Id == stationId, ct)
                ?? throw ValidationErrors.ForField("fuelStationId", "Posto não encontrado. Ele pode ter sido excluído.");
            if (!station.IsActive && previous?.FuelStationId != stationId)
                throw ValidationErrors.ForField("fuelStationId", $"O posto {station.Name} está inativo. Escolha outro ou reative-o.");
            fueling.FuelStation = station;
        }
        else
        {
            fueling.FuelStation = null;
        }

        if (request.DriverId is { } driverId)
        {
            var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == driverId, ct)
                ?? throw ValidationErrors.ForField("driverId", "Motorista não encontrado. Ele pode ter sido excluído.");
            // An unchanged driver on a correction is history; only a newly named driver must be active.
            if (driver.Status != DriverStatus.Active && previous?.DriverId != driverId)
                throw ValidationErrors.ForField("driverId", driver.Status == DriverStatus.OnLeave
                    ? $"{driver.FullName} está afastado(a) e não pode ser informado(a) em um novo abastecimento."
                    : $"{driver.FullName} está desligado(a) e não pode ser informado(a) em um novo abastecimento.");
            fueling.Driver = driver;
        }
        else if (settings.RequireDriver)
        {
            throw ValidationErrors.ForField("driverId", "Motorista: campo obrigatório nesta empresa.");
        }
        else
        {
            fueling.Driver = null;
        }

        var now = clock.UtcNow;
        var requested = request.FueledAt?.ToUniversalTime() ?? previous?.FueledAt ?? now;
        // Whole seconds: the value round-trips through JSON/JS (millisecond precision) on every correction, and an
        // unchanged date must compare equal (the date of a fueling with a reading is locked).
        var fueledAt = WholeSeconds(requested > now ? now : requested);
        fueling.DriverId = request.DriverId;
        fueling.FuelStationId = request.FuelStationId;
        fueling.FuelTypeId = type.Id;
        fueling.FuelType = type;
        fueling.FueledAt = fueledAt;
        fueling.FueledOn = clock.ToBusinessDate(fueledAt);
        fueling.OdometerKm = request.OdometerKm!.Value;
        fueling.Quantity = request.Quantity!.Value;
        fueling.UnitPrice = request.UnitPrice!.Value;
        fueling.TotalAmount = FuelingAmounts.Total(fueling.Quantity, fueling.UnitPrice);
        fueling.IsFullTank = request.IsFullTank;
        fueling.PaymentMethod = request.PaymentMethod!.Value;
        fueling.ReceiptNumber = request.ReceiptNumber.TrimToNull();
        fueling.Notes = request.Notes.TrimToNull();
        return type;
    }

    /// <summary>
    /// Fueling → reading → vehicle history (seção 9). A fueling at or after the latest reading becomes a reading
    /// (validated, possibly PendingReview); an older one must fit between the readings around it and creates none.
    /// Returns the odometer anomaly when the reading went to review.
    /// </summary>
    private async Task<string?> RecordMileageAsync(Fueling fueling, Vehicle vehicle, CancellationToken ct)
    {
        var baseline = await mileage.BaselineAsync(vehicle, ct);
        var readAt = fueling.FueledAt;
        // The form has minute precision: a fueling typed right after another reading (e.g. the registration a moment ago)
        // can land a few seconds before it. Within the clock tolerance it is still "now", so the reading goes at the
        // baseline instant (the fueling keeps its own time) instead of being treated as entered late.
        if (readAt < baseline.ReadAt && baseline.ReadAt - readAt <= MileageService.ClockTolerance) readAt = baseline.ReadAt;
        if (readAt < baseline.ReadAt)
        {
            await mileage.EnsureFitsHistoryAsync(vehicle, fueling.OdometerKm, fueling.FueledAt, "odometerKm", ct);
            return null;
        }
        var reading = await mileage.AddReadingAsync(vehicle, fueling.OdometerKm, readAt, OdometerReadingSource.Fueling,
            notes: null, checklistExecutionId: null, field: "odometerKm", ct, fuelingId: fueling.Id);
        return reading.Status == OdometerReadingStatus.PendingReview ? reading.Anomaly : null;
    }

    /// <summary>
    /// Odometer/date of an existing fueling (ADR-032):
    /// - its reading is still under review → that reading is rejected and the corrected value goes through the rules again;
    /// - its reading was applied → only the date is locked, and the km becomes an audited odometer correction, allowed while
    ///   that reading is still the vehicle's latest and only with mileage.manage;
    /// - no reading (entered after later readings) → the new km must fit between the readings around it.
    /// </summary>
    private async Task<string?> CorrectMileageAsync(Fueling fueling, FuelingSnapshot before, string reason, CancellationToken ct)
    {
        var vehicle = fueling.Vehicle;
        var linked = await LinkedReadingAsync(fueling.Id, ct);
        switch (linked?.Status)
        {
            case OdometerReadingStatus.PendingReview:
                if (before.FueledAt != fueling.FueledAt)
                    throw ValidationErrors.ForField("fueledAt", DateLockedMessage);
                mileage.RejectPending(linked, $"Corrigido no abastecimento: {reason}");
                await db.SaveChangesAsync(ct);
                return await RecordMileageAsync(fueling, vehicle, ct);

            case OdometerReadingStatus.Valid:
                if (before.FueledAt != fueling.FueledAt)
                    throw ValidationErrors.ForField("fueledAt", DateLockedMessage);
                if (!currentUser.HasPermission(Permissions.Mileage.Manage))
                    throw new ForbiddenException(
                        "Este abastecimento já atualizou o hodômetro do veículo. Corrigir o km exige a permissão de corrigir hodômetro; peça a um gestor.");
                var baseline = await mileage.BaselineAsync(vehicle, ct);
                if (baseline.ReadAt != linked.ReadAt || baseline.OdometerKm != linked.OdometerKm)
                    throw new BusinessRuleException(
                        "Já existem leituras de hodômetro posteriores a este abastecimento. Corrija o hodômetro pela aba Quilometragem do veículo " +
                        "e, se preciso, cancele e registre o abastecimento novamente.");
                await mileage.AddReadingAsync(vehicle, fueling.OdometerKm, linked.ReadAt, OdometerReadingSource.Correction,
                    $"Correção do abastecimento: {reason}", checklistExecutionId: null, field: "odometerKm", ct, fuelingId: fueling.Id);
                return null;

            default:
                return await RecordMileageAsync(fueling, vehicle, ct);
        }
    }

    private const string DateLockedMessage =
        "A data de um abastecimento que gerou leitura de hodômetro não pode mudar, porque reordenaria o histórico de quilometragem. " +
        "Cancele este abastecimento e registre-o novamente com a data certa.";

    /// <summary>
    /// The odometer reading that currently represents this fueling (the original, or a later correction). A rejected one
    /// only wins when nothing else exists — readings made in the same instant must not be told apart by Id (CLAUDE.md).
    /// </summary>
    private Task<OdometerReading?> LinkedReadingAsync(Guid fuelingId, CancellationToken ct) =>
        LinkedReadings(fuelingId).Include(r => r.Vehicle).FirstOrDefaultAsync(ct);

    private IQueryable<OdometerReading> LinkedReadings(Guid fuelingId) =>
        db.OdometerReadings.Where(r => r.FuelingId == fuelingId)
            .OrderBy(r => r.Status == OdometerReadingStatus.Rejected ? 1 : 0).ThenByDescending(r => r.CreatedAt);

    private void RecordMarkedForReview(Fueling fueling, Vehicle vehicle, IReadOnlyList<FuelingAnomaly> added)
    {
        if (added.Count == 0) return;
        events.Record(OperationalEventType.FuelingMarkedForReview, FuelConsumptionService.Subject(fueling, vehicle),
            $"Abastecimento de {FuelConsumptionService.Plate(vehicle)} requer revisão: {added[0].Message}" +
            (added.Count > 1 ? $" (+{added.Count - 1} alerta(s))" : ""),
            new { fuelingId = fueling.Id, anomalies = added.Select(a => a.Type) });
    }

    /// <summary>The MileageJump message is stored as "Hodômetro em revisão: …"; re-evaluation expects the bare reason.</summary>
    private static string? StripPrefix(string? message)
    {
        const string prefix = "Hodômetro em revisão: ";
        return message is not null && message.StartsWith(prefix, StringComparison.Ordinal) ? message[prefix.Length..] : message;
    }

    private static string RequireReason(FuelingReasonRequest request, string message)
    {
        var reason = request.Reason.TrimToNull() ?? throw ValidationErrors.ForField("reason", message);
        if (reason.Length > Fueling.ReasonMaxLength)
            throw ValidationErrors.ForField("reason", $"Use no máximo {Fueling.ReasonMaxLength} caracteres.");
        return reason;
    }

    private async Task<Fueling> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Fuelings
            .Include(f => f.Vehicle).Include(f => f.Driver).Include(f => f.FuelStation).Include(f => f.FuelType)
            .Include(f => f.Anomalies).Include(f => f.Corrections)
            .SingleOrDefaultAsync(f => f.Id == id, ct)
        ?? throw new NotFoundException("Abastecimento não encontrado.");

    private sealed record FuelingSnapshot(
        Guid? DriverId, string? DriverName, Guid? FuelStationId, string? StationName, Guid FuelTypeId, string FuelTypeName, FuelUnit Unit,
        DateTime FueledAt, int OdometerKm, decimal Quantity, decimal UnitPrice, decimal TotalAmount, bool IsFullTank,
        PaymentMethod PaymentMethod, string? ReceiptNumber, string? Notes);

    private static FuelingSnapshot Snapshot(Fueling f) => new(
        f.DriverId, f.Driver?.FullName, f.FuelStationId, f.FuelStation?.Name, f.FuelTypeId, f.FuelType.Name, f.FuelType.Unit,
        f.FueledAt, f.OdometerKm, f.Quantity, f.UnitPrice, f.TotalAmount, f.IsFullTank, f.PaymentMethod, f.ReceiptNumber, f.Notes);

    /// <summary>Money fields are named so the response can hide them from who may not see costs.</summary>
    private static readonly HashSet<string> MoneyFields = ["unitPrice", "totalAmount"];

    private List<FuelingChange> Diff(FuelingSnapshot a, FuelingSnapshot b)
    {
        var changes = new List<FuelingChange>();
        void Add(string field, string label, bool changed, string? from, string? to)
        {
            if (changed) changes.Add(new FuelingChange(field, label, from, to));
        }
        Add("driverId", "Motorista", a.DriverId != b.DriverId, a.DriverName, b.DriverName);
        Add("fuelStationId", "Posto", a.FuelStationId != b.FuelStationId, a.StationName, b.StationName);
        Add("fuelTypeId", "Combustível", a.FuelTypeId != b.FuelTypeId, a.FuelTypeName, b.FuelTypeName);
        Add("fueledAt", "Data e hora", a.FueledAt != b.FueledAt, clock.FormatDateTime(a.FueledAt), clock.FormatDateTime(b.FueledAt));
        Add("odometerKm", "Hodômetro", a.OdometerKm != b.OdometerKm, Km(a.OdometerKm), Km(b.OdometerKm));
        Add("quantity", "Quantidade", a.Quantity != b.Quantity, Qty(a.Quantity, a.Unit), Qty(b.Quantity, b.Unit));
        Add("unitPrice", "Preço por unidade", a.UnitPrice != b.UnitPrice, Money(a.UnitPrice, 3), Money(b.UnitPrice, 3));
        Add("totalAmount", "Total", a.TotalAmount != b.TotalAmount, Money(a.TotalAmount, 2), Money(b.TotalAmount, 2));
        Add("isFullTank", "Tanque cheio", a.IsFullTank != b.IsFullTank, YesNo(a.IsFullTank), YesNo(b.IsFullTank));
        Add("paymentMethod", "Forma de pagamento", a.PaymentMethod != b.PaymentMethod, PaymentText(a.PaymentMethod), PaymentText(b.PaymentMethod));
        Add("receiptNumber", "Número do cupom", a.ReceiptNumber != b.ReceiptNumber, a.ReceiptNumber, b.ReceiptNumber);
        Add("notes", "Observações", a.Notes != b.Notes, a.Notes, b.Notes);
        return changes;
    }

    private async Task<FuelingResponse> ToResponseAsync(Fueling f, CancellationToken ct)
    {
        var canSeeCosts = CanSeeAllCosts || f.CreatedBy == currentUser.UserId;
        var corrections = f.Corrections.OrderByDescending(c => c.CorrectedAt).ToList();
        var names = await UserNames.LoadAsync(db,
            new[] { f.CreatedBy, f.ReviewedBy, f.CancelledBy }
                .Concat(corrections.Select(c => c.CorrectedBy))
                .Concat(f.Anomalies.Select(a => a.ReviewedBy)), ct);
        var readingStatus = await LinkedReadings(f.Id).Select(r => (OdometerReadingStatus?)r.Status).FirstOrDefaultAsync(ct);
        var attachments = await files.ListByOwnerAsync(FileOwnerType.Fueling, f.Id, ct);

        var anomalies = f.Anomalies.OrderBy(a => a.DetectedAt).Select(a =>
        {
            // Price anomalies compare money values; quantity/consumption values are not money.
            var hide = a.Type == FuelAnomalyType.AbnormalPrice && !canSeeCosts;
            return new FuelingAnomalyResponse(a.Id, a.Type, a.Message, hide ? null : a.ExpectedValue, hide ? null : a.ActualValue,
                a.DetectedAt, a.ReviewedAt, names.Get(a.ReviewedBy));
        }).ToList();

        var correctionResponses = corrections.Select(c => new FuelingCorrectionResponse(
            c.Id, c.CorrectedAt, names.Get(c.CorrectedBy), c.Reason,
            (JsonSerializer.Deserialize<List<FuelingChange>>(c.Changes, Json) ?? [])
            .Select(ch => canSeeCosts || !MoneyFields.Contains(ch.Field) ? ch : ch with { From = "—", To = "—" })
            .ToList())).ToList();

        var actions = new FuelingActions(
            FuelingWorkflow.CanCorrect(f.Status) && currentUser.HasPermission(Permissions.Fuel.Correct),
            FuelingWorkflow.CanCancel(f.Status) && currentUser.HasPermission(Permissions.Fuel.Cancel),
            FuelingWorkflow.CanReview(f.Status) && currentUser.HasPermission(Permissions.Fuel.ReviewAnomalies));

        return new FuelingResponse(
            f.Id, f.VehicleId, f.Vehicle.LicensePlate, f.DriverId, f.Driver?.FullName, f.FuelStationId, f.FuelStation?.Name,
            f.FuelTypeId, f.FuelType.Name, f.FuelType.Unit, f.FueledAt, f.OdometerKm, f.Quantity,
            canSeeCosts ? f.UnitPrice : null, canSeeCosts ? f.TotalAmount : null, f.IsFullTank, f.PaymentMethod, f.ReceiptNumber, f.Notes,
            f.Source, f.Status, canSeeCosts,
            f.ConsumptionResult, f.SegmentDistanceKm, f.SegmentQuantity, canSeeCosts ? f.SegmentCost : null, f.Consumption,
            f.ExpectedConsumption, f.BaselineSource, f.ConsumptionDeviationPercent, readingStatus,
            f.ReviewedAt, names.Get(f.ReviewedBy), f.ReviewNotes,
            f.CancelledAt, names.Get(f.CancelledBy), f.CancellationReason,
            names.Get(f.CreatedBy), f.CreatedAt,
            anomalies, correctionResponses, attachments, actions);
    }

    private static DateTime WholeSeconds(DateTime utc) =>
        new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private static string Km(int km) => $"{BrazilianFormat.Number(km)} km";
    private static string Qty(decimal value, FuelUnit unit) => $"{BrazilianFormat.Number(value, 2)} {FuelUnits.Symbol(unit)}";
    private static string Money(decimal value, int decimals) => $"R$ {BrazilianFormat.Number(value, decimals)}";
    private static string YesNo(bool value) => value ? "Sim" : "Não";

    private static string PaymentText(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Dinheiro",
        PaymentMethod.Pix => "Pix",
        PaymentMethod.DebitCard => "Cartão de débito",
        PaymentMethod.CreditCard => "Cartão de crédito",
        PaymentMethod.FuelCard => "Cartão combustível",
        PaymentMethod.Invoice => "Faturado",
        PaymentMethod.InternalTank => "Tanque próprio",
        _ => "Outro",
    };
}
