using Fleet.Application.Common;
using Fleet.Domain.Common;
using Fleet.Domain.Tires;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tires;

// ---------- Tire models (catalog) ----------

public sealed record TireModelRequest
{
    public string? Brand { get; init; }
    public string? Name { get; init; }
    public string? Size { get; init; }
    public TireApplication Application { get; init; } = TireApplication.AllPosition;
    public TireConstruction Construction { get; init; } = TireConstruction.Radial;
    public string? LoadIndex { get; init; }
    public string? SpeedRating { get; init; }
    public decimal? OriginalTreadDepthMm { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
}

public sealed record TireModelResponse(
    Guid Id, string Brand, string Name, string Size, TireApplication Application, TireConstruction Construction, string? LoadIndex,
    string? SpeedRating, decimal? OriginalTreadDepthMm, bool IsActive, string? Notes, int TireCount);

public sealed class TireModelRequestValidator : AbstractValidator<TireModelRequest>
{
    public TireModelRequestValidator()
    {
        RuleFor(x => x.Brand).Required("Marca").MaxLen(TireModel.BrandMaxLength);
        RuleFor(x => x.Name).Required("Modelo").MaxLen(TireModel.NameMaxLength);
        RuleFor(x => x.Size).Required("Medida").MaxLen(TireModel.SizeMaxLength)
            .Matches(@"^[0-9A-Za-z/.,\- ]*$").WithMessage("Medida: use o formato impresso no pneu (ex.: 295/80R22.5).");
        RuleFor(x => x.Application).IsInEnum().WithMessage("Aplicação inválida.");
        RuleFor(x => x.Construction).IsInEnum().WithMessage("Construção inválida.");
        RuleFor(x => x.LoadIndex).MaxLen(TireModel.RatingMaxLength);
        RuleFor(x => x.SpeedRating).MaxLen(TireModel.RatingMaxLength);
        RuleFor(x => x.OriginalTreadDepthMm).InclusiveBetween(1m, 40m).WithMessage("Sulco original deve estar entre 1 e 40 mm.");
        RuleFor(x => x.Notes).MaxLen(TireModel.NotesMaxLength);
    }
}

/// <summary>Per-company catalog of tire models (ADR-035). A model in use is deactivated, never deleted.</summary>
public sealed class TireModelService(IFleetDbContext db, IValidator<TireModelRequest> validator)
{
    public async Task<IReadOnlyList<TireModelResponse>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.TireModels.AsQueryable();
        if (!includeInactive) query = query.Where(m => m.IsActive);
        return await query.OrderBy(m => m.Brand).ThenBy(m => m.Name).ThenBy(m => m.Size).Select(Projection()).ToListAsync(ct);
    }

    /// <summary>Distinct brands for the list filter.</summary>
    public async Task<IReadOnlyList<string>> BrandsAsync(CancellationToken ct) =>
        await db.TireModels.Select(m => m.Brand).Distinct().OrderBy(b => b).ToListAsync(ct);

    public async Task<TireModelResponse> CreateAsync(TireModelRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var model = new TireModel();
        await ApplyAsync(request, model, ct);
        db.TireModels.Add(model);
        await db.SaveChangesAsync(ct);
        return await GetAsync(model.Id, ct);
    }

