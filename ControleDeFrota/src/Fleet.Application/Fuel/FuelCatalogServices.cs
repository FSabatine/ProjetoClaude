using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Common;
using Fleet.Domain.Fuel;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Fuel;

// ---------- Fuel types ----------

public sealed record FuelTypeRequest
{
    public string? Name { get; init; }
    public string? Code { get; init; }
    public FuelCategory? Category { get; init; }
    public FuelUnit Unit { get; init; } = FuelUnit.Liter;
    public bool IsActive { get; init; } = true;
    public string? Description { get; init; }
}

public sealed record FuelTypeResponse(
    Guid Id, string Name, string Code, FuelCategory Category, FuelUnit Unit, bool IsActive, string? Description, int FuelingCount);

public sealed class FuelTypeRequestValidator : AbstractValidator<FuelTypeRequest>
{
    public FuelTypeRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(FuelType.NameMaxLength);
        RuleFor(x => x.Code).Required("Código").MaxLen(FuelType.CodeMaxLength)
            .Matches("^[A-Za-z0-9-]*$").WithMessage("Código: use apenas letras, números e hífen (ex.: S10).");
        RuleFor(x => x.Category).NotNull().WithMessage("Categoria: campo obrigatório.").IsInEnum().WithMessage("Categoria inválida.");
        RuleFor(x => x.Unit).IsInEnum().WithMessage("Unidade inválida.");
        RuleFor(x => x.Description).MaxLen(FuelType.DescriptionMaxLength);
    }
}

/// <summary>Per-company fuel catalog (ADR-031). Never hard-coded; a type in use is deactivated, not deleted.</summary>
public sealed class FuelTypeService(IFleetDbContext db, ICurrentUser currentUser, IValidator<FuelTypeRequest> validator)
{
    public async Task<IReadOnlyList<FuelTypeResponse>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);
        var query = db.FuelTypes.AsQueryable();
        if (!includeInactive) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.Name).Select(Projection()).ToListAsync(ct);
    }

    public async Task<FuelTypeResponse> CreateAsync(FuelTypeRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var type = new FuelType();
        await ApplyAsync(request, type, ct);
        db.FuelTypes.Add(type);
        await db.SaveChangesAsync(ct);
        return await GetAsync(type.Id, ct);
    }

    public async Task<FuelTypeResponse> UpdateAsync(Guid id, FuelTypeRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var type = await LoadAsync(id, ct);
        // Changing the unit would turn every past "km/L" of this product into a different measure.
        if (request.Unit != type.Unit && await db.Fuelings.AnyAsync(f => f.FuelTypeId == id, ct))
            throw new BusinessRuleException("Este combustível já tem abastecimentos registrados, então a unidade não pode mudar. Crie um novo tipo.");
        await ApplyAsync(request, type, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var type = await LoadAsync(id, ct);
        if (await db.Fuelings.AnyAsync(f => f.FuelTypeId == id, ct))
            throw new BusinessRuleException("Este combustível tem abastecimentos registrados e não pode ser excluído. Inative-o para que não seja mais usado.");
        db.FuelTypes.Remove(type);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Every company starts with the default catalog the first time it is read. Idempotent (same as DocumentTypeService).</summary>
    public async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        // IgnoreQueryFilters so deleted types also count: a company that removed every type must not get them back.
        var companyId = currentUser.CompanyId;
        if (await db.FuelTypes.IgnoreQueryFilters().AnyAsync(t => t.CompanyId == companyId, ct)) return;
        db.FuelTypes.AddRange(CreateDefaults());
        await db.SaveChangesAsync(ct);
    }

    public static IEnumerable<FuelType> CreateDefaults(Guid? companyId = null) => FuelTypeDefaults.All.Select(d => new FuelType
    {
        CompanyId = companyId ?? Guid.Empty, Name = d.Name, Code = d.Code, Category = d.Category, Unit = d.Unit,
    });

    private async Task ApplyAsync(FuelTypeRequest request, FuelType type, CancellationToken ct)
    {
        var name = request.Name!.Trim();
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await db.FuelTypes.AnyAsync(t => t.Name == name && t.Id != type.Id, ct))
            throw new ConflictException("Já existe um combustível com este nome.", "name");
        if (await db.FuelTypes.AnyAsync(t => t.Code == code && t.Id != type.Id, ct))
            throw new ConflictException("Já existe um combustível com este código.", "code");
        type.Name = name;
        type.Code = code;
        type.Category = request.Category!.Value;
        type.Unit = request.Unit;
        type.IsActive = request.IsActive;
        type.Description = request.Description.TrimToNull();
    }

    private System.Linq.Expressions.Expression<Func<FuelType, FuelTypeResponse>> Projection() => t =>
        new FuelTypeResponse(t.Id, t.Name, t.Code, t.Category, t.Unit, t.IsActive, t.Description, db.Fuelings.Count(f => f.FuelTypeId == t.Id));

    private async Task<FuelTypeResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.FuelTypes.Where(t => t.Id == id).Select(Projection()).SingleAsync(ct);

    private async Task<FuelType> LoadAsync(Guid id, CancellationToken ct) =>
        await db.FuelTypes.SingleOrDefaultAsync(t => t.Id == id, ct)
        ?? throw new NotFoundException("Combustível não encontrado. Ele pode ter sido excluído.");
}

