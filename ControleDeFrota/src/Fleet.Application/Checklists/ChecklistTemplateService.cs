using Fleet.Application.Common;
using Fleet.Domain.Checklists;
using Fleet.Domain.Occurrences;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Checklists;

public sealed record ChecklistTemplateItemRequest
{
    /// <summary>Existing item id to keep it stable across edits; null for a new item.</summary>
    public Guid? Id { get; init; }
    public string? Section { get; init; }
    public string? Label { get; init; }
    public ChecklistResponseType ResponseType { get; init; } = ChecklistResponseType.PassFail;
    public bool IsRequired { get; init; } = true;
    public string? Unit { get; init; }
    public bool RequiresPhotoOnFail { get; init; }
    public OccurrenceType FailureOccurrenceType { get; init; } = OccurrenceType.MechanicalIssue;
    public OccurrenceSeverity FailureSeverity { get; init; } = OccurrenceSeverity.Medium;
}

public sealed record ChecklistTemplateRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public ChecklistFrequency Frequency { get; init; } = ChecklistFrequency.OnDemand;
    public bool IsActive { get; init; } = true;
    public IReadOnlyList<ChecklistTemplateItemRequest> Items { get; init; } = [];
}

public sealed record ChecklistTemplateItemResponse(
    Guid Id, int Position, string? Section, string Label, ChecklistResponseType ResponseType, bool IsRequired, string? Unit,
    bool RequiresPhotoOnFail, OccurrenceType FailureOccurrenceType, OccurrenceSeverity FailureSeverity);

public sealed record ChecklistTemplateListItemResponse(
    Guid Id, string Name, string? Description, ChecklistFrequency Frequency, bool IsActive, int Version, int ItemCount, int ExecutionCount);

public sealed record ChecklistTemplateResponse(
    Guid Id, string Name, string? Description, ChecklistFrequency Frequency, bool IsActive, int Version,
    IReadOnlyList<ChecklistTemplateItemResponse> Items, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed class ChecklistTemplateRequestValidator : AbstractValidator<ChecklistTemplateRequest>
{
    public ChecklistTemplateRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(ChecklistTemplate.NameMaxLength);
        RuleFor(x => x.Description).MaxLen(ChecklistTemplate.DescriptionMaxLength);
        RuleFor(x => x.Frequency).IsInEnum().WithMessage("Frequência inválida.");
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Inclua pelo menos um item no checklist.")
            .Must(items => items.Count <= ChecklistTemplate.MaxItems).WithMessage($"Use no máximo {ChecklistTemplate.MaxItems} itens.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Label).Required("Pergunta").MaxLen(ChecklistTemplateItem.LabelMaxLength);
            item.RuleFor(i => i.Section).MaxLen(ChecklistTemplateItem.SectionMaxLength);
            item.RuleFor(i => i.Unit).MaxLen(ChecklistTemplateItem.UnitMaxLength);
            item.RuleFor(i => i.ResponseType).IsInEnum().WithMessage("Tipo de resposta inválido.");
            item.RuleFor(i => i.FailureOccurrenceType).IsInEnum().WithMessage("Tipo de ocorrência inválido.");
            item.RuleFor(i => i.FailureSeverity).IsInEnum().WithMessage("Gravidade inválida.");
        });
    }
}

