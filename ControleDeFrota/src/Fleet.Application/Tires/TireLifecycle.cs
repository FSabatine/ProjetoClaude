using Fleet.Application.Common;
using Fleet.Application.Mileage;
using Fleet.Application.Operations;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Implements;
using Fleet.Domain.Mileage;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tires;

/// <summary>A vehicle or an implement with its axle layout — the same tire concepts serve both (seção 10).</summary>
public sealed record TireAsset(Vehicle? Vehicle, Implement? Implement, TireLayout? Layout, IReadOnlyList<TirePosition> Positions)
{
    public Guid? VehicleId => Vehicle?.Id;
    public Guid? ImplementId => Implement?.Id;
    public string Label => LicensePlate.Format(Vehicle?.LicensePlate ?? Implement!.LicensePlate);
    public bool IsInactive => Vehicle?.Status == VehicleStatus.Inactive || Implement?.Status == ImplementStatus.Inactive;

    public TirePosition? Position(string? code) =>
        code is null ? null : Positions.SingleOrDefault(p => string.Equals(p.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public IQueryable<TireInstallation> OpenStints(IQueryable<TireInstallation> stints) => Vehicle is not null
        ? stints.Where(i => i.RemovedAt == null && i.VehicleId == Vehicle.Id)
        : stints.Where(i => i.RemovedAt == null && i.ImplementId == Implement!.Id);
}

/// <summary>
/// Shared plumbing of every tire operation (ADR-036): loading, chronology, mileage from the odometer history, the
/// transaction and the translation of concurrency failures. The rules themselves live in the Domain (TireWorkflow & co.).
/// </summary>
public sealed class TireLifecycle(
    IFleetDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    MileageService mileage,
    OperationalEventLog events,
    TireLayoutService layouts)
{
    public const string ConcurrencyMessage =
        "Este pneu (ou a posição) acabou de ser alterado por outra pessoa. Atualize a tela e confira antes de tentar de novo.";

    public DateTime Now => clock.UtcNow;
    public IClock Clock => clock;
    public bool CanSeeCosts => currentUser.HasPermission(Permissions.Tires.ViewCosts);
    public bool Can(string permission) => currentUser.HasPermission(permission);
    public Guid? UserId => currentUser.UserId;

    public async Task<Tire> LoadTireAsync(Guid id, CancellationToken ct) =>
        await db.Tires.Include(t => t.Model).SingleOrDefaultAsync(t => t.Id == id, ct)
        ?? throw new NotFoundException("Pneu não encontrado. Ele pode ter sido excluído.");

    /// <summary>For request fields (tireId in a body): a missing tire is a field error, not a 404 of the route.</summary>
    public async Task<Tire> LoadTireForFieldAsync(Guid? id, string field, CancellationToken ct) =>
        await db.Tires.Include(t => t.Model).SingleOrDefaultAsync(t => t.Id == id, ct)
        ?? throw ValidationErrors.ForField(field, "Pneu não encontrado. Selecione um pneu da lista.");

    public async Task<TireInstallation?> OpenStintAsync(Guid tireId, CancellationToken ct) =>
        await db.TireInstallations.Include(i => i.Vehicle).Include(i => i.Implement)
            .SingleOrDefaultAsync(i => i.TireId == tireId && i.RemovedAt == null, ct);

    /// <summary>Exactly one of vehicle/implement. Missing (or another company's) → 404, like every cross-tenant id.</summary>
    public async Task<TireAsset> LoadAssetAsync(Guid? vehicleId, Guid? implementId, CancellationToken ct)
    {
        if ((vehicleId is null) == (implementId is null))
            throw ValidationErrors.ForField("vehicleId", "Informe o veículo ou o implemento (apenas um dos dois).");
        await layouts.EnsureDefaultsAsync(ct);
        if (vehicleId is { } vid)
        {
            var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vid, ct)
                ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
            return await WithLayoutAsync(vehicle, null, vehicle.TireLayoutId, ct);
        }
        var implement = await db.Implements.SingleOrDefaultAsync(i => i.Id == implementId, ct)
            ?? throw new NotFoundException("Implemento não encontrado. Ele pode ter sido excluído.");
        return await WithLayoutAsync(null, implement, implement.TireLayoutId, ct);
    }

    public Task<TireAsset> AssetOfAsync(TireInstallation stint, CancellationToken ct) =>
        WithLayoutAsync(stint.Vehicle, stint.Implement, stint.Vehicle?.TireLayoutId ?? stint.Implement?.TireLayoutId, ct);

    private async Task<TireAsset> WithLayoutAsync(Vehicle? vehicle, Implement? implement, Guid? layoutId, CancellationToken ct)
    {
        var layout = layoutId is { } id ? await db.TireLayouts.Include(l => l.Axles).SingleOrDefaultAsync(l => l.Id == id, ct) : null;
        return new TireAsset(vehicle, implement, layout, layout is null ? [] : TirePositions.For(layout));
    }

    public static TirePosition RequirePosition(TireAsset asset, string? code, string field = "positionCode")
    {
        if (asset.Layout is null)
            throw new BusinessRuleException(
                $"{asset.Label} ainda não tem configuração de eixos. Defina a configuração na aba Pneus antes de instalar pneus.");
        return asset.Position(code)
            ?? throw ValidationErrors.ForField(field, $"A posição informada não existe na configuração de eixos de {asset.Label}.");
    }

    /// <summary>
    /// When the operation happened: never in the future (5-minute clock tolerance, like the odometer), never before the tire's
    /// latest movement — the lifecycle is append-only, a stint cannot start before the previous one ended.
    /// </summary>
    public DateTime ResolveTime(DateTime? requested, Tire tire, string field = "performedAt")
    {
        var now = clock.UtcNow;
        var at = requested ?? now;
        if (at > now.Add(MileageService.ClockTolerance)) throw ValidationErrors.ForField(field, "A data não pode ser futura.");
        if (at > now) at = now;
        at = new DateTime(at.Ticks - at.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        if (tire.LastMovementAt is { } last && at < last)
            throw ValidationErrors.ForField(field,
                $"A data não pode ser anterior à última movimentação do pneu {tire.Code} ({clock.FormatDateTime(last)}).");
        return at;
    }

    /// <summary>A position must have been free at that time: the stint that last left it ended before.</summary>
    public async Task EnsurePositionFreeSinceAsync(TireAsset asset, TirePosition position, DateTime at, CancellationToken ct, Guid? exceptTireId = null)
    {
        var stints = db.TireInstallations.Where(i => i.PositionCode == position.Code && i.TireId != exceptTireId);
        stints = asset.Vehicle is not null ? stints.Where(i => i.VehicleId == asset.VehicleId) : stints.Where(i => i.ImplementId == asset.ImplementId);
        var lastRemoval = await stints.Where(i => i.RemovedAt != null).MaxAsync(i => (DateTime?)i.RemovedAt, ct);
        if (lastRemoval is { } removedAt && at < removedAt)
            throw ValidationErrors.ForField("performedAt",
                $"A posição {position.Label} estava ocupada até {clock.FormatDateTime(removedAt)}. Informe uma data posterior.");
    }

    /// <summary>
    /// Vehicle km at the operation (seção 38): the mileage history is the only source. A current operation with a newer km
    /// records a reading through MileageService (Source = TireService); a suspicious jump is refused here instead of being
    /// left for review — a tire stint cannot start on a km nobody confirmed. An operation typed after the fact takes the
    /// km of the history at that time, or an informed km that fits between the readings around it. Implements: null.
    /// </summary>
    public async Task<int?> OdometerAsync(TireAsset asset, DateTime at, int? informedKm, string note, CancellationToken ct, string field = "odometerKm")
    {
        if (asset.Vehicle is not { } vehicle) return null;
        if (informedKm is < 0 or > OdometerPolicy.MaxOdometerKm)
            throw ValidationErrors.ForField(field, $"Hodômetro deve estar entre 0 e {BrazilianFormat.Number(OdometerPolicy.MaxOdometerKm)} km.");

        var baseline = await mileage.BaselineAsync(vehicle, ct);
        var isCurrent = at >= baseline.ReadAt.Subtract(MileageService.ClockTolerance);
        if (isCurrent)
        {
            if (informedKm is null || informedKm == baseline.OdometerKm) return baseline.OdometerKm;
            var readAt = at < baseline.ReadAt ? baseline.ReadAt : at;
            var evaluation = OdometerPolicy.Evaluate(informedKm.Value, readAt, baseline);
            if (evaluation.Check == OdometerCheck.SuspiciousJump)
                throw ValidationErrors.ForField(field,
                    $"O hodômetro informado ({Km(informedKm.Value)}) é um aumento suspeito desde a última leitura ({Km(baseline.OdometerKm)}). " +
                    "Registre a leitura na aba Quilometragem (ela fica em revisão) e faça a operação do pneu depois de confirmada.");
            await mileage.AddReadingAsync(vehicle, informedKm.Value, readAt, OdometerReadingSource.TireService, note, null, field, ct);
            return informedKm;
        }

        if (informedKm is null) return await mileage.OdometerAtAsync(vehicle, at, ct);
        await mileage.EnsureFitsHistoryAsync(vehicle, informedKm.Value, at, field, ct);
        return informedKm;
    }

    /// <summary>Every operation touches the tire: bumps the concurrency token and the movement clock.</summary>
    public static void Touch(Tire tire, DateTime at)
    {
        tire.Version++;
        if (tire.LastMovementAt is null || at > tire.LastMovementAt) tire.LastMovementAt = at;
    }

    /// <summary>
    /// The unit of work of a tire operation (seção 51): all or nothing. Concurrency failures (token or filtered unique index)
    /// become a 409 with an explanation instead of a generic error.
    /// </summary>
    public async Task RunAsync(Func<Task> work, CancellationToken ct)
    {
        try
        {
            await db.InTransactionAsync(work, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ConcurrencyMessage);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message is { } m &&
                                           (m.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                                            m.Contains("duplicate", StringComparison.OrdinalIgnoreCase)) &&
                                           (m.Contains("TireInstallations", StringComparison.OrdinalIgnoreCase) ||
                                            m.Contains("TireServiceOrders", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException(ConcurrencyMessage);
        }
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public void Record(OperationalEventType type, Tire tire, TireAsset? asset, DateTime at, string summary, object? data = null, bool tireOnly = false) =>
        events.RecordAt(type, new EventSubject(nameof(Tire), tire.Id, tireOnly ? null : asset?.VehicleId, null,
            tireOnly ? null : asset?.ImplementId, tire.Id), at, summary, data);

    public void RecordForAsset(OperationalEventType type, string subjectType, Guid subjectId, TireAsset asset, DateTime at, string summary, object? data = null) =>
        events.RecordAt(type, new EventSubject(subjectType, subjectId, asset.VehicleId, null, asset.ImplementId), at, summary, data);

    /// <summary>Opens a stint (no save). The caller has already checked status, position and compatibility.</summary>
    public TireInstallation Open(Tire tire, TireAsset asset, TirePosition position, DateTime at, int? km, TireInstallReason reason,
        string? notes, Guid? rotationId = null)
    {
        var stint = new TireInstallation
        {
            TireId = tire.Id,
            Tire = tire,
            VehicleId = asset.VehicleId,
            ImplementId = asset.ImplementId,
            PositionCode = position.Code,
            PositionLabel = position.Label,
            AxleNumber = position.AxleNumber,
            IsSpare = position.IsSpare,
            InstalledAt = at,
            InstalledOdometerKm = km,
            InstalledHourMeter = asset.Vehicle?.HourMeter,
            InstallReason = reason,
            RotationId = rotationId,
            Notes = notes,
        };
        db.TireInstallations.Add(stint);
        tire.Status = TireStatus.Installed;
        tire.StorageLocation = null;
        Touch(tire, at);
        return stint;
    }

    /// <summary>Closes a stint (no save): km snapshot, accumulated km of the tire, who removed it.</summary>
    public void Close(TireInstallation stint, Tire tire, DateTime at, int? km, TireRemovalReason reason, TireRemovalDestination destination,
        string? notes, Guid? rotationId = null)
    {
        stint.RemovedAt = at;
        stint.RemovedOdometerKm = km;
        stint.RemovedHourMeter = stint.Vehicle?.HourMeter;
        stint.RemovalReason = reason;
        stint.RemovalDestination = destination;
        stint.RemovedBy = currentUser.UserId;
        stint.RemovalRotationId = rotationId;
        stint.RemovalNotes = notes;
        stint.DistanceKm = TireMileage.StintDistance(stint.IsSpare, stint.InstalledOdometerKm, km);
        if (stint.DistanceKm is { } distance) tire.AccumulatedKm += distance;
        else tire.HasUnmeasuredDistance = true;
        Touch(tire, at);
    }

    /// <summary>Tire km at a given vehicle km of its open stint (accumulated + running).</summary>
    public static int? TireKmAt(Tire tire, TireInstallation? stint, int? assetKm) =>
        stint is null ? tire.HasUnmeasuredDistance ? null : tire.AccumulatedKm
        : stint.IsSpare ? tire.AccumulatedKm
        : TireMileage.StintDistance(false, stint.InstalledOdometerKm, assetKm) is { } running ? tire.AccumulatedKm + running
        : null;

    public static string Km(int km) => $"{BrazilianFormat.Number(km)} km";

    public static string Mm(decimal mm) => $"{BrazilianFormat.Compact(mm)} mm";

    public static string Describe(Tire tire) => $"{tire.Code} ({tire.Model.Brand} {tire.Model.Name} {tire.Model.Size})";
}