// ---------- Fuel stations and reference prices ----------

public sealed class FuelStationListRequest : ListRequest
{
    public bool? IsActive { get; set; }
    public bool? IsInternal { get; set; }
}

public sealed record FuelStationRequest
{
    public string? Name { get; init; }
    public string? Cnpj { get; init; }
    public AddressDto Address { get; init; } = new();
    public string? Phone { get; init; }
    public string? ContactName { get; init; }
    public bool IsInternal { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
}

public sealed record FuelStationResponse(
    Guid Id, string Name, string? Cnpj, AddressDto Address, string? Phone, string? ContactName, bool IsInternal, bool IsActive,
    string? Notes, int FuelingCount, DateOnly? LastFueledOn, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record FuelPriceRequest
{
    public Guid? FuelTypeId { get; init; }
    public decimal? Price { get; init; }
    public DateOnly? EffectiveFrom { get; init; }
    public string? Notes { get; init; }
}

public sealed record FuelPriceResponse(
    Guid Id, Guid FuelTypeId, string FuelTypeName, FuelUnit Unit, decimal Price, DateOnly EffectiveFrom, string? Notes,
    string? RecordedByName, DateTime CreatedAt);

public sealed class FuelStationRequestValidator : AbstractValidator<FuelStationRequest>
{
    public FuelStationRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(FuelStation.NameMaxLength);
        RuleFor(x => x.Cnpj).ValidCnpj().When(x => !string.IsNullOrWhiteSpace(x.Cnpj));
        RuleFor(x => x.Phone).ValidPhone().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.ContactName).MaxLen(FuelStation.ContactMaxLength);
        RuleFor(x => x.Notes).MaxLen(FuelStation.NotesMaxLength);
        RuleFor(x => x.Address).NotNull().SetValidator(new AddressValidator(required: false));
    }
}

public sealed class FuelPriceRequestValidator : AbstractValidator<FuelPriceRequest>
{
    public FuelPriceRequestValidator(IClock clock)
    {
        RuleFor(x => x.FuelTypeId).NotNull().WithMessage("Combustível: campo obrigatório.");
        RuleFor(x => x.Price).NotNull().WithMessage("Preço: campo obrigatório.")
            .GreaterThan(0).WithMessage("O preço deve ser maior que zero.")
            .LessThanOrEqualTo(FuelingAmounts.MaxUnitPrice).WithMessage("Preço muito alto. Confira o valor por unidade (ex.: 6,199).");
        RuleFor(x => x.EffectiveFrom).NotNull().WithMessage("Vigente a partir de: campo obrigatório.")
            .Must(d => d is null || d.Value <= clock.Today.AddDays(30))
            .WithMessage("A vigência pode começar no máximo 30 dias à frente.");
        RuleFor(x => x.Notes).MaxLen(FuelPrice.NotesMaxLength);
    }
}