    public async Task<TireModelResponse> UpdateAsync(Guid id, TireModelRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var model = await LoadAsync(id, ct);
        // The size is what positions are checked against: changing it would silently change installed tires.
        if (TireSizes.Normalize(request.Size) != model.Size && await db.Tires.AnyAsync(t => t.TireModelId == id, ct))
            throw new BusinessRuleException("Este modelo já tem pneus cadastrados, então a medida não pode mudar. Cadastre um novo modelo.");
        await ApplyAsync(request, model, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var model = await LoadAsync(id, ct);
        if (await db.Tires.AnyAsync(t => t.TireModelId == id, ct))
            throw new BusinessRuleException("Este modelo tem pneus cadastrados e não pode ser excluído. Inative-o para que não seja mais usado.");
        db.TireModels.Remove(model);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(TireModelRequest request, TireModel model, CancellationToken ct)
    {
        var brand = request.Brand!.Trim();
        var name = request.Name!.Trim();
        var size = TireSizes.Normalize(request.Size);
        if (await db.TireModels.AnyAsync(m => m.Brand == brand && m.Name == name && m.Size == size && m.Id != model.Id, ct))
            throw new ConflictException("Já existe um modelo com esta marca, modelo e medida.", "name");
        model.Brand = brand;
        model.Name = name;
        model.Size = size;
        model.Application = request.Application;
        model.Construction = request.Construction;
        model.LoadIndex = request.LoadIndex.TrimToNull()?.ToUpperInvariant();
        model.SpeedRating = request.SpeedRating.TrimToNull()?.ToUpperInvariant();
        model.OriginalTreadDepthMm = request.OriginalTreadDepthMm;
        model.IsActive = request.IsActive;
        model.Notes = request.Notes.TrimToNull();
    }

    private System.Linq.Expressions.Expression<Func<TireModel, TireModelResponse>> Projection() => m => new TireModelResponse(
        m.Id, m.Brand, m.Name, m.Size, m.Application, m.Construction, m.LoadIndex, m.SpeedRating, m.OriginalTreadDepthMm, m.IsActive,
        m.Notes, db.Tires.Count(t => t.TireModelId == m.Id));

    private async Task<TireModelResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.TireModels.Where(m => m.Id == id).Select(Projection()).SingleAsync(ct);

    private async Task<TireModel> LoadAsync(Guid id, CancellationToken ct) =>
        await db.TireModels.SingleOrDefaultAsync(m => m.Id == id, ct)
        ?? throw new NotFoundException("Modelo de pneu não encontrado. Ele pode ter sido excluído.");
}

// ---------- Axle layouts ----------

public sealed record TireLayoutAxleRequest
{
    public AxleType? Type { get; init; }
    public bool IsDual { get; init; }
    public bool IsRequired { get; init; } = true;
    public string? AllowedSize { get; init; }
    public decimal? RecommendedPressurePsi { get; init; }
}

public sealed record TireLayoutRequest
{
    public string? Name { get; init; }
    public TireLayoutTarget? Target { get; init; }
    public string? Description { get; init; }
    public int SpareCount { get; init; }
    public bool IsActive { get; init; } = true;
    /// <summary>Front to rear: the order is the axle number.</summary>
    public IReadOnlyList<TireLayoutAxleRequest> Axles { get; init; } = [];
}

public sealed record TireLayoutAxleResponse(
    int Number, AxleType Type, bool IsDual, bool IsRequired, string? AllowedSize, decimal? RecommendedPressurePsi);

public sealed record TirePositionResponse(
    string Code, string Label, int AxleNumber, AxleType? AxleType, TireSide Side, TirePlacement Placement, bool IsRequired,
    string? AllowedSize, decimal? RecommendedPressurePsi);

public sealed record TireLayoutResponse(
    Guid Id, string Name, TireLayoutTarget Target, string? Description, int SpareCount, bool IsActive,
    IReadOnlyList<TireLayoutAxleResponse> Axles, IReadOnlyList<TirePositionResponse> Positions, int AssetCount, int TirePositionCount);

public sealed class TireLayoutRequestValidator : AbstractValidator<TireLayoutRequest>
{
    public TireLayoutRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(TireLayout.NameMaxLength);
        RuleFor(x => x.Target).NotNull().WithMessage("Tipo: informe se a configuração é de veículo ou de implemento.")
            .IsInEnum().WithMessage("Tipo inválido.");
        RuleFor(x => x.Description).MaxLen(TireLayout.DescriptionMaxLength);
        RuleFor(x => x.SpareCount).InclusiveBetween(0, TireLayout.MaxSpares).WithMessage($"Estepes: de 0 a {TireLayout.MaxSpares}.");
        RuleFor(x => x.Axles).Must(a => a.Count is >= 1 and <= TireLayout.MaxAxles)
            .WithMessage($"Informe de 1 a {TireLayout.MaxAxles} eixos.");
        RuleForEach(x => x.Axles).ChildRules(axle =>
        {
            axle.RuleFor(a => a.Type).NotNull().WithMessage("Tipo do eixo: campo obrigatório.").IsInEnum().WithMessage("Tipo de eixo inválido.");
            axle.RuleFor(a => a.AllowedSize).MaxLen(TireModel.SizeMaxLength);
            axle.RuleFor(a => a.RecommendedPressurePsi).InclusiveBetween(1m, TirePressure.MaxPsi)
                .WithMessage($"Pressão de referência deve estar entre 1 e {BrazilianFormat.Number(TirePressure.MaxPsi)} psi.");
        });
    }
}

/// <summary>
/// Configurable axle layouts (seções 8–10), shared by vehicles and implements (one concept for both). Every company starts with
/// <see cref="TireLayoutDefaults"/>. A layout cannot lose a position that holds a tire on any asset using it.
/// </summary>
public sealed class TireLayoutService(IFleetDbContext db, ICurrentUser currentUser, IValidator<TireLayoutRequest> validator)
{
    public async Task<IReadOnlyList<TireLayoutResponse>> ListAsync(TireLayoutTarget? target, bool includeInactive, CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);
        var query = db.TireLayouts.Include(l => l.Axles).AsQueryable();
        if (target is { } t) query = query.Where(l => l.Target == t);
        if (!includeInactive) query = query.Where(l => l.IsActive);
        var layouts = await query.OrderBy(l => l.Target).ThenBy(l => l.Name).ToListAsync(ct);
        return await ToResponsesAsync(layouts, ct);
    }

