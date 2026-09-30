using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Application.Operations;
using Fleet.Domain.Checklists;
using Fleet.Domain.Common;
using Fleet.Domain.Files;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Occurrences;

public sealed class OccurrenceListRequest : ListRequest
{
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? ImplementId { get; set; }
    public OccurrenceType? Type { get; set; }
    public OccurrenceSeverity? Severity { get; set; }
    public OccurrenceStatus? Status { get; set; }
    /// <summary>Open + InAnalysis — the "what still needs attention" view.</summary>
    public bool? OpenOnly { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record OccurrenceRequest
{
    public Guid? VehicleId { get; init; }
    public Guid? DriverId { get; init; }
    public Guid? ImplementId { get; init; }
    public OccurrenceType? Type { get; init; }
    public OccurrenceSeverity? Severity { get; init; }
    public DateTime? OccurredAt { get; init; }
    public string? Location { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public sealed record OccurrenceStatusRequest
{
    public OccurrenceStatus? Status { get; init; }
    /// <summary>Resolution (Resolved) or reason (Cancelled). Required to close.</summary>
    public string? Resolution { get; init; }
}

public sealed record OccurrenceListItemResponse(
    Guid Id,
    OccurrenceType Type,
    OccurrenceSeverity Severity,
    OccurrenceStatus Status,
    DateTime OccurredAt,
    string Description,
    Guid? VehicleId,
    string? LicensePlate,
    Guid? DriverId,
    string? DriverName,
    OccurrenceSource Source);

public sealed record OccurrenceResponse(
    Guid Id,
    OccurrenceType Type,
    OccurrenceSeverity Severity,
    OccurrenceStatus Status,
    IReadOnlyList<OccurrenceStatus> NextStatuses,
    DateTime OccurredAt,
    string? Location,
    string Description,
    Guid? VehicleId,
    string? LicensePlate,
    Guid? DriverId,
    string? DriverName,
    Guid? ImplementId,
    string? ImplementPlate,
    string? Resolution,
    DateTime? ClosedAt,
    string? ClosedByName,
    OccurrenceSource Source,
    Guid? ChecklistExecutionId,
    IReadOnlyList<FileResponse> Files,
    string? CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class OccurrenceListRequestValidator : AbstractValidator<OccurrenceListRequest>
{
    public OccurrenceListRequestValidator() => this.ValidPeriod(x => x.From, x => x.To, "to");
}

public sealed class OccurrenceRequestValidator : AbstractValidator<OccurrenceRequest>
{
    public OccurrenceRequestValidator(IClock clock)
    {
        RuleFor(x => x.VehicleId)
            .Must((x, v) => v is not null || x.DriverId is not null || x.ImplementId is not null)
            .WithMessage("Informe o veículo, o motorista ou o implemento envolvido.");
        RuleFor(x => x.Type).NotNull().WithMessage("Tipo: campo obrigatório.").IsInEnum().WithMessage("Tipo inválido.");
        RuleFor(x => x.Severity).NotNull().WithMessage("Gravidade: campo obrigatório.").IsInEnum().WithMessage("Gravidade inválida.");
        RuleFor(x => x.OccurredAt)
            .Must(d => d is null || d.Value <= clock.UtcNow.AddMinutes(5)).WithMessage("A data da ocorrência não pode ser futura.");
        RuleFor(x => x.Description).Required("Descrição").MaxLen(Occurrence.DescriptionMaxLength);
        RuleFor(x => x.Location).MaxLen(Occurrence.LocationMaxLength);
    }
}

public sealed class OccurrenceStatusRequestValidator : AbstractValidator<OccurrenceStatusRequest>
{
    public OccurrenceStatusRequestValidator()
    {
        RuleFor(x => x.Status).NotNull().WithMessage("Informe a nova situação.").IsInEnum().WithMessage("Situação inválida.");
        RuleFor(x => x.Resolution).MaxLen(Occurrence.ResolutionMaxLength);
        RuleFor(x => x.Resolution)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .When(x => x.Status is { } s && OccurrenceWorkflow.RequiresResolution(s))
            .WithMessage(x => x.Status == OccurrenceStatus.Cancelled
                ? "Informe o motivo do cancelamento."
                : "Descreva como a ocorrência foi resolvida.");
    }
}

/// <summary>General operational occurrences (ADR-023) with an explicit status workflow.</summary>
public sealed class OccurrenceService(
    IFleetDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    FileService files,
    OperationalEventLog events,
    IValidator<OccurrenceListRequest> listValidator,
    IValidator<OccurrenceRequest> validator,
    IValidator<OccurrenceStatusRequest> statusValidator)
{
    private static readonly SortMap<Occurrence> Sorts = new SortMap<Occurrence>("occurredAt")
        .Add("occurredAt", o => o.OccurredAt)
        .Add("severity", o => o.Severity)
        .Add("status", o => o.Status)
        .Add("type", o => o.Type);

    public static readonly string[] SeverityLabels = ["baixa", "média", "alta", "crítica"];

    public async Task<PagedResult<OccurrenceListItemResponse>> ListAsync(OccurrenceListRequest request, CancellationToken ct)
    {
        await listValidator.ValidateAndThrowAsync(request, ct);
        var query = db.Occurrences.AsQueryable();
        if (request.VehicleId is { } vehicleId) query = query.Where(o => o.VehicleId == vehicleId);
        if (request.DriverId is { } driverId) query = query.Where(o => o.DriverId == driverId);
        if (request.ImplementId is { } implementId) query = query.Where(o => o.ImplementId == implementId);
        if (request.Type is { } type) query = query.Where(o => o.Type == type);
        if (request.Severity is { } severity) query = query.Where(o => o.Severity == severity);
        if (request.Status is { } status) query = query.Where(o => o.Status == status);
        if (request.OpenOnly == true) query = Open(query);
        if (request.From is { } from)
        {
            var start = clock.StartOfBusinessDayUtc(from);
            query = query.Where(o => o.OccurredAt >= start);
        }
        if (request.To is { } to)
        {
            var end = clock.StartOfBusinessDayUtc(to.AddDays(1));
            query = query.Where(o => o.OccurredAt < end);
        }
        if (request.SearchTerm is { } term)
        {
            var plate = LicensePlate.Normalize(term);
            query = query.Where(o => o.Description.Contains(term) || (o.Vehicle != null && o.Vehicle.LicensePlate.Contains(plate)) ||
                                     (o.Driver != null && o.Driver.FullName.Contains(term)));
        }

        // Newest first by default: occurrences are a feed.
        var direction = request.SortBy is null ? SortDirection.Desc : request.SortDirection;
        var page = await Sorts.Apply(query, request.SortBy, direction).ToPagedResultAsync(request, o => new OccurrenceListItemResponse(
            o.Id, o.Type, o.Severity, o.Status, o.OccurredAt, o.Description,
            o.VehicleId, o.Vehicle != null ? o.Vehicle.LicensePlate : null,
            o.DriverId, o.Driver != null ? o.Driver.FullName : null, o.Source), ct);
        return page;
    }

    public async Task<OccurrenceResponse> GetAsync(Guid id, CancellationToken ct) => await ToResponseAsync(await LoadAsync(id, ct), ct);

    public async Task<OccurrenceResponse> CreateAsync(OccurrenceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var occurrence = new Occurrence { OccurredAt = Min(request.OccurredAt ?? clock.UtcNow, clock.UtcNow) };
        await ApplyAsync(request, occurrence, ct);
        db.Occurrences.Add(occurrence);
        await files.AttachAsync(request.FileIds, FileOwnerType.Occurrence, occurrence.Id, "fileIds", ct);
        await RecordCreatedAsync(occurrence, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(occurrence.Id, ct);
    }

    /// <summary>Details can be corrected while the occurrence is being handled; closed ones are history.</summary>
    public async Task<OccurrenceResponse> UpdateAsync(Guid id, OccurrenceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var occurrence = await LoadAsync(id, ct);
        if (occurrence.IsClosed)
            throw new BusinessRuleException("Ocorrências resolvidas ou canceladas não podem ser alteradas. Registre uma nova ocorrência se o problema voltou.");
        if (request.OccurredAt is { } occurredAt) occurrence.OccurredAt = Min(occurredAt, clock.UtcNow);
        await ApplyAsync(request, occurrence, ct);
        await files.AttachAsync(request.FileIds, FileOwnerType.Occurrence, occurrence.Id, "fileIds", ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OccurrenceResponse> ChangeStatusAsync(Guid id, OccurrenceStatusRequest request, CancellationToken ct)
    {
        await statusValidator.ValidateAndThrowAsync(request, ct);
        var occurrence = await LoadAsync(id, ct);
        var to = request.Status!.Value;
        if (!OccurrenceWorkflow.CanTransition(occurrence.Status, to))
            throw new BusinessRuleException(occurrence.IsClosed
                ? "Esta ocorrência já foi encerrada e não pode mudar de situação."
                : "Esta mudança de situação não é permitida.");

        var from = occurrence.Status;
        occurrence.Status = to;
        if (OccurrenceWorkflow.IsClosed(to))
        {
            occurrence.Resolution = request.Resolution!.Trim();
            occurrence.ClosedAt = clock.UtcNow;
            occurrence.ClosedBy = currentUser.UserId;
        }

        events.Record(OperationalEventType.OccurrenceStatusChanged, Subject(occurrence),
            $"Ocorrência \"{Short(occurrence.Description)}\": {StatusText(from)} → {StatusText(to)}.",
            new { from, to });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Open + InAnalysis: still needs attention (dashboard, vehicle alerts).</summary>
    public static IQueryable<Occurrence> Open(IQueryable<Occurrence> query) =>
        query.Where(o => o.Status == OccurrenceStatus.Open || o.Status == OccurrenceStatus.InAnalysis);

    /// <summary>Used by the checklist: adds the occurrence and its event to the caller's unit of work.</summary>
    internal async Task RecordCreatedAsync(Occurrence occurrence, CancellationToken ct)
    {
        var plate = occurrence.VehicleId is { } vehicleId
            ? await db.Vehicles.Where(v => v.Id == vehicleId).Select(v => v.LicensePlate).SingleOrDefaultAsync(ct)
            : null;
        var where = plate is null ? string.Empty : $" no veículo {LicensePlate.Format(plate)}";
        events.Record(OperationalEventType.OccurrenceCreated, Subject(occurrence),
            $"Ocorrência registrada{where} (gravidade {SeverityLabels[(int)occurrence.Severity]}): {Short(occurrence.Description)}",
            new { occurrence.Type, occurrence.Severity, occurrence.Source, occurrence.ChecklistExecutionId });
    }

    private async Task ApplyAsync(OccurrenceRequest request, Occurrence occurrence, CancellationToken ct)
    {
        if (request.VehicleId is { } vehicleId && !await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw ValidationErrors.ForField("vehicleId", "Veículo não encontrado. Selecione um veículo da lista.");
        if (request.DriverId is { } driverId && !await db.Drivers.AnyAsync(d => d.Id == driverId, ct))
            throw ValidationErrors.ForField("driverId", "Motorista não encontrado. Selecione um motorista da lista.");
        if (request.ImplementId is { } implementId && !await db.Implements.AnyAsync(i => i.Id == implementId, ct))
            throw ValidationErrors.ForField("implementId", "Implemento não encontrado. Selecione um implemento da lista.");

        occurrence.VehicleId = request.VehicleId;
        occurrence.DriverId = request.DriverId;
        occurrence.ImplementId = request.ImplementId;
        occurrence.Type = request.Type!.Value;
        occurrence.Severity = request.Severity!.Value;
        occurrence.Location = request.Location.TrimToNull();
        occurrence.Description = request.Description!.Trim();
    }

    private async Task<Occurrence> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Occurrences.Include(o => o.Vehicle).Include(o => o.Driver).Include(o => o.Implement)
            .SingleOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new NotFoundException("Ocorrência não encontrada.");

    private async Task<OccurrenceResponse> ToResponseAsync(Occurrence o, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, [o.CreatedBy, o.ClosedBy], ct);
        var attachments = (await files.ListByOwnerAsync(FileOwnerType.Occurrence, o.Id, ct)).ToList();
        // Photos taken on the failed checklist item are the evidence of an occurrence it opened.
        var answerIds = await db.ChecklistExecutions.Where(e => e.Id == o.ChecklistExecutionId)
            .SelectMany(e => e.Answers).Where(a => a.OccurrenceId == o.Id).Select(a => a.Id).ToListAsync(ct);
        foreach (var photos in (await files.ListByOwnersAsync(FileOwnerType.ChecklistAnswer, answerIds, ct)).Values)
            attachments.AddRange(photos);

        return new OccurrenceResponse(
            o.Id, o.Type, o.Severity, o.Status, OccurrenceWorkflow.NextStatuses(o.Status), o.OccurredAt, o.Location, o.Description,
            o.VehicleId, o.Vehicle?.LicensePlate, o.DriverId, o.Driver?.FullName, o.ImplementId, o.Implement?.LicensePlate,
            o.Resolution, o.ClosedAt, names.Get(o.ClosedBy), o.Source, o.ChecklistExecutionId, attachments,
            names.Get(o.CreatedBy), o.CreatedAt, o.UpdatedAt);
    }

    private static EventSubject Subject(Occurrence o) => new(nameof(Occurrence), o.Id, o.VehicleId, o.DriverId, o.ImplementId);

    private static string StatusText(OccurrenceStatus status) => status switch
    {
        OccurrenceStatus.Open => "aberta",
        OccurrenceStatus.InAnalysis => "em análise",
        OccurrenceStatus.Resolved => "resolvida",
        _ => "cancelada",
    };

    private static string Short(string text) => text.Length <= 80 ? text : text[..79] + "…";

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