/// <summary>Configurable, versioned inspection models (ADR-024).</summary>
public sealed class ChecklistTemplateService(IFleetDbContext db, IValidator<ChecklistTemplateRequest> validator)
{
    public async Task<IReadOnlyList<ChecklistTemplateListItemResponse>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.ChecklistTemplates.AsQueryable();
        if (!includeInactive) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.Name)
            .Select(t => new ChecklistTemplateListItemResponse(t.Id, t.Name, t.Description, t.Frequency, t.IsActive, t.Version,
                t.Items.Count, db.ChecklistExecutions.Count(e => e.TemplateId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<ChecklistTemplateResponse> GetAsync(Guid id, CancellationToken ct) => ToResponse(await LoadAsync(id, ct));

    public async Task<ChecklistTemplateResponse> CreateAsync(ChecklistTemplateRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var template = new ChecklistTemplate();
        await ApplyAsync(request, template, ct);
        template.Items = request.Items.Select((item, index) => NewItem(item, index)).ToList();
        db.ChecklistTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return ToResponse(template);
    }

    public async Task<ChecklistTemplateResponse> UpdateAsync(Guid id, ChecklistTemplateRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var template = await LoadAsync(id, ct);
        await ApplyAsync(request, template, ct);

        var before = Signature(template.Items);
        ReplaceItems(template, request.Items);
        // Executions keep their own snapshot; the version tells which model they answered.
        if (Signature(template.Items) != before) template.Version++;

        await db.SaveChangesAsync(ct);
        return ToResponse(template);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var template = await LoadAsync(id, ct);
        db.ChecklistTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(ChecklistTemplateRequest request, ChecklistTemplate template, CancellationToken ct)
    {
        var name = request.Name!.Trim();
        if (await db.ChecklistTemplates.AnyAsync(t => t.Name == name && t.Id != template.Id, ct))
            throw new ConflictException("Já existe um modelo de checklist com este nome.", "name");
        template.Name = name;
        template.Description = request.Description.TrimToNull();
        template.Frequency = request.Frequency;
        template.IsActive = request.IsActive;
    }

    private void ReplaceItems(ChecklistTemplate template, IReadOnlyList<ChecklistTemplateItemRequest> items)
    {
        var existing = template.Items.ToDictionary(i => i.Id);
        var kept = new List<ChecklistTemplateItem>();
        for (var index = 0; index < items.Count; index++)
        {
            var request = items[index];
            if (request.Id is { } itemId && existing.Remove(itemId, out var item))
            {
                Fill(item, request, index);
                kept.Add(item);
            }
            else
            {
                kept.Add(NewItem(request, index));
            }
        }
        foreach (var removed in existing.Values) template.Items.Remove(removed);
        foreach (var added in kept.Where(i => !template.Items.Contains(i))) template.Items.Add(added);
        template.Items.Sort((a, b) => a.Position.CompareTo(b.Position));
    }

    private static ChecklistTemplateItem NewItem(ChecklistTemplateItemRequest request, int index)
    {
        var item = new ChecklistTemplateItem();
        Fill(item, request, index);
        return item;
    }

    private static void Fill(ChecklistTemplateItem item, ChecklistTemplateItemRequest request, int index)
    {
        item.Position = index + 1;
        item.Section = request.Section.TrimToNull();
        item.Label = request.Label!.Trim();
        item.ResponseType = request.ResponseType;
        item.IsRequired = request.IsRequired;
        item.Unit = request.ResponseType == ChecklistResponseType.Number ? request.Unit.TrimToNull() : null;
        // Only a pass/fail item can fail and open an occurrence.
        var canFail = request.ResponseType == ChecklistResponseType.PassFail;
        item.RequiresPhotoOnFail = canFail && request.RequiresPhotoOnFail;
        item.FailureOccurrenceType = request.FailureOccurrenceType;
        item.FailureSeverity = request.FailureSeverity;
    }

    private static string Signature(IEnumerable<ChecklistTemplateItem> items) => string.Join("\n", items.OrderBy(i => i.Position).Select(i =>
        $"{i.Position}|{i.Section}|{i.Label}|{i.ResponseType}|{i.IsRequired}|{i.Unit}|{i.RequiresPhotoOnFail}|{i.FailureOccurrenceType}|{i.FailureSeverity}"));

    private async Task<ChecklistTemplate> LoadAsync(Guid id, CancellationToken ct) =>
        await db.ChecklistTemplates.Include(t => t.Items).SingleOrDefaultAsync(t => t.Id == id, ct)
        ?? throw new NotFoundException("Modelo de checklist não encontrado.");

    public static ChecklistTemplateResponse ToResponse(ChecklistTemplate t) => new(
        t.Id, t.Name, t.Description, t.Frequency, t.IsActive, t.Version,
        t.Items.OrderBy(i => i.Position).Select(i => new ChecklistTemplateItemResponse(
            i.Id, i.Position, i.Section, i.Label, i.ResponseType, i.IsRequired, i.Unit, i.RequiresPhotoOnFail,
            i.FailureOccurrenceType, i.FailureSeverity)).ToList(),
        t.CreatedAt, t.UpdatedAt);
}
