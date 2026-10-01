using Fleet.Application.Common;
using Fleet.Domain.Common;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Maintenance;

public sealed class WorkshopListRequest : ListRequest
{
    public WorkshopStatus? Status { get; set; }
}

public sealed record WorkshopRequest
{
    public string? Name { get; init; }
    public string? Document { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public AddressDto Address { get; init; } = new();
    public string? Specialties { get; init; }
    public WorkshopStatus Status { get; init; } = WorkshopStatus.Active;
    public string? Notes { get; init; }
}

public sealed record WorkshopResponse(
    Guid Id, string Name, string? Document, string? Phone, string? Email, AddressDto Address,
    string? Specialties, WorkshopStatus Status, string? Notes, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed class WorkshopRequestValidator : AbstractValidator<WorkshopRequest>
{
    public WorkshopRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(Workshop.NameMaxLength);
        RuleFor(x => x.Phone).ValidPhone().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email).ValidEmail().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Document)
            .Must(d => Cpf.IsValid(d) || Cnpj.IsValid(d))
            .When(x => !string.IsNullOrWhiteSpace(x.Document))
            .WithMessage("Documento inválido. Informe um CPF ou CNPJ válido.");
        RuleFor(x => x.Specialties).MaxLen(Workshop.SpecialtiesMaxLength);
        RuleFor(x => x.Notes).MaxLen(Workshop.NotesMaxLength);
        RuleFor(x => x.Address).NotNull().SetValidator(new AddressValidator(required: false));
    }
}

/// <summary>Workshop registry (seção 23) — internal or external, used by WorkOrder.WorkshopId.</summary>
public sealed class WorkshopService(IFleetDbContext db, IValidator<WorkshopRequest> validator)
{
    private static readonly SortMap<Workshop> Sorts = new SortMap<Workshop>("name")
        .Add("name", w => w.Name)
        .Add("status", w => w.Status)
        .Add("createdAt", w => w.CreatedAt);

    public async Task<PagedResult<WorkshopResponse>> ListAsync(WorkshopListRequest request, CancellationToken ct)
    {
        var query = db.Workshops.AsQueryable();
        if (request.Status is { } status) query = query.Where(w => w.Status == status);
        if (request.SearchTerm is { } term) query = query.Where(w => w.Name.Contains(term));

        return await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, w => new WorkshopResponse(
            w.Id, w.Name, w.Document, w.Phone, w.Email, AddressDto.From(w.Address), w.Specialties, w.Status, w.Notes, w.CreatedAt, w.UpdatedAt), ct);
    }

    public async Task<WorkshopResponse> GetAsync(Guid id, CancellationToken ct) => ToResponse(await LoadAsync(id, ct));

    public async Task<WorkshopResponse> CreateAsync(WorkshopRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var workshop = new Workshop();
        Apply(request, workshop);
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync(ct);
        return ToResponse(workshop);
    }

    public async Task<WorkshopResponse> UpdateAsync(Guid id, WorkshopRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var workshop = await LoadAsync(id, ct);
        Apply(request, workshop);
        await db.SaveChangesAsync(ct);
        return ToResponse(workshop);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var workshop = await LoadAsync(id, ct);
        if (await db.WorkOrders.AnyAsync(w => w.WorkshopId == id, ct))
            throw new BusinessRuleException("Esta oficina tem ordens de serviço registradas e não pode ser excluída. Inative-a.");
        db.Workshops.Remove(workshop);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Workshop> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Workshops.SingleOrDefaultAsync(w => w.Id == id, ct)
        ?? throw new NotFoundException("Oficina não encontrada. Ela pode ter sido excluída.");

    private static void Apply(WorkshopRequest request, Workshop workshop)
    {
        workshop.Name = request.Name!.Trim();
        workshop.Document = Digits(request.Document);
        workshop.Phone = Digits(request.Phone);
        workshop.Email = request.Email.TrimToNull()?.ToLowerInvariant();
        request.Address.ApplyTo(workshop.Address);
        workshop.Specialties = request.Specialties.TrimToNull();
        workshop.Status = request.Status;
        workshop.Notes = request.Notes.TrimToNull();
    }

    private static string? Digits(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new string(value.Where(char.IsAsciiDigit).ToArray());

    private static WorkshopResponse ToResponse(Workshop w) => new(
        w.Id, w.Name, w.Document, w.Phone, w.Email, AddressDto.From(w.Address),
        w.Specialties, w.Status, w.Notes, w.CreatedAt, w.UpdatedAt);
}