/// <summary>
/// Fuel stations (seção 5/6): operational entities, not a supplier registry. Reference prices are manual history;
/// the price actually paid lives on each fueling and is never recalculated from here (seção 22).
/// </summary>
public sealed class FuelStationService(
    IFleetDbContext db,
    OperationalEventLog events,
    IValidator<FuelStationRequest> validator,
    IValidator<FuelPriceRequest> priceValidator)
{
    private static readonly SortMap<FuelStation> Sorts = new SortMap<FuelStation>("name")
        .Add("name", s => s.Name)
        .Add("createdAt", s => s.CreatedAt);

    public async Task<PagedResult<FuelStationResponse>> ListAsync(FuelStationListRequest request, CancellationToken ct)
    {
        var query = db.FuelStations.AsQueryable();
        if (request.IsActive is { } active) query = query.Where(s => s.IsActive == active);
        if (request.IsInternal is { } internalTank) query = query.Where(s => s.IsInternal == internalTank);
        if (request.SearchTerm is { } term)
        {
            var cnpj = Cnpj.Normalize(term);
            query = query.Where(s => s.Name.Contains(term) || (s.Cnpj != null && cnpj.Length > 0 && s.Cnpj.Contains(cnpj)) ||
                                     (s.Address.City != null && s.Address.City.Contains(term)));
        }
        return await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, Projection(), ct);
    }

    public async Task<FuelStationResponse> GetAsync(Guid id, CancellationToken ct) =>
        await db.FuelStations.Where(s => s.Id == id).Select(Projection()).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Posto não encontrado. Ele pode ter sido excluído.");

    public async Task<FuelStationResponse> CreateAsync(FuelStationRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var station = new FuelStation();
        await ApplyAsync(request, station, ct);
        db.FuelStations.Add(station);
        await db.SaveChangesAsync(ct);
        return await GetAsync(station.Id, ct);
    }

    public async Task<FuelStationResponse> UpdateAsync(Guid id, FuelStationRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var station = await LoadAsync(id, ct);
        await ApplyAsync(request, station, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var station = await LoadAsync(id, ct);
        if (await db.Fuelings.AnyAsync(f => f.FuelStationId == id, ct))
            throw new BusinessRuleException("Este posto tem abastecimentos registrados e não pode ser excluído. Inative-o.");
        foreach (var price in await db.FuelPrices.Where(p => p.FuelStationId == id).ToListAsync(ct)) db.FuelPrices.Remove(price);
        db.FuelStations.Remove(station);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FuelPriceResponse>> ListPricesAsync(Guid stationId, CancellationToken ct)
    {
        await LoadAsync(stationId, ct);
        var rows = await db.FuelPrices.Where(p => p.FuelStationId == stationId)
            .OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.FuelTypeId, p.FuelType.Name, p.FuelType.Unit, p.Price, p.EffectiveFrom, p.Notes, p.CreatedBy, p.CreatedAt })
            .Take(200)
            .ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, rows.Select(r => r.CreatedBy), ct);
        return rows.Select(r => new FuelPriceResponse(r.Id, r.FuelTypeId, r.Name, r.Unit, r.Price, r.EffectiveFrom, r.Notes,
            names.Get(r.CreatedBy), r.CreatedAt)).ToList();
    }

    public async Task<FuelPriceResponse> AddPriceAsync(Guid stationId, FuelPriceRequest request, CancellationToken ct)
    {
        await priceValidator.ValidateAndThrowAsync(request, ct);
        var station = await LoadAsync(stationId, ct);
        var type = await db.FuelTypes.SingleOrDefaultAsync(t => t.Id == request.FuelTypeId, ct)
            ?? throw ValidationErrors.ForField("fuelTypeId", "Combustível não encontrado.");

        var previous = await db.FuelPrices
            .Where(p => p.FuelStationId == stationId && p.FuelTypeId == type.Id && p.EffectiveFrom <= request.EffectiveFrom)
            .OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.CreatedAt)
            .Select(p => (decimal?)p.Price).FirstOrDefaultAsync(ct);

        var price = new FuelPrice
        {
            FuelStationId = stationId, FuelTypeId = type.Id, Price = request.Price!.Value,
            EffectiveFrom = request.EffectiveFrom!.Value, Notes = request.Notes.TrimToNull(),
        };
        db.FuelPrices.Add(price);
        // A station price is market information, not fleet spending, so the value can go in the (vehicle-less) event.
        events.Record(OperationalEventType.FuelPriceChanged, new EventSubject(nameof(FuelPrice), price.Id),
            $"Preço de referência de {type.Name} em {station.Name}: {Money(price.Price)}/{FuelUnits.Symbol(type.Unit)} " +
            $"a partir de {BrazilianFormat.Date(price.EffectiveFrom)}" + (previous is { } p ? $" (antes {Money(p)})." : "."),
            new { stationId, fuelTypeId = type.Id, price = price.Price, previousPrice = previous, price.EffectiveFrom });
        await db.SaveChangesAsync(ct);
        return (await ListPricesAsync(stationId, ct)).First(r => r.Id == price.Id);
    }

    /// <summary>Removes a price typed by mistake (soft delete, audited). Fuelings are not affected: they keep what was paid.</summary>
    public async Task DeletePriceAsync(Guid stationId, Guid priceId, CancellationToken ct)
    {
        var price = await db.FuelPrices.SingleOrDefaultAsync(p => p.Id == priceId && p.FuelStationId == stationId, ct)
            ?? throw new NotFoundException("Preço não encontrado.");
        db.FuelPrices.Remove(price);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(FuelStationRequest request, FuelStation station, CancellationToken ct)
    {
        var cnpj = string.IsNullOrWhiteSpace(request.Cnpj) ? null : Cnpj.Normalize(request.Cnpj);
        if (cnpj is not null && await db.FuelStations.AnyAsync(s => s.Cnpj == cnpj && s.Id != station.Id, ct))
            throw new ConflictException("Já existe um posto com este CNPJ.", "cnpj");
        station.Name = request.Name!.Trim();
        station.Cnpj = cnpj;
        request.Address.ApplyTo(station.Address);
        station.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : new string(request.Phone.Where(char.IsAsciiDigit).ToArray());
        station.ContactName = request.ContactName.TrimToNull();
        station.IsInternal = request.IsInternal;
        station.IsActive = request.IsActive;
        station.Notes = request.Notes.TrimToNull();
    }

    private System.Linq.Expressions.Expression<Func<FuelStation, FuelStationResponse>> Projection() => s => new FuelStationResponse(
        s.Id, s.Name, s.Cnpj,
        new AddressDto
        {
            Street = s.Address.Street, Number = s.Address.Number, Complement = s.Address.Complement, Neighborhood = s.Address.Neighborhood,
            City = s.Address.City, State = s.Address.State, ZipCode = s.Address.ZipCode,
        },
        s.Phone, s.ContactName, s.IsInternal, s.IsActive, s.Notes,
        db.Fuelings.Count(f => f.FuelStationId == s.Id && f.Status != FuelingStatus.Cancelled),
        db.Fuelings.Where(f => f.FuelStationId == s.Id && f.Status != FuelingStatus.Cancelled).Max(f => (DateOnly?)f.FueledOn),
        s.CreatedAt, s.UpdatedAt);

    private async Task<FuelStation> LoadAsync(Guid id, CancellationToken ct) =>
        await db.FuelStations.SingleOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException("Posto não encontrado. Ele pode ter sido excluído.");

    private static string Money(decimal value) => $"R$ {BrazilianFormat.Number(value, 3)}";
}

