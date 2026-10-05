using Fleet.Application.Assignments;
using Fleet.Application.Common;
using Fleet.Domain.Tracking;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Application.Tracking;

// ---------------- Providers

public sealed record TrackingProviderRequest
{
    public string? Name { get; init; }
    public TrackingProviderKind Kind { get; init; } = TrackingProviderKind.GenericHttp;
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record TrackingProviderResponse(Guid Id, string Name, TrackingProviderKind Kind, string? Notes, bool IsActive, int Devices);

public sealed class TrackingProviderRequestValidator : AbstractValidator<TrackingProviderRequest>
{
    public TrackingProviderRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(TrackingProvider.NameMaxLength);
        RuleFor(x => x.Notes).MaxLen(TrackingProvider.NotesMaxLength);
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Tipo inválido.");
    }
}

public sealed class TrackingProviderService(IFleetDbContext db, IValidator<TrackingProviderRequest> validator)
{
    public async Task<IReadOnlyList<TrackingProviderResponse>> ListAsync(CancellationToken ct) =>
        await db.TrackingProviders.OrderBy(p => p.Name)
            .Select(p => new TrackingProviderResponse(p.Id, p.Name, p.Kind, p.Notes, p.IsActive, db.TrackingDevices.Count(d => d.TrackingProviderId == p.Id)))
            .ToListAsync(ct);

