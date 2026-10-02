using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Domain.Authorization;
using Fleet.Domain.Files;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tires;

public sealed record TireInstallRequest
{
    public Guid? VehicleId { get; init; }
    public Guid? ImplementId { get; init; }
    public string? PositionCode { get; init; }
    /// <summary>When it happened. Blank = now; may be in the past (typed later), never before the tire's last movement.</summary>
    public DateTime? PerformedAt { get; init; }
    /// <summary>Optional: the vehicle odometer read now. Blank = the odometer history (seção 38).</summary>
    public int? OdometerKm { get; init; }
    public string? Notes { get; init; }
}

public record TireRemovalRequest
{
    public DateTime? PerformedAt { get; init; }
    public int? OdometerKm { get; init; }
    public TireRemovalReason? Reason { get; init; }
    public TireRemovalDestination? Destination { get; init; }
    // Measurement at removal (optional) — becomes part of the tread history.
    public decimal? TreadDepthMm { get; init; }
    public decimal? Pressure { get; init; }
    public TirePressureUnit? PressureUnit { get; init; }
    public TireCondition? Condition { get; init; }
    public TireWearPattern? WearPattern { get; init; }
    public IReadOnlyList<TireDamageType>? Damages { get; init; }
    public string? Notes { get; init; }
    /// <summary>Destination stock/evaluation: where the tire is kept.</summary>
    public string? StorageLocation { get; init; }
    // Destination repair/retread: the provider, when already known.
    public Guid? WorkshopId { get; init; }
    public string? ProviderName { get; init; }
    public TireRepairType? RepairType { get; init; }
    // Destination disposal.
    public TireDisposalReason? DisposalReason { get; init; }
    public string? DisposalDestination { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

/// <summary>Replacement (seção 32): the removal of the tire in the position + the tire that goes in, in one transaction.</summary>
public sealed record TireReplaceRequest : TireRemovalRequest
{
    public Guid? ReplacementTireId { get; init; }
    public string? InstallNotes { get; init; }
}

/// <summary>Move an installed tire to another vehicle/implement (seção 2).</summary>
public sealed record TireTransferRequest
{
    public Guid? VehicleId { get; init; }
    public Guid? ImplementId { get; init; }
    public string? PositionCode { get; init; }
    public DateTime? PerformedAt { get; init; }
    /// <summary>Odometer of the vehicle the tire leaves (optional).</summary>
    public int? OriginOdometerKm { get; init; }
    /// <summary>Odometer of the vehicle the tire goes to (optional).</summary>
    public int? OdometerKm { get; init; }
    public string? Notes { get; init; }
}

public sealed record TireRotationMove
{
    public Guid? TireId { get; init; }
    public string? ToPositionCode { get; init; }
}

public sealed record TireRotationRequest
{
    public Guid? VehicleId { get; init; }
    public Guid? ImplementId { get; init; }
    public DateTime? PerformedAt { get; init; }
    public int? OdometerKm { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<TireRotationMove> Moves { get; init; } = [];
}

public sealed record TireStockRequest
{
    public DateTime? PerformedAt { get; init; }
    public string? StorageLocation { get; init; }
    public string? Notes { get; init; }
}

public sealed record TireDisposalRequest
{
    public DateTime? PerformedAt { get; init; }
    public TireDisposalReason? Reason { get; init; }
    public string? Destination { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public sealed record TireInstallationCorrectionRequest
{
    public int? InstalledOdometerKm { get; init; }
    public int? RemovedOdometerKm { get; init; }
    public string? Reason { get; init; }
}

public sealed record TireLayoutAssignmentRequest
{
    /// <summary>Null removes the configuration (only when no tire is installed).</summary>
    public Guid? LayoutId { get; init; }
}

public sealed record AssetTirePositionResponse(
    TirePositionResponse Position, AssetInstalledTireResponse? Tire);

public sealed record AssetInstalledTireResponse(
    Guid TireId, string Code, string Brand, string ModelName, string Size, TireStatus Status, Guid InstallationId, DateTime InstalledAt,
    int? InstalledOdometerKm, int? CurrentKm, int? KmInPosition, decimal? CurrentTreadDepthMm, DateTime? TreadMeasuredAt, decimal? OriginalTreadDepthMm,
    DateTime? LastInspectedAt, int RetreadCount, IReadOnlyList<TireAlert> Alerts, int OpenAnomalyCount, IReadOnlyList<string> CompatibilityWarnings);

public sealed record AssetTireMovementResponse(long Id, OperationalEventType Type, DateTime OccurredAt, string Summary, Guid? TireId);

public sealed record AssetTireActions(bool CanInstall, bool CanRemove, bool CanReplace, bool CanRotate, bool CanInspect, bool CanChangeLayout);

public sealed record AssetTiresResponse(
    Guid? VehicleId, Guid? ImplementId, string AssetLabel, bool IsInactive, int? CurrentOdometerKm, DateTime? OdometerUpdatedAt,
    Guid? LayoutId, string? LayoutName, IReadOnlyList<TireLayoutAxleResponse> Axles, IReadOnlyList<AssetTirePositionResponse> Positions,
    int InstalledCount, int EmptyRequiredCount, int AlertCount, IReadOnlyList<AssetTireMovementResponse> RecentMovements, AssetTireActions Actions);

public sealed class TireInstallRequestValidator : AbstractValidator<TireInstallRequest>
{
    public TireInstallRequestValidator()
    {
        RuleFor(x => x.PositionCode).Required("Posição").MaxLen(10);
        RuleFor(x => x.Notes).MaxLen(TireInstallation.NotesMaxLength);
    }
}

public sealed class TireRemovalRequestValidator : AbstractValidator<TireRemovalRequest>
{
    public TireRemovalRequestValidator()
    {
        RuleFor(x => x.Reason).NotNull().WithMessage("Motivo da remoção: campo obrigatório.").IsInEnum().WithMessage("Motivo inválido.");
        RuleFor(x => x.Destination).NotNull().WithMessage("Destino do pneu: campo obrigatório.").IsInEnum().WithMessage("Destino inválido.")
            .NotEqual(TireRemovalDestination.Installed).WithMessage("Para mudar o pneu de posição use o rodízio; para outro veículo, a transferência.");
        RuleFor(x => x.DisposalReason).NotNull().When(x => x.Destination == TireRemovalDestination.Disposal)
            .WithMessage("Motivo da baixa: campo obrigatório.");
        RuleFor(x => x.RepairType).IsInEnum().WithMessage("Tipo de conserto inválido.");
        TireMeasurementRules.Apply(this, x => x.TreadDepthMm, x => x.Pressure, x => x.Damages!);
        RuleFor(x => x.Notes).MaxLen(TireInstallation.NotesMaxLength);
        RuleFor(x => x.StorageLocation).MaxLen(Tire.StorageMaxLength);
        RuleFor(x => x.ProviderName).MaxLen(TireServiceOrder.ProviderMaxLength);
        RuleFor(x => x.DisposalDestination).MaxLen(Tire.DisposalTextMaxLength);
    }
}

public sealed class TireReplaceRequestValidator : AbstractValidator<TireReplaceRequest>
{
    public TireReplaceRequestValidator()
    {
        Include(new TireRemovalRequestValidator());
        RuleFor(x => x.ReplacementTireId).NotNull().WithMessage("Pneu substituto: selecione o pneu que será instalado.");
        RuleFor(x => x.InstallNotes).MaxLen(TireInstallation.NotesMaxLength);
    }
}

public sealed class TireTransferRequestValidator : AbstractValidator<TireTransferRequest>
{
    public TireTransferRequestValidator()
    {
        RuleFor(x => x.PositionCode).Required("Posição").MaxLen(10);
        RuleFor(x => x.Notes).MaxLen(TireInstallation.NotesMaxLength);
    }
}

public sealed class TireRotationRequestValidator : AbstractValidator<TireRotationRequest>
{
    public TireRotationRequestValidator()
    {
        RuleFor(x => x.Moves).Must(m => m.Count >= 1).WithMessage("Informe ao menos um pneu e a nova posição.");
        RuleFor(x => x.Moves).Must(m => m.Where(x => x.TireId is not null).Select(x => x.TireId).Distinct().Count() == m.Count(x => x.TireId is not null))
            .WithMessage("Cada pneu aparece uma única vez no rodízio.");
        RuleFor(x => x.Moves).Must(m => m.Where(x => x.ToPositionCode is not null).Select(x => x.ToPositionCode!.Trim().ToUpperInvariant()).Distinct().Count() ==
                                        m.Count(x => x.ToPositionCode is not null))
            .WithMessage("Dois pneus não podem ir para a mesma posição.");
        RuleForEach(x => x.Moves).ChildRules(move =>
        {
            move.RuleFor(m => m.TireId).NotNull().WithMessage("Pneu: campo obrigatório.");
            move.RuleFor(m => m.ToPositionCode).Required("Nova posição").MaxLen(10);
        });
        RuleFor(x => x.Reason).MaxLen(TireRotation.ReasonMaxLength);
        RuleFor(x => x.Notes).MaxLen(TireInstallation.NotesMaxLength);
    }
}

public sealed class TireStockRequestValidator : AbstractValidator<TireStockRequest>
{
    public TireStockRequestValidator()
    {
        RuleFor(x => x.StorageLocation).MaxLen(Tire.StorageMaxLength);
        RuleFor(x => x.Notes).MaxLen(TireInstallation.NotesMaxLength);
    }
}

public sealed class TireDisposalRequestValidator : AbstractValidator<TireDisposalRequest>
{
    public TireDisposalRequestValidator()
    {
        RuleFor(x => x.Reason).NotNull().WithMessage("Motivo da baixa: campo obrigatório.").IsInEnum().WithMessage("Motivo inválido.");
        RuleFor(x => x.Destination).MaxLen(Tire.DisposalTextMaxLength);
        RuleFor(x => x.Notes).MaxLen(Tire.DisposalTextMaxLength);
    }
}

public sealed class TireInstallationCorrectionRequestValidator : AbstractValidator<TireInstallationCorrectionRequest>
{
    public TireInstallationCorrectionRequestValidator()
    {
        RuleFor(x => x.Reason).Required("Motivo da correção").MaxLen(TireInstallation.NotesMaxLength);
        RuleFor(x => x.InstalledOdometerKm).InclusiveBetween(0, Fleet.Domain.Mileage.OdometerPolicy.MaxOdometerKm).WithMessage("Hodômetro inválido.");
        RuleFor(x => x.RemovedOdometerKm).InclusiveBetween(0, Fleet.Domain.Mileage.OdometerPolicy.MaxOdometerKm).WithMessage("Hodômetro inválido.")
            .GreaterThanOrEqualTo(x => x.InstalledOdometerKm).When(x => x.InstalledOdometerKm is not null)
            .WithMessage("O hodômetro da remoção não pode ser menor que o da instalação.");
    }
}

/// <summary>Validation of a measurement, shared by the removal and the inspection.</summary>
public static class TireMeasurementRules
{
    public const decimal MaxTreadMm = 40m;
    public const decimal MaxPressure = 1_800m; // kPa upper bound; psi/bar are far below

    public static void Apply<T>(AbstractValidator<T> v, System.Linq.Expressions.Expression<Func<T, decimal?>> tread,
        System.Linq.Expressions.Expression<Func<T, decimal?>> pressure, System.Linq.Expressions.Expression<Func<T, IEnumerable<TireDamageType>>> damages)
    {
        v.RuleFor(tread).InclusiveBetween(0m, MaxTreadMm).WithMessage("Sulco deve estar entre 0 e 40 mm.")
            .Must(d => d is null || decimal.Round(d.Value, 1) == d.Value).WithMessage("Use no máximo uma casa decimal no sulco (ex.: 8,5).");
        v.RuleFor(pressure).GreaterThan(0m).WithMessage("A pressão deve ser maior que zero.")
            .LessThanOrEqualTo(MaxPressure).WithMessage("Pressão muito alta. Confira o valor e a unidade.");
        v.RuleForEach(damages).IsInEnum().WithMessage("Tipo de dano inválido.");
    }
}

/// <summary>
/// The transactional tire operations (seções 11–15, 32, 42, 51): each one changes several records — stints, tire status,
/// service orders, readings, events — and runs as a single unit of work. Concurrency is enforced by the database
/// (filtered unique indexes + the tire's concurrency token), not only by the checks below.
/// </summary>
public sealed class TireOperationsService(
    IFleetDbContext db,
    TireLifecycle lifecycle,
    TireMonitoring monitoring,
    TireService tires,
    TireSettingsService settingsService,
    FileService files,
    IValidator<TireInstallRequest> installValidator,
    IValidator<TireRemovalRequest> removalValidator,
    IValidator<TireReplaceRequest> replaceValidator,
    IValidator<TireTransferRequest> transferValidator,
    IValidator<TireRotationRequest> rotationValidator,
    IValidator<TireStockRequest> stockValidator,
    IValidator<TireDisposalRequest> disposalValidator,
    IValidator<TireInstallationCorrectionRequest> correctionValidator)
{
    // ---------- installation (seções 11, 12, 66) ----------

    public async Task<TireResponse> InstallAsync(Guid tireId, TireInstallRequest request, CancellationToken ct)
    {
        await installValidator.ValidateAndThrowAsync(request, ct);
        var tire = await lifecycle.LoadTireAsync(tireId, ct);
        var asset = await lifecycle.LoadAssetAsync(request.VehicleId, request.ImplementId, ct);
        var position = await ValidateInstallAsync(tire, asset, request.PositionCode, ct);
        var at = lifecycle.ResolveTime(request.PerformedAt, tire);
        await lifecycle.EnsurePositionFreeSinceAsync(asset, position, at, ct);

        await lifecycle.RunAsync(async () =>
        {
            var km = await lifecycle.OdometerAsync(asset, at, request.OdometerKm, $"Informado na instalação do pneu {tire.Code}.", ct);
            OpenFromStock(tire, asset, position, at, km, TireInstallReason.Installation, request.Notes.TrimToNull());
            await db.SaveChangesAsync(ct);
        }, ct);
        return await tires.GetAsync(tireId, ct);
    }

    // ---------- removal (seções 13, 69) ----------

    public async Task<TireResponse> RemoveAsync(Guid tireId, TireRemovalRequest request, CancellationToken ct)
    {
        await removalValidator.ValidateAndThrowAsync(request, ct);
        var (tire, stint, asset, at) = await PrepareRemovalAsync(tireId, request, ct);
        var settings = await settingsService.CurrentAsync(ct);

        await lifecycle.RunAsync(async () =>
        {
            await RemoveCoreAsync(tire, stint, asset, at, request, settings, ct);
            await db.SaveChangesAsync(ct);
        }, ct);
        return await tires.GetAsync(tireId, ct);
    }

    // ---------- replacement (seções 32, 70) ----------

    /// <summary>The installed tire is removed (with reason and destination) and the replacement takes its position — one transaction.</summary>
    public async Task<TireResponse> ReplaceAsync(Guid tireId, TireReplaceRequest request, CancellationToken ct)
    {
        await replaceValidator.ValidateAndThrowAsync(request, ct);
        if (!lifecycle.Can(Permissions.Tires.Remove))
            throw new ForbiddenException("Substituir um pneu inclui a remoção do atual, que exige a permissão de remover pneus.");
        var (tire, stint, asset, at) = await PrepareRemovalAsync(tireId, request, ct);
        if (request.ReplacementTireId == tireId)
            throw ValidationErrors.ForField("replacementTireId", "Escolha outro pneu: este é o pneu que está saindo.");
        var replacement = await lifecycle.LoadTireForFieldAsync(request.ReplacementTireId, "replacementTireId", ct);
        var position = TireLifecycle.RequirePosition(asset, stint.PositionCode);
        await ValidateInstallAsync(replacement, asset, position.Code, ct, ignoreOccupant: stint.TireId, field: "replacementTireId");
        lifecycle.ResolveTime(at, replacement, "performedAt");
        var settings = await settingsService.CurrentAsync(ct);

        await lifecycle.RunAsync(async () =>
        {
            var km = await RemoveCoreAsync(tire, stint, asset, at, request with { Reason = request.Reason ?? TireRemovalReason.Replacement }, settings, ct);
            // The position frees up before the replacement takes it: two saves inside the transaction (filtered unique index).
            await db.SaveChangesAsync(ct);
            OpenFromStock(replacement, asset, position, at, km, TireInstallReason.Replacement,
                request.InstallNotes.TrimToNull() ?? $"Substituiu o pneu {tire.Code}.");
            await db.SaveChangesAsync(ct);
        }, ct);
        return await tires.GetAsync(replacement.Id, ct);
    }

    // ---------- transfer between assets ----------

    public async Task<TireResponse> TransferAsync(Guid tireId, TireTransferRequest request, CancellationToken ct)
    {
        await transferValidator.ValidateAndThrowAsync(request, ct);
        if (!lifecycle.Can(Permissions.Tires.Remove))
            throw new ForbiddenException("Transferir um pneu inclui a remoção dele do veículo atual, que exige a permissão de remover pneus.");
        var tire = await lifecycle.LoadTireAsync(tireId, ct);
        if (!TireWorkflow.CanRemove(tire.Status))
            throw new BusinessRuleException($"O pneu {tire.Code} não está instalado. Para colocá-lo num veículo, use Instalar.");
        var stint = (await lifecycle.OpenStintAsync(tireId, ct))!;
        var origin = await lifecycle.AssetOfAsync(stint, ct);
        var target = await lifecycle.LoadAssetAsync(request.VehicleId, request.ImplementId, ct);
        if (target.VehicleId == origin.VehicleId && target.ImplementId == origin.ImplementId)
            throw ValidationErrors.ForField("vehicleId", "O pneu já está neste veículo/implemento. Para mudar de posição, use o rodízio.");
        var position = await ValidateInstallAsync(tire, target, request.PositionCode, ct, statusChecked: true);
        var at = lifecycle.ResolveTime(request.PerformedAt, tire);
        if (at < stint.InstalledAt) throw ValidationErrors.ForField("performedAt", "A data não pode ser anterior à instalação atual do pneu.");
        await lifecycle.EnsurePositionFreeSinceAsync(target, position, at, ct);

        await lifecycle.RunAsync(async () =>
        {
            var originKm = await lifecycle.OdometerAsync(origin, at, request.OriginOdometerKm, $"Informado na transferência do pneu {tire.Code}.", ct,
                "originOdometerKm");
            lifecycle.Close(stint, tire, at, originKm, TireRemovalReason.Transfer, TireRemovalDestination.Installed, request.Notes.TrimToNull());
            lifecycle.Record(OperationalEventType.TireRemoved, tire, origin, at,
                $"Pneu {tire.Code} retirado de {origin.Label} ({stint.PositionLabel}) para transferência a {target.Label}" + KmSuffix(originKm),
                new { tireId = tire.Id, stint.PositionCode, odometerKm = originKm, reason = TireRemovalReason.Transfer });
            await db.SaveChangesAsync(ct);
            var km = await lifecycle.OdometerAsync(target, at, request.OdometerKm, $"Informado na transferência do pneu {tire.Code}.", ct);
            var opened = lifecycle.Open(tire, target, position, at, km, TireInstallReason.Transfer, request.Notes.TrimToNull());
            RecordInstalled(tire, target, opened, km, $" (transferido de {origin.Label})");
            await db.SaveChangesAsync(ct);
        }, ct);
        return await tires.GetAsync(tireId, ct);
    }

    // ---------- rotation (seções 14, 15, 67) ----------

    /// <summary>
    /// Atomic rotation: validated as a whole against the final position map, then every stint involved is closed and reopened in
    /// one transaction. A target position may be taken only by a tire that is itself leaving it in the same rotation.
    /// </summary>
    public async Task<AssetTiresResponse> RotateAsync(TireRotationRequest request, CancellationToken ct)
    {
        await rotationValidator.ValidateAndThrowAsync(request, ct);
        var asset = await lifecycle.LoadAssetAsync(request.VehicleId, request.ImplementId, ct);
        if (asset.IsInactive) throw new BusinessRuleException($"{asset.Label} está inativo. Reative-o para fazer rodízio.");
        var open = await asset.OpenStints(db.TireInstallations.Include(i => i.Vehicle).Include(i => i.Implement)
            .Include(i => i.Tire).ThenInclude(t => t.Model)).ToListAsync(ct);
        var byTire = open.ToDictionary(i => i.TireId);

        var moves = new List<(TireInstallation Stint, TirePosition Target)>();
        for (var i = 0; i < request.Moves.Count; i++)
        {
            var move = request.Moves[i];
            if (!byTire.TryGetValue(move.TireId!.Value, out var stint))
                throw ValidationErrors.ForField($"moves[{i}].tireId", $"Este pneu não está instalado em {asset.Label}.");
            var target = TireLifecycle.RequirePosition(asset, move.ToPositionCode, $"moves[{i}].toPositionCode");
            if (target.Code == stint.PositionCode)
                throw ValidationErrors.ForField($"moves[{i}].toPositionCode", $"O pneu {stint.Tire.Code} já está em {target.Label}.");
            moves.Add((stint, target));
        }

        // Final map: tires that stay + tires that move. Every target must be free or vacated in this same rotation.
        var moving = moves.Select(m => m.Stint.TireId).ToHashSet();
        var finalMap = open.Where(i => !moving.Contains(i.TireId)).ToDictionary(i => i.PositionCode, i => i.Tire);
        foreach (var (stint, target) in moves)
        {
            if (finalMap.TryGetValue(target.Code, out var occupant))
                throw new ConflictException(
                    $"A posição {target.Label} está ocupada pelo pneu {occupant.Code}, que não faz parte do rodízio. Inclua-o no rodízio ou remova-o antes.");
            finalMap[target.Code] = stint.Tire;
        }
        var problems = new List<string>();
        foreach (var (stint, target) in moves)
        {
            var partner = TirePositions.DualPartner(target) is { } code && finalMap.TryGetValue(code, out var p) ? p.Model.Size : null;
            var check = TireCompatibility.Check(target, stint.Tire.Model.Size, stint.Tire.Model.Application, partner);
            if (check.Status == TireCompatibilityStatus.Incompatible) problems.AddRange(check.Messages.Select(m => $"{stint.Tire.Code}: {m}"));
        }
        if (problems.Count > 0) throw new BusinessRuleException(string.Join(" ", problems));

        var at = moves.Select(m => lifecycle.ResolveTime(request.PerformedAt, m.Stint.Tire)).Max();
        foreach (var (stint, _) in moves)
            if (at < stint.InstalledAt) throw ValidationErrors.ForField("performedAt", $"A data não pode ser anterior à instalação do pneu {stint.Tire.Code}.");

        await lifecycle.RunAsync(async () =>
        {
            var km = await lifecycle.OdometerAsync(asset, at, request.OdometerKm, "Informado no rodízio de pneus.", ct);
            var rotation = new TireRotation
            {
                VehicleId = asset.VehicleId, ImplementId = asset.ImplementId, PerformedAt = at, OdometerKm = km,
                Reason = request.Reason.TrimToNull(), Notes = request.Notes.TrimToNull(), TireCount = moves.Count,
            };
            db.TireRotations.Add(rotation);
            foreach (var (stint, _) in moves)
                lifecycle.Close(stint, stint.Tire, at, km, TireRemovalReason.Rotation, TireRemovalDestination.Installed, rotation.Notes, rotation.Id);
            // Positions are vacated before they are taken again (filtered unique index): two saves, one transaction.
            await db.SaveChangesAsync(ct);

            foreach (var (stint, target) in moves)
            {
                lifecycle.Open(stint.Tire, asset, target, at, km, TireInstallReason.Rotation, rotation.Notes, rotation.Id);
                lifecycle.Record(OperationalEventType.TireRotated, stint.Tire, asset, at,
                    $"Rodízio em {asset.Label}: pneu {stint.Tire.Code} de {stint.PositionLabel} para {target.Label}" + KmSuffix(km),
                    new { tireId = stint.Tire.Id, rotationId = rotation.Id, from = stint.PositionCode, to = target.Code, odometerKm = km }, tireOnly: true);
            }
            lifecycle.RecordForAsset(OperationalEventType.TireRotated, nameof(TireRotation), rotation.Id, asset, at,
                $"Rodízio de {moves.Count} pneu(s) em {asset.Label}: " +
                string.Join("; ", moves.Select(m => $"{m.Stint.Tire.Code} {m.Stint.PositionCode}→{m.Target.Code}")) +
                (rotation.Reason is { } reason ? $". Motivo: {reason}" : "."),
                new { rotationId = rotation.Id, moves = moves.Select(m => new { tireId = m.Stint.TireId, from = m.Stint.PositionCode, to = m.Target.Code }) });
            await db.SaveChangesAsync(ct);
        }, ct);
        return await GetAssetAsync(asset.VehicleId, asset.ImplementId, ct);
    }

    // ---------- stock, evaluation, disposal (seções 40–43) ----------

    public async Task<TireResponse> SendToEvaluationAsync(Guid tireId, TireStockRequest request, CancellationToken ct)
    {
        await stockValidator.ValidateAndThrowAsync(request, ct);
        var tire = await lifecycle.LoadTireAsync(tireId, ct);
        if (!TireWorkflow.CanSendToEvaluation(tire.Status))
            throw new BusinessRuleException($"Só um pneu em estoque é enviado para avaliação. O pneu {tire.Code} está {StatusText(tire.Status)}.");
        var at = lifecycle.ResolveTime(request.PerformedAt, tire);
        tire.Status = TireStatus.UnderInspection;
        if (request.StorageLocation.TrimToNull() is { } location) tire.StorageLocation = location;
        TireLifecycle.Touch(tire, at);
        lifecycle.Record(OperationalEventType.TireSentToEvaluation, tire, null, at,
            $"Pneu {tire.Code} separado para avaliação" + (request.Notes.TrimToNull() is { } n ? $": {n}" : "."), new { tireId = tire.Id });
        await lifecycle.RunAsync(() => db.SaveChangesAsync(ct), ct);
        return await tires.GetAsync(tireId, ct);
    }

    public async Task<TireResponse> ReturnToStockAsync(Guid tireId, TireStockRequest request, CancellationToken ct)
    {
        await stockValidator.ValidateAndThrowAsync(request, ct);
        var tire = await lifecycle.LoadTireAsync(tireId, ct);
        if (!TireWorkflow.CanReturnToStock(tire.Status))
            throw new BusinessRuleException($"Só um pneu em avaliação volta ao estoque por aqui. O pneu {tire.Code} está {StatusText(tire.Status)}.");
        var at = lifecycle.ResolveTime(request.PerformedAt, tire);
        tire.Status = TireStatus.InStock;
        tire.StorageLocation = request.StorageLocation.TrimToNull() ?? tire.StorageLocation;
        TireLifecycle.Touch(tire, at);
        lifecycle.Record(OperationalEventType.TireReturnedToStock, tire, null, at,
            $"Pneu {tire.Code} avaliado e liberado para uso" + (tire.StorageLocation is { } s ? $" ({s})" : "") +
            (request.Notes.TrimToNull() is { } n ? $": {n}" : "."), new { tireId = tire.Id });
        await lifecycle.RunAsync(() => db.SaveChangesAsync(ct), ct);
        return await tires.GetAsync(tireId, ct);
    }

    public async Task<TireResponse> DisposeAsync(Guid tireId, TireDisposalRequest request, CancellationToken ct)
    {
        await disposalValidator.ValidateAndThrowAsync(request, ct);
        var tire = await lifecycle.LoadTireAsync(tireId, ct);
        if (!TireWorkflow.CanDispose(tire.Status))
            throw new BusinessRuleException(tire.Status == TireStatus.Installed
                ? $"O pneu {tire.Code} está instalado. Remova-o com o destino \"Baixa\": um pneu baixado não pode ficar no veículo."
                : $"O pneu {tire.Code} está {StatusText(tire.Status)} e não pode ser baixado agora.");
        var at = lifecycle.ResolveTime(request.PerformedAt, tire);
        var settings = await settingsService.CurrentAsync(ct);
        await lifecycle.RunAsync(async () =>
        {
            await ApplyDisposalAsync(tire, at, request.Reason!.Value, request.Destination, request.Notes, request.FileIds, settings, ct);
            await db.SaveChangesAsync(ct);
        }, ct);
        return await tires.GetAsync(tireId, ct);
    }

    // ---------- controlled correction (seção 15) ----------

    /// <summary>
    /// The only edit of a completed stint: its odometer values, with a reason. The tire's accumulated km follows; the
    /// correction is an event in the tire timeline (from → to) and in the generic audit. Positions and dates are never edited.
    /// </summary>
    public async Task<TireInstallationResponse> CorrectInstallationAsync(Guid installationId, TireInstallationCorrectionRequest request, CancellationToken ct)
    {
        await correctionValidator.ValidateAndThrowAsync(request, ct);
        var stint = await db.TireInstallations.Include(i => i.Tire).ThenInclude(t => t.Model).Include(i => i.Vehicle).Include(i => i.Implement)
            .SingleOrDefaultAsync(i => i.Id == installationId, ct)
            ?? throw new NotFoundException("Instalação de pneu não encontrada.");
        if (stint.VehicleId is null)
            throw new BusinessRuleException("Instalações em implementos não têm hodômetro: não há km para corrigir.");
        if (stint.IsSpare) throw new BusinessRuleException("A posição de estepe não acumula km: não há km para corrigir.");
        if (stint.IsOpen && request.RemovedOdometerKm is not null)
            throw ValidationErrors.ForField("removedOdometerKm", "O pneu ainda está nesta posição: só o km da instalação pode ser corrigido.");

        var installed = request.InstalledOdometerKm ?? stint.InstalledOdometerKm;
        var removed = stint.IsOpen ? null : request.RemovedOdometerKm ?? stint.RemovedOdometerKm;
        if (installed == stint.InstalledOdometerKm && removed == stint.RemovedOdometerKm)
            throw new BusinessRuleException("Nenhum valor foi alterado. Informe o km correto da instalação ou da remoção.");
        if (installed is { } i && removed is { } r && r < i)
            throw ValidationErrors.ForField("removedOdometerKm", "O hodômetro da remoção não pode ser menor que o da instalação.");

        var tire = stint.Tire;
        var before = (stint.InstalledOdometerKm, stint.RemovedOdometerKm, stint.DistanceKm);
        stint.InstalledOdometerKm = installed;
        if (!stint.IsOpen)
        {
            stint.RemovedOdometerKm = removed;
            var distance = TireMileage.StintDistance(false, installed, removed);
            tire.AccumulatedKm = Math.Max(0, tire.AccumulatedKm - (before.DistanceKm ?? 0) + (distance ?? 0));
            stint.DistanceKm = distance;
        }
        tire.Version++;
        var asset = await lifecycle.AssetOfAsync(stint, ct);
        var reason = request.Reason!.Trim();
        lifecycle.Record(OperationalEventType.TireHistoryCorrected, tire, null, lifecycle.Now,
            $"Histórico do pneu {tire.Code} corrigido ({asset.Label}, {stint.PositionLabel}): km da instalação {Km(before.InstalledOdometerKm)} → {Km(installed)}" +
            (stint.IsOpen ? "" : $", km da remoção {Km(before.RemovedOdometerKm)} → {Km(removed)}") + $". Motivo: {reason}",
            new { installationId = stint.Id, previousInstalledKm = before.InstalledOdometerKm, previousRemovedKm = before.RemovedOdometerKm, installedOdometerKm = installed, removedOdometerKm = removed },
            tireOnly: true);
        await lifecycle.RunAsync(() => db.SaveChangesAsync(ct), ct);
        var names = await UserNames.LoadAsync(db, [stint.CreatedBy, stint.RemovedBy], ct);
        return TireService.ToResponse(stint, stint.Vehicle!.LicensePlate, names);
    }

    // ---------- the asset's tire view (seções 30, 31, 55) ----------

    public async Task<AssetTiresResponse> GetAssetAsync(Guid? vehicleId, Guid? implementId, CancellationToken ct)
    {
        var asset = await lifecycle.LoadAssetAsync(vehicleId, implementId, ct);
        var settings = await settingsService.CurrentAsync(ct);
        var open = await asset.OpenStints(db.TireInstallations.Include(i => i.Tire).ThenInclude(t => t.Model)).ToListAsync(ct);
        var tireIds = open.Select(i => i.TireId).ToList();
        var anomalies = await db.TireAnomalies.Where(a => tireIds.Contains(a.TireId) && a.ReviewedAt == null)
            .GroupBy(a => a.TireId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var currentKm = asset.Vehicle?.CurrentOdometerKm;
        var byCode = open.ToDictionary(i => i.PositionCode);
        var now = lifecycle.Now;

        var positions = asset.Positions.Select(p =>
        {
            if (!byCode.TryGetValue(p.Code, out var stint)) return new AssetTirePositionResponse(TireLayoutService.ToResponse(p), null);
            var t = stint.Tire;
            var partner = TirePositions.DualPartner(p) is { } code && byCode.TryGetValue(code, out var other) ? other.Tire.Model.Size : null;
            var compatibility = TireCompatibility.Check(p, t.Model.Size, t.Model.Application, partner);
            return new AssetTirePositionResponse(TireLayoutService.ToResponse(p), new AssetInstalledTireResponse(
                t.Id, t.Code, t.Model.Brand, t.Model.Name, t.Model.Size, t.Status, stint.Id, stint.InstalledAt, stint.InstalledOdometerKm,
                TireMileage.Current(t.AccumulatedKm, true, stint.IsSpare, stint.InstalledOdometerKm, currentKm),
                TireMileage.StintDistance(stint.IsSpare, stint.InstalledOdometerKm, currentKm),
                t.CurrentTreadDepthMm, t.TreadMeasuredAt, t.OriginalTreadDepthMm, t.LastInspectedAt, t.RetreadCount,
                TireAlertPolicy.Evaluate(t, settings, now, lifecycle.Clock.Today), anomalies.GetValueOrDefault(t.Id),
                compatibility.Status is TireCompatibilityStatus.Warning or TireCompatibilityStatus.Incompatible ? compatibility.Messages : []));
        }).ToList();

        var tireEvents = TireEventTypes;
        var events = db.OperationalEvents.Where(e => tireEvents.Contains(e.Type));
        events = asset.VehicleId is { } vid ? events.Where(e => e.VehicleId == vid) : events.Where(e => e.ImplementId == asset.ImplementId);
        var recent = await events.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Take(10)
            .Select(e => new AssetTireMovementResponse(e.Id, e.Type, e.OccurredAt, e.Summary, e.TireId)).ToListAsync(ct);

        bool Can(string p) => lifecycle.Can(p);
        var hasLayout = asset.Layout is not null;
        return new AssetTiresResponse(asset.VehicleId, asset.ImplementId, asset.Label, asset.IsInactive, currentKm, asset.Vehicle?.OdometerUpdatedAt,
            asset.Layout?.Id, asset.Layout?.Name,
            asset.Layout?.Axles.OrderBy(a => a.Number).Select(a => new TireLayoutAxleResponse(a.Number, a.Type, a.IsDual, a.IsRequired, a.AllowedSize,
                a.RecommendedPressurePsi)).ToList() ?? [],
            positions, open.Count, positions.Count(p => p.Tire is null && p.Position.IsRequired),
            positions.Sum(p => p.Tire?.Alerts.Count ?? 0), recent,
            new AssetTireActions(
                CanInstall: Can(Permissions.Tires.Install) && hasLayout && !asset.IsInactive,
                CanRemove: Can(Permissions.Tires.Remove),
                CanReplace: Can(Permissions.Tires.Remove) && Can(Permissions.Tires.Install) && !asset.IsInactive,
                CanRotate: Can(Permissions.Tires.Rotate) && open.Count > 0 && !asset.IsInactive,
                CanInspect: Can(Permissions.Tires.Inspect),
                CanChangeLayout: Can(Permissions.Tires.ManageSettings)));
    }

    /// <summary>
    /// The axle configuration of a vehicle/implement (seção 9). Changing it keeps every installed tire valid: each occupied
    /// position must exist in the new layout; removing the configuration requires no tire installed.
    /// </summary>
    public async Task<AssetTiresResponse> SetLayoutAsync(Guid? vehicleId, Guid? implementId, TireLayoutAssignmentRequest request, CancellationToken ct)
    {
        var asset = await lifecycle.LoadAssetAsync(vehicleId, implementId, ct);
        var occupied = await asset.OpenStints(db.TireInstallations).Select(i => i.PositionCode).ToListAsync(ct);
        TireLayout? layout = null;
        if (request.LayoutId is { } layoutId)
        {
            layout = await db.TireLayouts.Include(l => l.Axles).SingleOrDefaultAsync(l => l.Id == layoutId, ct)
                ?? throw ValidationErrors.ForField("layoutId", "Configuração de eixos não encontrada. Selecione uma da lista.");
            var expected = asset.Vehicle is not null ? TireLayoutTarget.Vehicle : TireLayoutTarget.Implement;
            if (layout.Target != expected)
                throw ValidationErrors.ForField("layoutId", expected == TireLayoutTarget.Vehicle
                    ? "Esta configuração é de implemento. Escolha uma configuração de veículo."
                    : "Esta configuração é de veículo. Escolha uma configuração de implemento.");
            if (!layout.IsActive && layout.Id != asset.Layout?.Id)
                throw ValidationErrors.ForField("layoutId", "Esta configuração está inativa. Escolha outra ou reative-a.");
            var codes = TirePositions.For(layout).Select(p => p.Code).ToHashSet();
            var missing = occupied.Where(c => !codes.Contains(c)).ToList();
            if (missing.Count > 0)
                throw new BusinessRuleException(
                    $"Há pneus instalados nas posições {string.Join(", ", missing)}, que não existem na nova configuração. Remova-os antes de trocar.");
        }
        else if (occupied.Count > 0)
            throw new BusinessRuleException("Há pneus instalados. Remova-os antes de retirar a configuração de eixos.");

        if (asset.Vehicle is { } vehicle) vehicle.TireLayoutId = layout?.Id;
        else asset.Implement!.TireLayoutId = layout?.Id;
        lifecycle.RecordForAsset(OperationalEventType.TireLayoutChanged, asset.Vehicle is not null ? "Vehicle" : "Implement",
            asset.VehicleId ?? asset.ImplementId!.Value, asset, lifecycle.Now,
            layout is null ? $"Configuração de eixos removida de {asset.Label}." : $"Configuração de eixos de {asset.Label} definida como \"{layout.Name}\".",
            new { layoutId = layout?.Id });
        await db.SaveChangesAsync(ct);
        return await GetAssetAsync(vehicleId, implementId, ct);
    }

    public static readonly OperationalEventType[] TireEventTypes =
    [
        OperationalEventType.TireInstalled, OperationalEventType.TireRemoved, OperationalEventType.TireRotated,
        OperationalEventType.TireInspected, OperationalEventType.TireInspectionFailed, OperationalEventType.TireRepairCompleted,
        OperationalEventType.TireLayoutChanged,
    ];

    // ---------- helpers ----------

    private async Task<TirePosition> ValidateInstallAsync(Tire tire, TireAsset asset, string? positionCode, CancellationToken ct,
        Guid? ignoreOccupant = null, string field = "tireId", bool statusChecked = false)
    {
        if (!statusChecked && !TireWorkflow.CanInstall(tire.Status))
            throw new BusinessRuleException(tire.Status == TireStatus.Installed
                ? $"O pneu {tire.Code} já está instalado. Para levá-lo a outro veículo use Transferir; para mudar de posição, o rodízio."
                : $"O pneu {tire.Code} está {StatusText(tire.Status)} e não pode ser instalado. Só pneus em estoque são instalados.");
        if (asset.IsInactive) throw new BusinessRuleException($"{asset.Label} está inativo e não recebe pneus. Reative-o para instalar.");
        var position = TireLifecycle.RequirePosition(asset, positionCode);
        var occupant = await asset.OpenStints(db.TireInstallations).Where(i => i.PositionCode == position.Code && i.TireId != ignoreOccupant)
            .Select(i => i.Tire.Code).FirstOrDefaultAsync(ct);
        if (occupant is not null)
            throw new ConflictException(
                $"A posição {position.Label} já tem o pneu {occupant}. Use Substituir para trocá-lo — a remoção do atual fica registrada.", "positionCode");
        var compatibility = await tires.CheckAsync(tire, asset, position, ct);
        if (compatibility.Status == TireCompatibilityStatus.Incompatible)
            throw ValidationErrors.ForField(field, compatibility.Messages[0]);
        return position;
    }

    private void OpenFromStock(Tire tire, TireAsset asset, TirePosition position, DateTime at, int? km, TireInstallReason reason, string? notes)
    {
        var stint = lifecycle.Open(tire, asset, position, at, km, reason, notes);
        // The inspection interval starts again when a tire goes into use (seção 34).
        if (tire.InspectionReferenceAt is null || at > tire.InspectionReferenceAt) tire.InspectionReferenceAt = at;
        RecordInstalled(tire, asset, stint, km, reason == TireInstallReason.Replacement ? " (substituição)" : "");
    }

    private void RecordInstalled(Tire tire, TireAsset asset, TireInstallation stint, int? km, string suffix) =>
        lifecycle.Record(OperationalEventType.TireInstalled, tire, asset, stint.InstalledAt,
            $"Pneu {TireLifecycle.Describe(tire)} instalado em {asset.Label}, {stint.PositionLabel}{suffix}" + KmSuffix(km),
            new { tireId = tire.Id, stint.PositionCode, odometerKm = km, reason = stint.InstallReason });

    private async Task<(Tire Tire, TireInstallation Stint, TireAsset Asset, DateTime At)> PrepareRemovalAsync(Guid tireId, TireRemovalRequest request, CancellationToken ct)
    {
        var tire = await lifecycle.LoadTireAsync(tireId, ct);
        if (!TireWorkflow.CanRemove(tire.Status))
            throw new BusinessRuleException($"O pneu {tire.Code} não está instalado ({StatusText(tire.Status)}), então não há o que remover.");
        var needed = request.Destination switch
        {
            TireRemovalDestination.Disposal => (Permissions.Tires.Dispose, "dar baixa em pneus"),
            TireRemovalDestination.Repair => (Permissions.Tires.Repair, "registrar consertos"),
            TireRemovalDestination.Retread => (Permissions.Tires.Retread, "registrar recapagens"),
            _ => ((string?)null, ""),
        };
        if (needed.Item1 is { } permission && !lifecycle.Can(permission))
            throw new ForbiddenException($"Este destino exige a permissão de {needed.Item2}.");
        if (request.WorkshopId is { } workshopId && !await db.Workshops.AnyAsync(w => w.Id == workshopId, ct))
            throw ValidationErrors.ForField("workshopId", "Fornecedor não encontrado. Selecione uma oficina da lista.");
        var stint = await lifecycle.OpenStintAsync(tireId, ct)
            ?? throw new BusinessRuleException($"O pneu {tire.Code} não tem instalação aberta. Atualize a tela.");
        var asset = await lifecycle.AssetOfAsync(stint, ct);
        var at = lifecycle.ResolveTime(request.PerformedAt, tire);
        if (at < stint.InstalledAt) throw ValidationErrors.ForField("performedAt", "A remoção não pode ser anterior à instalação.");
        return (tire, stint, asset, at);
    }

    /// <summary>Removal in the unit of work (no save). Returns the km used, so a replacement installs at the same km.</summary>
    private async Task<int?> RemoveCoreAsync(Tire tire, TireInstallation stint, TireAsset asset, DateTime at, TireRemovalRequest request,
        TireSettings settings, CancellationToken ct)
    {
        var km = await lifecycle.OdometerAsync(asset, at, request.OdometerKm, $"Informado na remoção do pneu {tire.Code}.", ct);
        var destination = request.Destination!.Value;
        var reason = request.Reason!.Value;
        lifecycle.Close(stint, tire, at, km, reason, destination, request.Notes.TrimToNull());

        var damages = request.Damages ?? [];
        if (request.TreadDepthMm is not null || request.Pressure is not null || request.Condition is not null || damages.Count > 0 ||
            request.WearPattern is not null)
            await monitoring.RecordAsync(tire, stint, asset, at, new TireMeasurementInput(TireInspectionSource.Removal, km, request.TreadDepthMm,
                request.Pressure, request.PressureUnit, request.Condition ?? TireCondition.Good, request.WearPattern ?? TireWearPattern.Unknown,
                damages, request.Notes.TrimToNull()), settings, ct);

        tire.Status = TireWorkflow.AfterRemoval(destination);
        if (destination is TireRemovalDestination.Stock or TireRemovalDestination.Evaluation)
            tire.StorageLocation = request.StorageLocation.TrimToNull();
        lifecycle.Record(OperationalEventType.TireRemoved, tire, asset, at,
            $"Pneu {tire.Code} removido de {asset.Label}, {stint.PositionLabel} ({ReasonText(reason)}; destino: {DestinationText(destination)})" + KmSuffix(km) +
            (stint.DistanceKm is { } d && !stint.IsSpare ? $" Rodou {Km(d)} nesta posição." : ""),
            new { tireId = tire.Id, stint.PositionCode, odometerKm = km, reason, destination, distanceKm = stint.DistanceKm });
        if (reason == TireRemovalReason.Damage) await monitoring.CheckRecurringPositionDamageAsync(tire, stint, asset, at, ct);

        switch (destination)
        {
            case TireRemovalDestination.Repair or TireRemovalDestination.Retread:
                var kind = destination == TireRemovalDestination.Retread ? TireServiceKind.Retread : TireServiceKind.Repair;
                var order = TireServiceOrderService.Open(db, tire, kind, at, request.WorkshopId, request.ProviderName.TrimToNull(),
                    request.RepairType, request.Notes.TrimToNull());
                var provider = request.WorkshopId is { } workshopId
                    ? await db.Workshops.Where(w => w.Id == workshopId).Select(w => w.Name).SingleAsync(ct) : order.ProviderName;
                lifecycle.Record(kind == TireServiceKind.Retread ? OperationalEventType.TireRetreadStarted : OperationalEventType.TireRepairStarted,
                    tire, null, at, TireServiceOrderService.StartedSummary(tire, order, provider), new { tireId = tire.Id, serviceOrderId = order.Id }, tireOnly: true);
                break;
            case TireRemovalDestination.Disposal:
                await ApplyDisposalAsync(tire, at, request.DisposalReason!.Value, request.DisposalDestination, request.Notes, request.FileIds, settings, ct);
                break;
        }
        return km;
    }

    private async Task ApplyDisposalAsync(Tire tire, DateTime at, TireDisposalReason reason, string? destination, string? notes,
        IReadOnlyList<Guid>? fileIds, TireSettings settings, CancellationToken ct)
    {
        tire.Status = TireStatus.Disposed;
        tire.DisposedAt = at;
        tire.DisposalReason = reason;
        tire.DisposalDestination = destination.TrimToNull();
        tire.DisposalNotes = notes.TrimToNull();
        tire.DisposedBy = lifecycle.UserId;
        tire.StorageLocation = null;
        TireLifecycle.Touch(tire, at);
        await files.AttachAsync(fileIds, FileOwnerType.Tire, tire.Id, "fileIds", ct);
        lifecycle.Record(OperationalEventType.TireEndOfLife, tire, null, at,
            $"Pneu {tire.Code} baixado ({DisposalText(reason)}) com {Km(tire.AccumulatedKm)} rodados" +
            (tire.DisposalDestination is { } d ? $". Destino: {d}." : "."),
            new { tireId = tire.Id, reason, tire.AccumulatedKm, tire.RetreadCount }, tireOnly: true);
        await monitoring.CheckShortLifecycleAsync(tire, settings, at, ct);
    }

    private static string KmSuffix(int? km) => km is { } value ? $", com {Km(value)}." : ".";

    private static string Km(int? km) => km is { } value ? TireLifecycle.Km(value) : "—";

    public static string StatusText(TireStatus s) => s switch
    {
        TireStatus.InStock => "em estoque",
        TireStatus.Installed => "instalado",
        TireStatus.UnderInspection => "em avaliação",
        TireStatus.UnderRepair => "em conserto",
        TireStatus.UnderRetread => "em recapagem",
        _ => "baixado",
    };

    private static string ReasonText(TireRemovalReason r) => r switch
    {
        TireRemovalReason.Rotation => "rodízio",
        TireRemovalReason.Replacement => "substituição",
        TireRemovalReason.Repair => "conserto",
        TireRemovalReason.Retread => "recapagem",
        TireRemovalReason.Inspection => "inspeção",
        TireRemovalReason.VehicleSale => "venda do veículo",
        TireRemovalReason.VehicleDecommission => "baixa do veículo",
        TireRemovalReason.Damage => "dano",
        TireRemovalReason.EndOfLife => "fim de vida",
        TireRemovalReason.Transfer => "transferência",
        _ => "outro motivo",
    };

    private static string DestinationText(TireRemovalDestination d) => d switch
    {
        TireRemovalDestination.Stock => "estoque",
        TireRemovalDestination.Evaluation => "avaliação",
        TireRemovalDestination.Repair => "conserto",
        TireRemovalDestination.Retread => "recapagem",
        TireRemovalDestination.Disposal => "baixa",
        _ => "outra posição",
    };

    public static string DisposalText(TireDisposalReason r) => r switch
    {
        TireDisposalReason.EndOfLife => "fim de vida",
        TireDisposalReason.IrreparableDamage => "dano irreparável",
        TireDisposalReason.SafetyConcern => "segurança",
        TireDisposalReason.Lost => "extravio",
        TireDisposalReason.Sold => "venda",
        TireDisposalReason.Transferred => "transferência para outra empresa",
        _ => "outro motivo",
    };
}