    public async Task<TireLayoutResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await ToResponsesAsync([await LoadAsync(id, ct)], ct)).Single();

    public async Task<TireLayoutResponse> CreateAsync(TireLayoutRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var layout = new TireLayout();
        await ApplyAsync(request, layout, ct);
        db.TireLayouts.Add(layout);
        await db.SaveChangesAsync(ct);
        return await GetAsync(layout.Id, ct);
    }

    public async Task<TireLayoutResponse> UpdateAsync(Guid id, TireLayoutRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var layout = await LoadAsync(id, ct);
        if (request.Target != layout.Target && await AssetCountAsync(id, ct) > 0)
            throw new BusinessRuleException("Esta configuração já está em uso, então o tipo (veículo/implemento) não pode mudar.");

        var before = TirePositions.For(layout).Select(p => p.Code).ToHashSet();
        await ApplyAsync(request, layout, ct);
        var removed = before.Except(TirePositions.For(layout).Select(p => p.Code)).ToList();
        if (removed.Count > 0)
        {
            var occupied = await OccupiedPositionsAsync(id, ct);
            var blocked = removed.Where(occupied.Contains).ToList();
            if (blocked.Count > 0)
                throw new BusinessRuleException(
                    $"As posições {string.Join(", ", blocked)} têm pneus instalados em veículos/implementos com esta configuração. " +
                    "Remova esses pneus antes de tirar as posições.");
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var layout = await LoadAsync(id, ct);
        if (await AssetCountAsync(id, ct) > 0)
            throw new BusinessRuleException("Esta configuração está em uso por veículos ou implementos. Troque a configuração deles ou inative-a.");
        db.TireLayouts.Remove(layout);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Idempotent defaults on first read (same pattern as document and fuel types).</summary>
    public async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        // IgnoreQueryFilters so deleted layouts count too: a company that removed them must not get them back.
        var companyId = currentUser.CompanyId;
        if (await db.TireLayouts.IgnoreQueryFilters().AnyAsync(l => l.CompanyId == companyId, ct)) return;
        db.TireLayouts.AddRange(CreateDefaults());
        await db.SaveChangesAsync(ct);
    }

    public static IEnumerable<TireLayout> CreateDefaults(Guid? companyId = null) => TireLayoutDefaults.All.Select(d => new TireLayout
    {
        CompanyId = companyId ?? Guid.Empty,
        Name = d.Name,
        Target = d.Target,
        Description = d.Description,
        SpareCount = d.SpareCount,
        Axles = d.Axles.Select((a, i) => new TireLayoutAxle
        {
            CompanyId = companyId ?? Guid.Empty, Number = i + 1, Type = a.Type, IsDual = a.IsDual, IsRequired = a.IsRequired,
        }).ToList(),
    });

    public static TirePositionResponse ToResponse(TirePosition p) =>
        new(p.Code, p.Label, p.AxleNumber, p.AxleType, p.Side, p.Placement, p.IsRequired, p.AllowedSize, p.RecommendedPressurePsi);

    private async Task ApplyAsync(TireLayoutRequest request, TireLayout layout, CancellationToken ct)
    {
        var name = request.Name!.Trim();
        if (await db.TireLayouts.AnyAsync(l => l.Name == name && l.Id != layout.Id, ct))
            throw new ConflictException("Já existe uma configuração de eixos com este nome.", "name");
        layout.Name = name;
        layout.Target = request.Target!.Value;
        layout.Description = request.Description.TrimToNull();
        layout.SpareCount = request.SpareCount;
        layout.IsActive = request.IsActive;

        // Axles are value-like rows: rebuilt in place (number = order), keeping ids for unchanged numbers.
        for (var i = 0; i < request.Axles.Count; i++)
        {
            var source = request.Axles[i];
            var axle = layout.Axles.SingleOrDefault(a => a.Number == i + 1);
            if (axle is null)
            {
                axle = new TireLayoutAxle { Number = i + 1 };
                layout.Axles.Add(axle);
            }
            axle.Type = source.Type!.Value;
            axle.IsDual = source.IsDual;
            axle.IsRequired = source.IsRequired;
            axle.AllowedSize = source.AllowedSize.TrimToNull() is { } size ? TireSizes.Normalize(size) : null;
            axle.RecommendedPressurePsi = source.RecommendedPressurePsi;
        }
        foreach (var extra in layout.Axles.Where(a => a.Number > request.Axles.Count).ToList())
            layout.Axles.Remove(extra);
    }

    private Task<int> AssetCountAsync(Guid layoutId, CancellationToken ct) =>
        db.Vehicles.Where(v => v.TireLayoutId == layoutId).Select(v => v.Id)
            .Concat(db.Implements.Where(i => i.TireLayoutId == layoutId).Select(i => i.Id))
            .CountAsync(ct);

    private async Task<HashSet<string>> OccupiedPositionsAsync(Guid layoutId, CancellationToken ct) =>
        (await db.TireInstallations
            .Where(i => i.RemovedAt == null &&
                        ((i.VehicleId != null && i.Vehicle!.TireLayoutId == layoutId) ||
                         (i.ImplementId != null && i.Implement!.TireLayoutId == layoutId)))
            .Select(i => i.PositionCode).Distinct().ToListAsync(ct)).ToHashSet();

    private async Task<IReadOnlyList<TireLayoutResponse>> ToResponsesAsync(IReadOnlyList<TireLayout> layouts, CancellationToken ct)
    {
        var ids = layouts.Select(l => l.Id).ToList();
        var vehicleCounts = await db.Vehicles.Where(v => v.TireLayoutId != null && ids.Contains(v.TireLayoutId.Value))
            .GroupBy(v => v.TireLayoutId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var implementCounts = await db.Implements.Where(i => i.TireLayoutId != null && ids.Contains(i.TireLayoutId.Value))
            .GroupBy(i => i.TireLayoutId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(Guid id) => (vehicleCounts.SingleOrDefault(c => c.Key == id)?.Count ?? 0) +
                              (implementCounts.SingleOrDefault(c => c.Key == id)?.Count ?? 0);

        return layouts.Select(l =>
        {
            var positions = TirePositions.For(l);
            return new TireLayoutResponse(l.Id, l.Name, l.Target, l.Description, l.SpareCount, l.IsActive,
                l.Axles.OrderBy(a => a.Number).Select(a => new TireLayoutAxleResponse(a.Number, a.Type, a.IsDual, a.IsRequired, a.AllowedSize,
                    a.RecommendedPressurePsi)).ToList(),
                positions.Select(ToResponse).ToList(), Count(l.Id), positions.Count(p => !p.IsSpare));
        }).ToList();
    }

    private async Task<TireLayout> LoadAsync(Guid id, CancellationToken ct) =>
        await db.TireLayouts.Include(l => l.Axles).SingleOrDefaultAsync(l => l.Id == id, ct)
        ?? throw new NotFoundException("Configuração de eixos não encontrada. Ela pode ter sido excluída.");
}

// ---------- Tire policy settings ----------

public sealed record TireSettingsRequest
{
    public decimal MinTreadDepthMm { get; init; } = 3m;
    public decimal TreadWarningDepthMm { get; init; } = 4m;
    public int InspectionIntervalDays { get; init; } = 30;
    public int MaxAgeYears { get; init; } = 5;
    public int PressureTolerancePercent { get; init; } = 10;
    public TirePressureUnit PressureUnit { get; init; } = TirePressureUnit.Psi;
    public decimal RapidWearMmPer1000Km { get; init; } = 0.5m;
    public int MinExpectedLifeKm { get; init; }
    public bool AutoMaintenanceRequestOnUnfit { get; init; }
}

public sealed record TireSettingsResponse(
    decimal MinTreadDepthMm, decimal TreadWarningDepthMm, int InspectionIntervalDays, int MaxAgeYears, int PressureTolerancePercent,
    TirePressureUnit PressureUnit, decimal RapidWearMmPer1000Km, int MinExpectedLifeKm, bool AutoMaintenanceRequestOnUnfit);

public sealed class TireSettingsRequestValidator : AbstractValidator<TireSettingsRequest>
{
    public TireSettingsRequestValidator()
    {
        RuleFor(x => x.MinTreadDepthMm).InclusiveBetween(0.5m, 20m).WithMessage("Sulco mínimo deve estar entre 0,5 e 20 mm.");
        RuleFor(x => x.TreadWarningDepthMm).InclusiveBetween(0.5m, 25m).WithMessage("Sulco de aviso deve estar entre 0,5 e 25 mm.")
            .GreaterThanOrEqualTo(x => x.MinTreadDepthMm).WithMessage("O sulco de aviso deve ser maior ou igual ao sulco mínimo.");
        RuleFor(x => x.InspectionIntervalDays).InclusiveBetween(0, 365).WithMessage("Intervalo de inspeção: de 0 (desligado) a 365 dias.");
        RuleFor(x => x.MaxAgeYears).InclusiveBetween(0, 20).WithMessage("Idade máxima: de 0 (desligado) a 20 anos.");
        RuleFor(x => x.PressureTolerancePercent).InclusiveBetween(1, 50).WithMessage("Tolerância de pressão: de 1% a 50%.");
        RuleFor(x => x.PressureUnit).IsInEnum().WithMessage("Unidade de pressão inválida.");
        RuleFor(x => x.RapidWearMmPer1000Km).InclusiveBetween(0m, 10m).WithMessage("Desgaste rápido: de 0 (desligado) a 10 mm por 1.000 km.");
        RuleFor(x => x.MinExpectedLifeKm).InclusiveBetween(0, 1_000_000).WithMessage("Vida mínima esperada: de 0 (desligado) a 1.000.000 km.");
    }
}

public sealed class TireSettingsService(IFleetDbContext db, IValidator<TireSettingsRequest> validator)
{
    public async Task<TireSettings> CurrentAsync(CancellationToken ct) =>
        await db.TireSettings.AsNoTracking().SingleOrDefaultAsync(ct) ?? TireSettings.Defaults();

    public async Task<TireSettingsResponse> GetAsync(CancellationToken ct) => ToResponse(await CurrentAsync(ct));

    public async Task<TireSettingsResponse> UpdateAsync(TireSettingsRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var settings = await db.TireSettings.SingleOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new TireSettings();
            db.TireSettings.Add(settings);
        }
        settings.MinTreadDepthMm = request.MinTreadDepthMm;
        settings.TreadWarningDepthMm = request.TreadWarningDepthMm;
        settings.InspectionIntervalDays = request.InspectionIntervalDays;
        settings.MaxAgeYears = request.MaxAgeYears;
        settings.PressureTolerancePercent = request.PressureTolerancePercent;
        settings.PressureUnit = request.PressureUnit;
        settings.RapidWearMmPer1000Km = request.RapidWearMmPer1000Km;
        settings.MinExpectedLifeKm = request.MinExpectedLifeKm;
        settings.AutoMaintenanceRequestOnUnfit = request.AutoMaintenanceRequestOnUnfit;
        await db.SaveChangesAsync(ct);
        return ToResponse(settings);
    }

    private static TireSettingsResponse ToResponse(TireSettings s) => new(
        s.MinTreadDepthMm, s.TreadWarningDepthMm, s.InspectionIntervalDays, s.MaxAgeYears, s.PressureTolerancePercent, s.PressureUnit,
        s.RapidWearMmPer1000Km, s.MinExpectedLifeKm, s.AutoMaintenanceRequestOnUnfit);
}
