using Fleet.Application.Common;
using Fleet.Domain.Documents;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Documents;

public sealed record DocumentTypeRequest
{
    public string? Name { get; init; }
    public DocumentOwnerType? OwnerType { get; init; }
    public bool HasExpiration { get; init; } = true;
    public int AlertDaysBefore { get; init; } = DocumentType.DefaultAlertDaysBefore;
    public bool IsActive { get; init; } = true;
}

public sealed record DocumentTypeResponse(
    Guid Id, string Name, DocumentOwnerType OwnerType, bool HasExpiration, int AlertDaysBefore, bool IsActive, int DocumentCount);

public sealed class DocumentTypeRequestValidator : AbstractValidator<DocumentTypeRequest>
{
    public DocumentTypeRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(DocumentType.NameMaxLength);
        RuleFor(x => x.OwnerType).NotNull().WithMessage("Aplica-se a: campo obrigatório.").IsInEnum().WithMessage("Opção inválida.");
        RuleFor(x => x.AlertDaysBefore)
            .InclusiveBetween(0, DocumentType.MaxAlertDaysBefore)
            .WithMessage($"A antecedência do alerta deve estar entre 0 e {DocumentType.MaxAlertDaysBefore} dias.");
    }
}

/// <summary>Per-company catalog of document types (ADR-021).</summary>
public sealed class DocumentTypeService(IFleetDbContext db, ICurrentUser currentUser, IValidator<DocumentTypeRequest> validator)
{
    public async Task<IReadOnlyList<DocumentTypeResponse>> ListAsync(DocumentOwnerType? ownerType, bool includeInactive, CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);
        var query = db.DocumentTypes.AsQueryable();
        if (ownerType is { } owner) query = query.Where(t => t.OwnerType == owner);
        if (!includeInactive) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.OwnerType).ThenBy(t => t.Name)
            .Select(t => new DocumentTypeResponse(t.Id, t.Name, t.OwnerType, t.HasExpiration, t.AlertDaysBefore, t.IsActive,
                db.Documents.Count(d => d.DocumentTypeId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<DocumentTypeResponse> CreateAsync(DocumentTypeRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var type = new DocumentType { OwnerType = request.OwnerType!.Value };
        await ApplyAsync(request, type, ct);
        db.DocumentTypes.Add(type);
        await db.SaveChangesAsync(ct);
        return await GetAsync(type.Id, ct);
    }

    public async Task<DocumentTypeResponse> UpdateAsync(Guid id, DocumentTypeRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var type = await LoadAsync(id, ct);
        if (request.OwnerType != type.OwnerType && await db.Documents.AnyAsync(d => d.DocumentTypeId == id, ct))
            throw new BusinessRuleException("Este tipo já tem documentos cadastrados, então não é possível mudar a quem ele se aplica. Crie um novo tipo.");

        var rulesChanged = type.HasExpiration != request.HasExpiration || type.AlertDaysBefore != request.AlertDaysBefore;
        type.OwnerType = request.OwnerType!.Value;
        await ApplyAsync(request, type, ct);
        if (rulesChanged) await RecomputeAlertWindowsAsync(type, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var type = await LoadAsync(id, ct);
        if (await db.Documents.AnyAsync(d => d.DocumentTypeId == id, ct))
            throw new BusinessRuleException("Este tipo tem documentos cadastrados e não pode ser excluído. Inative-o para que não seja mais usado.");
        db.DocumentTypes.Remove(type);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Companies created before this module (or by the platform admin) get the default catalog the first time it is
    /// read, so no company ever starts with an empty list. Idempotent.
    /// </summary>
    public async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        // IgnoreQueryFilters so deleted types also count: a company that removed every type must not get them back.
        // The company is filtered explicitly because the tenant filter is ignored too.
        var companyId = currentUser.CompanyId;
        if (await db.DocumentTypes.IgnoreQueryFilters().AnyAsync(t => t.CompanyId == companyId, ct)) return;
        db.DocumentTypes.AddRange(CreateDefaults());
        await db.SaveChangesAsync(ct);
    }

    public static IEnumerable<DocumentType> CreateDefaults(Guid? companyId = null) => DocumentTypeDefaults.All.Select(d => new DocumentType
    {
        CompanyId = companyId ?? Guid.Empty,
        Name = d.Name,
        OwnerType = d.OwnerType,
        HasExpiration = d.HasExpiration,
        AlertDaysBefore = d.AlertDaysBefore,
    });

    private async Task ApplyAsync(DocumentTypeRequest request, DocumentType type, CancellationToken ct)
    {
        var name = request.Name!.Trim();
        if (await db.DocumentTypes.AnyAsync(t => t.Name == name && t.OwnerType == type.OwnerType && t.Id != type.Id, ct))
            throw new ConflictException("Já existe um tipo de documento com este nome para esta categoria.", "name");
        type.Name = name;
        type.HasExpiration = request.HasExpiration;
        type.AlertDaysBefore = request.AlertDaysBefore;
        type.IsActive = request.IsActive;
    }

    /// <summary>The alert window is stored per document; a new threshold must reach the existing documents.</summary>
    private async Task RecomputeAlertWindowsAsync(DocumentType type, CancellationToken ct)
    {
        var documents = await db.Documents.Where(d => d.DocumentTypeId == type.Id).ToListAsync(ct);
        foreach (var document in documents)
        {
            document.AlertStartsOn = DocumentExpiryPolicy.AlertStartsOn(document.ExpiresOn, type.HasExpiration, type.AlertDaysBefore);
            // Re-announce under the new rules (the scanner compares with the last alerted status).
            document.LastAlertedStatus = null;
        }
    }

    private async Task<DocumentTypeResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await db.DocumentTypes.Where(t => t.Id == id)
            .Select(t => new DocumentTypeResponse(t.Id, t.Name, t.OwnerType, t.HasExpiration, t.AlertDaysBefore, t.IsActive,
                db.Documents.Count(d => d.DocumentTypeId == t.Id)))
            .SingleAsync(ct));

    private async Task<DocumentType> LoadAsync(Guid id, CancellationToken ct) =>
        await db.DocumentTypes.SingleOrDefaultAsync(t => t.Id == id, ct)
        ?? throw new NotFoundException("Tipo de documento não encontrado.");
}