// ---------- Settings ----------

public sealed record FuelSettingsRequest
{
    public int TankTolerancePercent { get; init; }
    public int PriceDeviationPercent { get; init; }
    public int ConsumptionDeviationPercent { get; init; }
    public int MinHoursBetweenFuelings { get; init; }
    public bool RequireDriver { get; init; }
}

public sealed record FuelSettingsResponse(
    int TankTolerancePercent, int PriceDeviationPercent, int ConsumptionDeviationPercent, int MinHoursBetweenFuelings, bool RequireDriver);

public sealed class FuelSettingsRequestValidator : AbstractValidator<FuelSettingsRequest>
{
    public FuelSettingsRequestValidator()
    {
        RuleFor(x => x.TankTolerancePercent).InclusiveBetween(0, FuelSettings.MaxPercent)
            .WithMessage($"Tolerância do tanque: informe de 0 a {FuelSettings.MaxPercent}%.");
        RuleFor(x => x.PriceDeviationPercent).InclusiveBetween(1, FuelSettings.MaxPercent)
            .WithMessage($"Variação de preço: informe de 1 a {FuelSettings.MaxPercent}%.");
        RuleFor(x => x.ConsumptionDeviationPercent).InclusiveBetween(1, FuelSettings.MaxPercent)
            .WithMessage($"Variação de consumo: informe de 1 a {FuelSettings.MaxPercent}%.");
        RuleFor(x => x.MinHoursBetweenFuelings).InclusiveBetween(0, FuelSettings.MaxHours)
            .WithMessage($"Intervalo mínimo: informe de 0 (desligado) a {FuelSettings.MaxHours} horas.");
    }
}

/// <summary>Anomaly thresholds per company (seção 21). Absent row = defaults; the first save creates it.</summary>
public sealed class FuelSettingsService(IFleetDbContext db, IValidator<FuelSettingsRequest> validator)
{
    public async Task<FuelSettings> CurrentAsync(CancellationToken ct) =>
        await db.FuelSettings.AsNoTracking().SingleOrDefaultAsync(ct) ?? FuelSettings.Defaults();

    public async Task<FuelSettingsResponse> GetAsync(CancellationToken ct) => ToResponse(await CurrentAsync(ct));

    public async Task<FuelSettingsResponse> UpdateAsync(FuelSettingsRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var settings = await db.FuelSettings.SingleOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new FuelSettings();
            db.FuelSettings.Add(settings);
        }
        settings.TankTolerancePercent = request.TankTolerancePercent;
        settings.PriceDeviationPercent = request.PriceDeviationPercent;
        settings.ConsumptionDeviationPercent = request.ConsumptionDeviationPercent;
        settings.MinHoursBetweenFuelings = request.MinHoursBetweenFuelings;
        settings.RequireDriver = request.RequireDriver;
        await db.SaveChangesAsync(ct);
        return ToResponse(settings);
    }

    private static FuelSettingsResponse ToResponse(FuelSettings s) =>
        new(s.TankTolerancePercent, s.PriceDeviationPercent, s.ConsumptionDeviationPercent, s.MinHoursBetweenFuelings, s.RequireDriver);
}
