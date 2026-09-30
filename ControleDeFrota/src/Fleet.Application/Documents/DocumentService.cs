using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Application.Operations;
using Fleet.Domain.Common;
using Fleet.Domain.Documents;
using Fleet.Domain.Files;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Documents;

public sealed class DocumentListRequest : ListRequest
{
    public DocumentOwnerType? OwnerType { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? ImplementId { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public DocumentStatus? Status { get; set; }
    /// <summary>Shortcut for the alert views: ExpiringSoon or Expired.</summary>
    public bool? AlertsOnly { get; set; }
    public DateOnly? ExpiresFrom { get; set; }
    public DateOnly? ExpiresTo { get; set; }
    /// <summary>Replaced (renewed) documents are history; hidden unless asked for.</summary>
    public bool IncludeReplaced { get; set; }
}

/// <summary>Fields shared by create and update, validated once by <see cref="DocumentDatesValidator"/>.</summary>
public interface IDocumentFields
{
    string? Number { get; }
    DateOnly? IssuedOn { get; }
    DateOnly? ExpiresOn { get; }
    string? Notes { get; }
}

public sealed record DocumentCreateRequest : IDocumentFields
{
    public Guid? DocumentTypeId { get; init; }
    /// <summary>Vehicle, driver or implement id. Ignored for company documents (the owner is the caller's company).</summary>
    public Guid? OwnerId { get; init; }
    public string? Number { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
    /// <summary>Renewal: the document this one replaces (same type and owner).</summary>
    public Guid? ReplacesDocumentId { get; init; }
}

public sealed record DocumentUpdateRequest : IDocumentFields
{
    public string? Number { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public string? Notes { get; init; }
    /// <summary>New attachments (existing ones are removed through /files/{id}).</summary>
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public sealed record DocumentListItemResponse(
    Guid Id,
    Guid DocumentTypeId,
    string DocumentTypeName,
    DocumentOwnerType OwnerType,
    Guid? OwnerId,
    string? OwnerName,
    string? Number,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    DocumentStatus Status,
    int? DaysUntilExpiration,
    int FileCount);

public sealed record DocumentResponse(
    Guid Id,
    Guid DocumentTypeId,
    string DocumentTypeName,
    bool TypeHasExpiration,
    DocumentOwnerType OwnerType,
    Guid? OwnerId,
    string? OwnerName,
    string? Number,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    DocumentStatus Status,
    int? DaysUntilExpiration,
    string? Notes,
    DateTime? ReplacedAt,
    Guid? ReplacedByDocumentId,
    IReadOnlyList<FileResponse> Files,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class DocumentListRequestValidator : AbstractValidator<DocumentListRequest>
{
    public DocumentListRequestValidator() => this.ValidPeriod(x => x.ExpiresFrom, x => x.ExpiresTo, "expiresTo");
}

public sealed class DocumentCreateRequestValidator : AbstractValidator<DocumentCreateRequest>
{
    public DocumentCreateRequestValidator(IClock clock)
    {
        Include(new DocumentDatesValidator(clock));
        RuleFor(x => x.DocumentTypeId).NotNull().WithMessage("Tipo de documento: campo obrigatório.");
    }
}

public sealed class DocumentUpdateRequestValidator : AbstractValidator<DocumentUpdateRequest>
{
    public DocumentUpdateRequestValidator(IClock clock) => Include(new DocumentDatesValidator(clock));
}

internal sealed class DocumentDatesValidator : AbstractValidator<IDocumentFields>
{
    public const int MaxYearsAhead = 30;

    public DocumentDatesValidator(IClock clock)
    {
        RuleFor(x => x.IssuedOn)
            .Must(d => d is null || (d.Value.Year >= 1950 && d.Value <= clock.Today))
            .WithMessage("A data de emissão não pode ser futura.");
        RuleFor(x => x.ExpiresOn)
            .Must((x, d) => d is null || x.IssuedOn is not { } issued || d.Value >= issued)
            .WithMessage("O vencimento deve ser igual ou posterior à data de emissão.")
            .Must(d => d is null || (d.Value.Year >= 1950 && d.Value <= clock.Today.AddYears(MaxYearsAhead)))
            .WithMessage("Data de vencimento inválida. Confira a data impressa no documento.");
        RuleFor(x => x.Number).MaxLen(Document.NumberMaxLength);
        RuleFor(x => x.Notes).MaxLen(Document.NotesMaxLength);
    }
}

/// <summary>
/// Status filters as SQL — the same rules as <see cref="DocumentExpiryPolicy.Evaluate(Document, DateOnly)"/>,
/// using the stored AlertStartsOn (null ⇔ no expiration). Tests assert both agree.
/// </summary>
public static class DocumentQueries
{
    public static IQueryable<Document> WhereStatus(this IQueryable<Document> query, DocumentStatus status, DateOnly today) => status switch
    {
        DocumentStatus.Replaced => query.Where(d => d.ReplacedAt != null),
        DocumentStatus.NoExpiration => query.Where(d => d.ReplacedAt == null && (d.AlertStartsOn == null || d.ExpiresOn == null)),
        DocumentStatus.Expired => query.Where(d => d.ReplacedAt == null && d.AlertStartsOn != null && d.ExpiresOn < today),
        DocumentStatus.ExpiringSoon => query.Where(d => d.ReplacedAt == null && d.AlertStartsOn <= today && d.ExpiresOn >= today),
        _ => query.Where(d => d.ReplacedAt == null && d.AlertStartsOn > today),
    };

    /// <summary>Expiring soon or expired (not replaced).</summary>
    public static IQueryable<Document> WhereAlert(this IQueryable<Document> query, DateOnly today) =>
        query.Where(d => d.ReplacedAt == null && d.AlertStartsOn != null && d.AlertStartsOn <= today);
}

/// <summary>Fleet documents owned by vehicles, drivers, implements or the company (ADR-021).</summary>
public sealed class DocumentService(
    IFleetDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    FileService files,
    OperationalEventLog events,
    IValidator<DocumentListRequest> listValidator,
    IValidator<DocumentCreateRequest> createValidator,
    IValidator<DocumentUpdateRequest> updateValidator)
{
    private static readonly SortMap<Document> Sorts = new SortMap<Document>("expiresOn")
        // Documents without expiration go last when sorting by due date.
        .Add("expiresOn", d => d.ExpiresOn ?? DateOnly.MaxValue)
        .Add("type", d => d.DocumentType.Name)
        .Add("issuedOn", d => d.IssuedOn ?? DateOnly.MinValue)
        .Add("createdAt", d => d.CreatedAt);

    public async Task<PagedResult<DocumentListItemResponse>> ListAsync(DocumentListRequest request, CancellationToken ct)
    {
        await listValidator.ValidateAndThrowAsync(request, ct);
        var today = clock.Today;
        var query = db.Documents.AsQueryable();
        if (request.OwnerType is { } ownerType) query = query.Where(d => d.OwnerType == ownerType);
        if (request.VehicleId is { } vehicleId) query = query.Where(d => d.VehicleId == vehicleId);
        if (request.DriverId is { } driverId) query = query.Where(d => d.DriverId == driverId);
        if (request.ImplementId is { } implementId) query = query.Where(d => d.ImplementId == implementId);
        if (request.DocumentTypeId is { } typeId) query = query.Where(d => d.DocumentTypeId == typeId);
        if (request.Status is { } status) query = query.WhereStatus(status, today);
        else if (!request.IncludeReplaced) query = query.Where(d => d.ReplacedAt == null);
        if (request.AlertsOnly == true) query = query.WhereAlert(today);
        if (request.ExpiresFrom is { } from) query = query.Where(d => d.ExpiresOn >= from);
        if (request.ExpiresTo is { } to) query = query.Where(d => d.ExpiresOn <= to);
        if (request.SearchTerm is { } term)
        {
            var identifier = LicensePlate.Normalize(term);
            query = query.Where(d => (d.Number != null && d.Number.Contains(term)) || d.DocumentType.Name.Contains(term) ||
                                     (d.Vehicle != null && d.Vehicle.LicensePlate.Contains(identifier)) ||
                                     (d.Implement != null && d.Implement.LicensePlate.Contains(identifier)) ||
                                     (d.Driver != null && d.Driver.FullName.Contains(term)));
        }

        var companyName = await CompanyNameAsync(ct);
        var page = await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, d => new
        {
            d.Id, d.DocumentTypeId, TypeName = d.DocumentType.Name, d.OwnerType, d.VehicleId, d.DriverId, d.ImplementId,
            VehiclePlate = d.Vehicle != null ? d.Vehicle.LicensePlate : null,
            ImplementPlate = d.Implement != null ? d.Implement.LicensePlate : null,
            DriverName = d.Driver != null ? d.Driver.FullName : null,
            d.Number, d.IssuedOn, d.ExpiresOn, d.AlertStartsOn, d.ReplacedAt,
            FileCount = db.StoredFiles.Count(f => f.OwnerType == FileOwnerType.Document && f.OwnerId == d.Id),
        }, ct);

        var items = page.Items.Select(d => new DocumentListItemResponse(
            d.Id, d.DocumentTypeId, d.TypeName, d.OwnerType,
            OwnerId(d.OwnerType, d.VehicleId, d.DriverId, d.ImplementId),
            OwnerName(d.OwnerType, d.VehiclePlate, d.DriverName, d.ImplementPlate, companyName),
            d.Number, d.IssuedOn, d.ExpiresOn,
            DocumentExpiryPolicy.Evaluate(d.ExpiresOn, d.AlertStartsOn, today, d.ReplacedAt is not null),
            DocumentExpiryPolicy.DaysUntilExpiration(d.ExpiresOn, today), d.FileCount)).ToList();
        return new PagedResult<DocumentListItemResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<DocumentResponse> GetAsync(Guid id, CancellationToken ct) => await ToResponseAsync(await LoadAsync(id, ct), ct);

    public async Task<DocumentResponse> CreateAsync(DocumentCreateRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var type = await db.DocumentTypes.SingleOrDefaultAsync(t => t.Id == request.DocumentTypeId, ct)
            ?? throw ValidationErrors.ForField("documentTypeId", "Tipo de documento não encontrado. Selecione um tipo da lista.");
        if (!type.IsActive)
            throw ValidationErrors.ForField("documentTypeId", "Este tipo de documento está inativo. Selecione outro tipo.");

        var document = new Document { DocumentTypeId = type.Id, DocumentType = type, OwnerType = type.OwnerType };
        await SetOwnerAsync(document, request.OwnerId, ct);
        ApplyDates(document, type, request.IssuedOn, request.ExpiresOn);
        document.Number = request.Number.TrimToNull();
        document.Notes = request.Notes.TrimToNull();

        var replaced = request.ReplacesDocumentId is { } replacesId ? await LoadReplaceableAsync(replacesId, document, ct) : null;
        db.Documents.Add(document);
        await files.AttachAsync(request.FileIds, FileOwnerType.Document, document.Id, "fileIds", ct);

        var description = await DescribeAsync(document, ct);
        if (replaced is not null)
        {
            replaced.ReplacedAt = clock.UtcNow;
            replaced.ReplacedByDocumentId = document.Id;
            events.Record(OperationalEventType.DocumentRenewed, Subject(document),
                $"{description} renovado(a){ExpiryText(document)}.", new { replacedDocumentId = replaced.Id, document.ExpiresOn });
        }
        else
        {
            events.Record(OperationalEventType.DocumentCreated, Subject(document),
                $"{description} cadastrado(a){ExpiryText(document)}.", new { document.DocumentTypeId, document.ExpiresOn });
        }

        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(document, ct);
    }

    public async Task<DocumentResponse> UpdateAsync(Guid id, DocumentUpdateRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var document = await LoadAsync(id, ct);
        if (document.ReplacedAt is not null)
            throw new BusinessRuleException("Este documento foi substituído por uma renovação e não pode mais ser alterado.");

        var datesChanged = document.ExpiresOn != request.ExpiresOn;
        ApplyDates(document, document.DocumentType, request.IssuedOn, request.ExpiresOn);
        document.Number = request.Number.TrimToNull();
        document.Notes = request.Notes.TrimToNull();
        // A new due date must be announced again by the expiration scanner.
        if (datesChanged) document.LastAlertedStatus = null;
        await files.AttachAsync(request.FileIds, FileOwnerType.Document, document.Id, "fileIds", ct);
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(document, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var document = await LoadAsync(id, ct);
        var description = await DescribeAsync(document, ct);
        db.Documents.Remove(document);
        events.Record(OperationalEventType.DocumentDeleted, Subject(document), $"{description} excluído(a).",
            new { document.DocumentTypeId });
        await db.SaveChangesAsync(ct);
    }

    private async Task SetOwnerAsync(Document document, Guid? ownerId, CancellationToken ct)
    {
        const string field = "ownerId";
        switch (document.OwnerType)
        {
            case DocumentOwnerType.Vehicle:
                document.VehicleId = await db.Vehicles.Where(v => v.Id == ownerId).Select(v => (Guid?)v.Id).SingleOrDefaultAsync(ct)
                    ?? throw ValidationErrors.ForField(field, "Veículo não encontrado. Selecione um veículo da lista.");
                break;
            case DocumentOwnerType.Driver:
                document.DriverId = await db.Drivers.Where(d => d.Id == ownerId).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct)
                    ?? throw ValidationErrors.ForField(field, "Motorista não encontrado. Selecione um motorista da lista.");
                break;
            case DocumentOwnerType.Implement:
                document.ImplementId = await db.Implements.Where(i => i.Id == ownerId).Select(i => (Guid?)i.Id).SingleOrDefaultAsync(ct)
                    ?? throw ValidationErrors.ForField(field, "Implemento não encontrado. Selecione um implemento da lista.");
                break;
        }
    }

    private void ApplyDates(Document document, DocumentType type, DateOnly? issuedOn, DateOnly? expiresOn)
    {
        if (type.HasExpiration && expiresOn is null)
            throw ValidationErrors.ForField("expiresOn", $"Informe o vencimento: \"{type.Name}\" é um documento com validade.");
        if (!type.HasExpiration && expiresOn is not null)
            throw ValidationErrors.ForField("expiresOn", $"\"{type.Name}\" não tem validade. Deixe o vencimento em branco.");

        document.IssuedOn = issuedOn;
        document.ExpiresOn = expiresOn;
        document.AlertStartsOn = DocumentExpiryPolicy.AlertStartsOn(expiresOn, type.HasExpiration, type.AlertDaysBefore);
    }

    private async Task<Document> LoadReplaceableAsync(Guid id, Document renewal, CancellationToken ct)
    {
        var old = await db.Documents.SingleOrDefaultAsync(d => d.Id == id, ct)
            ?? throw ValidationErrors.ForField("replacesDocumentId", "O documento a ser renovado não foi encontrado.");
        // Compare the owner FKs (company documents have none: the tenant filter already guarantees the same company).
        var sameOwner = old.VehicleId == renewal.VehicleId && old.DriverId == renewal.DriverId && old.ImplementId == renewal.ImplementId;
        if (old.DocumentTypeId != renewal.DocumentTypeId || !sameOwner)
            throw ValidationErrors.ForField("replacesDocumentId", "A renovação deve ser do mesmo tipo e do mesmo dono do documento original.");
        if (old.ReplacedAt is not null)
            throw new BusinessRuleException("Este documento já foi renovado. Renove o documento mais recente.");
        return old;
    }

    private async Task<Document> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Documents.Include(d => d.DocumentType).SingleOrDefaultAsync(d => d.Id == id, ct)
        ?? throw new NotFoundException("Documento não encontrado. Ele pode ter sido excluído.");

    private async Task<DocumentResponse> ToResponseAsync(Document d, CancellationToken ct)
    {
        var today = clock.Today;
        var ownerName = await OwnerNameAsync(d, ct);
        return new DocumentResponse(
            d.Id, d.DocumentTypeId, d.DocumentType.Name, d.DocumentType.HasExpiration, d.OwnerType, d.OwnerId, ownerName,
            d.Number, d.IssuedOn, d.ExpiresOn, DocumentExpiryPolicy.Evaluate(d, today), DocumentExpiryPolicy.DaysUntilExpiration(d.ExpiresOn, today),
            d.Notes, d.ReplacedAt, d.ReplacedByDocumentId, await files.ListByOwnerAsync(FileOwnerType.Document, d.Id, ct),
            d.CreatedAt, d.UpdatedAt);
    }

    private async Task<string?> OwnerNameAsync(Document d, CancellationToken ct) => d.OwnerType switch
    {
        DocumentOwnerType.Vehicle => await db.Vehicles.Where(v => v.Id == d.VehicleId).Select(v => v.LicensePlate).SingleOrDefaultAsync(ct) is { } p
            ? LicensePlate.Format(p) : null,
        DocumentOwnerType.Implement => await db.Implements.Where(i => i.Id == d.ImplementId).Select(i => i.LicensePlate).SingleOrDefaultAsync(ct) is { } p
            ? LicensePlate.Format(p) : null,
        DocumentOwnerType.Driver => await db.Drivers.Where(x => x.Id == d.DriverId).Select(x => x.FullName).SingleOrDefaultAsync(ct),
        _ => await CompanyNameAsync(ct),
    };

    /// <summary>"Seguro do veículo ABC-1234", used in history summaries.</summary>
    private async Task<string> DescribeAsync(Document d, CancellationToken ct)
    {
        var owner = await OwnerNameAsync(d, ct);
        return d.OwnerType switch
        {
            DocumentOwnerType.Vehicle => $"{d.DocumentType.Name} do veículo {owner}",
            DocumentOwnerType.Implement => $"{d.DocumentType.Name} do implemento {owner}",
            DocumentOwnerType.Driver => $"{d.DocumentType.Name} de {owner}",
            _ => $"{d.DocumentType.Name} da empresa",
        };
    }

    private async Task<string?> CompanyNameAsync(CancellationToken ct) =>
        await db.Companies.Where(c => c.Id == currentUser.CompanyId).Select(c => c.TradeName ?? c.LegalName).SingleOrDefaultAsync(ct);

    private static string ExpiryText(Document d) =>
        d.ExpiresOn is { } date ? $", com vencimento em {BrazilianFormat.Date(date)}" : string.Empty;

    private static Guid? OwnerId(DocumentOwnerType type, Guid? vehicleId, Guid? driverId, Guid? implementId) => type switch
    {
        DocumentOwnerType.Vehicle => vehicleId,
        DocumentOwnerType.Driver => driverId,
        DocumentOwnerType.Implement => implementId,
        _ => null,
    };

    private static string? OwnerName(DocumentOwnerType type, string? plate, string? driver, string? implementPlate, string? company) => type switch
    {
        DocumentOwnerType.Vehicle => plate is null ? null : LicensePlate.Format(plate),
        DocumentOwnerType.Implement => implementPlate is null ? null : LicensePlate.Format(implementPlate),
        DocumentOwnerType.Driver => driver,
        _ => company,
    };

    public static EventSubject Subject(Document d) => new(nameof(Document), d.Id, d.VehicleId, d.DriverId, d.ImplementId);
}