    public async Task<TrackingProviderResponse> SaveAsync(Guid? id, TrackingProviderRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var provider = id is null ? new TrackingProvider() : await FindAsync(id.Value, ct);
        provider.Name = request.Name!.Trim();
        provider.Kind = request.Kind;
        provider.Notes = request.Notes.TrimToNull();
        provider.IsActive = request.IsActive;
        if (id is null) db.TrackingProviders.Add(provider);
        await db.SaveChangesAsync(ct);
        return (await ListAsync(ct)).Single(p => p.Id == provider.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var provider = await FindAsync(id, ct);
        if (await db.TrackingDevices.AnyAsync(d => d.TrackingProviderId == id, ct))
            throw new BusinessRuleException("O provedor tem rastreadores cadastrados. Exclua ou mova os rastreadores antes, ou apenas desative o provedor.");
        db.TrackingProviders.Remove(provider);
        await db.SaveChangesAsync(ct);
    }

    private async Task<TrackingProvider> FindAsync(Guid id, CancellationToken ct) =>
        await db.TrackingProviders.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Provedor de rastreamento não encontrado.");
}

// ---------------- Devices

public sealed record TrackingDeviceRequest
{
    public Guid? TrackingProviderId { get; init; }
    public string? Identifier { get; init; }
    public string? Model { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record TrackingDeviceResponse(
    Guid Id, Guid TrackingProviderId, string ProviderName, string Identifier, string? Model, bool IsActive, string ApiKeyPrefix,
    DateTime? KeyIssuedAt, Guid? VehicleId, string? LicensePlate, DateTime? LinkedSince, DateTime? LastPositionAt, TrackingConnectionStatus Status);

/// <summary>Returned only when a key is created or rotated — the plain key is never shown again.</summary>
public sealed record TrackingDeviceKeyResponse(TrackingDeviceResponse Device, string ApiKey);

public sealed record LinkVehicleRequest(Guid? VehicleId);

public sealed class TrackingDeviceRequestValidator : AbstractValidator<TrackingDeviceRequest>
{
    public TrackingDeviceRequestValidator()
    {
        RuleFor(x => x.TrackingProviderId).NotNull().WithMessage("Provedor: campo obrigatório.");
        RuleFor(x => x.Identifier).Required("Identificador").MaxLen(TrackingDevice.IdentifierMaxLength);
        RuleFor(x => x.Model).MaxLen(TrackingDevice.ModelMaxLength);
    }
}

/// <summary>Tracker registry, keys and device ↔ vehicle links (ADR-051).</summary>
public sealed class TrackingDeviceService(IFleetDbContext db, IClock clock, IValidator<TrackingDeviceRequest> validator)
{
    public async Task<IReadOnlyList<TrackingDeviceResponse>> ListAsync(CancellationToken ct)
    {
        var devices = await db.TrackingDevices.OrderBy(d => d.Identifier)
            .Select(d => new
            {
                d.Id, d.TrackingProviderId, Provider = d.TrackingProvider.Name, d.Identifier, d.Model, d.IsActive, d.ApiKeyPrefix, d.KeyIssuedAt,
                Link = db.VehicleDevices.Where(l => l.TrackingDeviceId == d.Id && l.EndedAt == null)
                    .Select(l => new { l.VehicleId, l.Vehicle.LicensePlate, l.StartedAt }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var ids = devices.Select(d => d.Id).ToList();
        var last = await db.VehiclePositions.Where(p => ids.Contains(p.TrackingDeviceId))
            .GroupBy(p => p.TrackingDeviceId).Select(g => new { g.Key, At = g.Max(p => p.RecordedAt) })
            .ToDictionaryAsync(g => g.Key, g => (DateTime?)g.At, ct);
        var now = clock.UtcNow;
        return devices.Select(d =>
        {
            var at = last.GetValueOrDefault(d.Id);
            return new TrackingDeviceResponse(d.Id, d.TrackingProviderId, d.Provider, d.Identifier, d.Model, d.IsActive, d.ApiKeyPrefix, d.KeyIssuedAt,
                d.Link?.VehicleId, d.Link?.LicensePlate, d.Link?.StartedAt, at, TrackingRules.Status(at, now));
        }).ToList();
    }

    private async Task<TrackingDeviceResponse> GetAsync(Guid id, CancellationToken ct) => (await ListAsync(ct)).Single(d => d.Id == id);

    public async Task<TrackingDeviceKeyResponse> CreateAsync(TrackingDeviceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureProviderAsync(request.TrackingProviderId!.Value, ct);
        await EnsureUniqueAsync(request.Identifier!, null, ct);
        var key = TrackingRules.NewApiKey();
        var device = new TrackingDevice
        {
            TrackingProviderId = request.TrackingProviderId.Value, Identifier = request.Identifier!.Trim(), Model = request.Model.TrimToNull(),
            IsActive = request.IsActive, ApiKeyHash = TrackingRules.HashApiKey(key), ApiKeyPrefix = key[..TrackingDevice.KeyPrefixLength], KeyIssuedAt = clock.UtcNow,
        };
        db.TrackingDevices.Add(device);
        await db.SaveChangesAsync(ct);
        return new TrackingDeviceKeyResponse(await GetAsync(device.Id, ct), key);
    }

    public async Task<TrackingDeviceResponse> UpdateAsync(Guid id, TrackingDeviceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var device = await FindAsync(id, ct);
        await EnsureProviderAsync(request.TrackingProviderId!.Value, ct);
        await EnsureUniqueAsync(request.Identifier!, id, ct);
        device.TrackingProviderId = request.TrackingProviderId.Value;
        device.Identifier = request.Identifier!.Trim();
        device.Model = request.Model.TrimToNull();
        device.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Invalidates the old key at once (e.g. a leaked key or a replaced chip).</summary>
    public async Task<TrackingDeviceKeyResponse> RotateKeyAsync(Guid id, CancellationToken ct)
    {
        var device = await FindAsync(id, ct);
        var key = TrackingRules.NewApiKey();
        device.ApiKeyHash = TrackingRules.HashApiKey(key);
        device.ApiKeyPrefix = key[..TrackingDevice.KeyPrefixLength];
        device.KeyIssuedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return new TrackingDeviceKeyResponse(await GetAsync(id, ct), key);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var device = await FindAsync(id, ct);
        await EndLinksAsync(l => l.TrackingDeviceId == id, ct);
        db.TrackingDevices.Remove(device);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Installs the device on a vehicle (or removes it, with null). One device per vehicle and one vehicle per device at a
    /// time — guaranteed by filtered unique indexes; ending the old links and opening the new one are two saves in one
    /// transaction (EF does not order commands by filtered indexes, same as assignments).
    /// </summary>
    public async Task<TrackingDeviceResponse> LinkAsync(Guid id, LinkVehicleRequest request, CancellationToken ct)
    {
        var device = await FindAsync(id, ct);
        if (request.VehicleId is { } vehicleId)
        {
            var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
                ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
            if (vehicle.Status == VehicleStatus.Inactive)
                throw new BusinessRuleException("Não é possível instalar um rastreador em veículo inativo.");
        }
        await db.InTransactionAsync(async () =>
        {
            await EndLinksAsync(l => l.TrackingDeviceId == device.Id || (request.VehicleId != null && l.VehicleId == request.VehicleId), ct);
            await db.SaveChangesAsync(ct);
            if (request.VehicleId is { } v)
            {
                db.VehicleDevices.Add(new VehicleDevice { TrackingDeviceId = device.Id, VehicleId = v, StartedAt = clock.UtcNow });
                await db.SaveChangesAsync(ct);
            }
        }, ct);
        return await GetAsync(id, ct);
    }

    private async Task EndLinksAsync(System.Linq.Expressions.Expression<Func<VehicleDevice, bool>> which, CancellationToken ct)
    {
        foreach (var link in await db.VehicleDevices.Where(l => l.EndedAt == null).Where(which).ToListAsync(ct)) link.EndedAt = clock.UtcNow;
    }

    private async Task EnsureProviderAsync(Guid providerId, CancellationToken ct)
    {
        if (!await db.TrackingProviders.AnyAsync(p => p.Id == providerId, ct))
            throw new NotFoundException("Provedor de rastreamento não encontrado.");
    }

    private async Task EnsureUniqueAsync(string identifier, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = identifier.Trim();
        if (await db.TrackingDevices.AnyAsync(d => d.Identifier == trimmed && d.Id != exceptId, ct))
            throw new ConflictException("Já existe um rastreador com este identificador nesta empresa.", "identifier");
    }

    private async Task<TrackingDevice> FindAsync(Guid id, CancellationToken ct) =>
        await db.TrackingDevices.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Rastreador não encontrado.");
}

// ---------------- Ingestion

public sealed record IngestPosition
{
    public DateTime? RecordedAt { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public decimal? SpeedKmh { get; init; }
    public int? Heading { get; init; }
    public bool? Ignition { get; init; }
    public int? OdometerKm { get; init; }
}

public sealed record IngestRequest
{
    public IReadOnlyList<IngestPosition>? Positions { get; init; }
}

public sealed record IngestRejection(int Index, string Reason);

public sealed record IngestResponse(int Accepted, int Duplicates, IReadOnlyList<IngestRejection> Rejected);

/// <summary>
/// Receives positions pushed by trackers (ADR-051). Authenticated by the device key (no user): the device is found by
/// the key hash across companies, and its company is written explicitly on every position.
/// </summary>
public sealed class TrackingIngestionService(IFleetDbContext db, IClock clock, ILogger<TrackingIngestionService> logger)
{
    public async Task<IngestResponse> IngestAsync(string? apiKey, IngestRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith("fk_", StringComparison.Ordinal))
            throw new AuthenticationFailedException("Chave do rastreador ausente ou inválida.");
        var hash = TrackingRules.HashApiKey(apiKey.Trim());
        // IgnoreQueryFilters: there is no signed-in user (no tenant yet); the key hash identifies device and company.
        // Soft-deleted and inactive devices are excluded explicitly.
        var device = await db.TrackingDevices.IgnoreQueryFilters()
            .Where(d => d.ApiKeyHash == hash && d.DeletedAt == null && d.IsActive && d.TrackingProvider.IsActive && d.TrackingProvider.DeletedAt == null)
            .Select(d => new { d.Id, d.CompanyId })
            .SingleOrDefaultAsync(ct)
            ?? throw new AuthenticationFailedException("Chave do rastreador ausente ou inválida.");

        var positions = request.Positions ?? [];
        if (positions.Count == 0) return new IngestResponse(0, 0, []);
        if (positions.Count > TrackingRules.MaxPositionsPerRequest)
            throw new BusinessRuleException($"Envie no máximo {TrackingRules.MaxPositionsPerRequest} posições por requisição.");

        var now = clock.UtcNow;
        var rejected = new List<IngestRejection>();
        var valid = new List<(int Index, PositionInput Input)>();
        for (var i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            if (p.RecordedAt is null || p.Latitude is null || p.Longitude is null)
            {
                rejected.Add(new(i, "recordedAt, latitude e longitude são obrigatórios"));
                continue;
            }
            var input = new PositionInput(DateTime.SpecifyKind(p.RecordedAt.Value.ToUniversalTime(), DateTimeKind.Utc),
                Math.Round(p.Latitude.Value, 6), Math.Round(p.Longitude.Value, 6), p.SpeedKmh, p.Heading, p.Ignition, p.OdometerKm);
            if (TrackingRules.Reject(input, now) is { } reason) rejected.Add(new(i, reason));
            else valid.Add((i, input));
        }

        var link = await db.VehicleDevices.IgnoreQueryFilters()
            .Where(l => l.TrackingDeviceId == device.Id && l.EndedAt == null && l.CompanyId == device.CompanyId)
            .Select(l => (Guid?)l.VehicleId).FirstOrDefaultAsync(ct);
        var times = valid.Select(v => v.Input.RecordedAt).ToList();
        var known = (await db.VehiclePositions.IgnoreQueryFilters()
            .Where(p => p.TrackingDeviceId == device.Id && times.Contains(p.RecordedAt))
            .Select(p => p.RecordedAt).ToListAsync(ct)).ToHashSet();

        var accepted = 0;
        var duplicates = 0;
        foreach (var (_, input) in valid.DistinctBy(v => v.Input.RecordedAt))
        {
            if (known.Contains(input.RecordedAt)) { duplicates++; continue; }
            db.VehiclePositions.Add(new VehiclePosition
            {
                CompanyId = device.CompanyId, TrackingDeviceId = device.Id, VehicleId = link, RecordedAt = input.RecordedAt, ReceivedAt = now,
                Latitude = input.Latitude, Longitude = input.Longitude, SpeedKmh = input.SpeedKmh, Heading = input.Heading,
                Ignition = input.Ignition, DeviceOdometerKm = input.DeviceOdometerKm,
            });
            accepted++;
        }
        duplicates += valid.Count - valid.DistinctBy(v => v.Input.RecordedAt).Count();
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Tracking ingest for device {DeviceId}: {Accepted} accepted, {Duplicates} duplicates, {Rejected} rejected",
            device.Id, accepted, duplicates, rejected.Count);
        return new IngestResponse(accepted, duplicates, rejected);
    }
}

// ---------------- Queries (map)

public sealed record VehicleLocation(
    Guid VehicleId, string LicensePlate, string Model, VehicleOperationalStatus OperationalStatus, string? DriverName,
    decimal? Latitude, decimal? Longitude, decimal? SpeedKmh, int? Heading, bool? Ignition, DateTime? RecordedAt, TrackingConnectionStatus Status,
    string? DeviceIdentifier);

public sealed record PositionPoint(DateTime RecordedAt, decimal Latitude, decimal Longitude, decimal? SpeedKmh, bool? Ignition);

public sealed record VehicleTrackResponse(
    Guid VehicleId, DateTime From, DateTime To, IReadOnlyList<PositionPoint> Points, bool Truncated, decimal? MaxSpeedKmh, int Count);

/// <summary>Fleet map and route history (spec §21).</summary>
public sealed class TrackingQueryService(IFleetDbContext db, IClock clock, ICurrentUser currentUser)
{
    /// <summary>Vehicles with an installed device or a recent position, with their last fix and connection status.</summary>
    public async Task<IReadOnlyList<VehicleLocation>> FleetAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var since = now.AddDays(-TrackingRules.MaxPositionAgeDays);
        var lastAt = await db.VehiclePositions.Where(p => p.VehicleId != null && p.RecordedAt >= since)
            .GroupBy(p => p.VehicleId!.Value).Select(g => new { VehicleId = g.Key, At = g.Max(p => p.RecordedAt) })
            .ToDictionaryAsync(g => g.VehicleId, g => g.At, ct);
        var linked = await db.VehicleDevices.Where(l => l.EndedAt == null)
            .Select(l => new { l.VehicleId, l.TrackingDevice.Identifier }).ToDictionaryAsync(l => l.VehicleId, l => l.Identifier, ct);
        var vehicleIds = lastAt.Keys.Union(linked.Keys).ToList();

        var times = lastAt.Values.ToList();
        var fixes = (await db.VehiclePositions.Where(p => p.VehicleId != null && vehicleIds.Contains(p.VehicleId.Value) && times.Contains(p.RecordedAt))
                .ToListAsync(ct))
            .Where(p => lastAt.TryGetValue(p.VehicleId!.Value, out var at) && at == p.RecordedAt)
            .GroupBy(p => p.VehicleId!.Value).ToDictionary(g => g.Key, g => g.First());

        var active = AssignmentService.Active(db);
        var canSeeDrivers = currentUser.HasPermission(Domain.Authorization.Permissions.Drivers.View);
        var vehicles = await db.Vehicles.Where(v => vehicleIds.Contains(v.Id))
            .Select(v => new
            {
                v.Id, v.LicensePlate, v.Model, v.Status, Assigned = active.Any(a => a.VehicleId == v.Id),
                Driver = active.Where(a => a.VehicleId == v.Id).Select(a => a.Driver.FullName).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return vehicles.OrderBy(v => v.LicensePlate).Select(v =>
        {
            var fix = fixes.GetValueOrDefault(v.Id);
            return new VehicleLocation(v.Id, v.LicensePlate, v.Model, VehicleOperationalState.From(v.Status, v.Assigned), canSeeDrivers ? v.Driver : null,
                fix?.Latitude, fix?.Longitude, fix?.SpeedKmh, fix?.Heading, fix?.Ignition, fix?.RecordedAt,
                TrackingRules.Status(fix?.RecordedAt, now), linked.GetValueOrDefault(v.Id));
        }).ToList();
    }

    public async Task<VehicleTrackResponse> TrackAsync(Guid vehicleId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
        var end = to?.ToUniversalTime() ?? clock.UtcNow;
        var start = from?.ToUniversalTime() ?? end.AddHours(-24);
        if (start > end) throw new BusinessRuleException("A data inicial deve ser anterior à final.");
        if (end - start > TimeSpan.FromDays(TrackingRules.MaxHistoryDays))
            throw new BusinessRuleException($"Escolha um período de até {TrackingRules.MaxHistoryDays} dias.");

        var query = db.VehiclePositions.Where(p => p.VehicleId == vehicleId && p.RecordedAt >= start && p.RecordedAt <= end);
        var count = await query.CountAsync(ct);
        var maxSpeed = count == 0 ? null : await query.MaxAsync(p => p.SpeedKmh, ct);
        // Keep the route readable and the payload bounded: above the limit, take every n-th point (always keeping the last).
        var points = await query.OrderBy(p => p.RecordedAt)
            .Select(p => new PositionPoint(p.RecordedAt, p.Latitude, p.Longitude, p.SpeedKmh, p.Ignition)).ToListAsync(ct);
        var truncated = points.Count > TrackingRules.MaxHistoryPoints;
        if (truncated)
        {
            var step = (int)Math.Ceiling(points.Count / (double)TrackingRules.MaxHistoryPoints);
            points = points.Where((_, i) => i % step == 0).Append(points[^1]).Distinct().ToList();
        }
        return new VehicleTrackResponse(vehicleId, start, end, points, truncated, maxSpeed, count);
    }
}
