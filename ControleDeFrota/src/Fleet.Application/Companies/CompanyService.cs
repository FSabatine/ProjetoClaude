using Fleet.Application.Common;
using Fleet.Application.Documents;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;
using Fleet.Domain.Users;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Companies;

/// <summary>
/// Platform admins (companies.manage) see every company; everyone else only their own (ADR-003).
/// </summary>
public sealed class CompanyService(IFleetDbContext db, ICurrentUser currentUser, IValidator<CompanyRequest> validator)
{
    private static readonly SortMap<Company> Sorts = new SortMap<Company>("legalName")
        .Add("legalName", c => c.LegalName)
        .Add("tradeName", c => c.TradeName)
        .Add("cnpj", c => c.Cnpj)
        .Add("city", c => c.Address.City)
        .Add("createdAt", c => c.CreatedAt);

    private bool IsPlatformAdmin => currentUser.HasPermission(Permissions.Companies.Manage);
    private Guid OwnCompanyId => currentUser.CompanyId!.Value;

    public async Task<PagedResult<CompanyListItemResponse>> ListAsync(CompanyListRequest request, CancellationToken ct)
    {
        var query = db.Companies.AsQueryable();
        if (request.IsActive is { } isActive) query = query.Where(c => c.IsActive == isActive);
        if (request.SearchTerm is { } term)
        {
            var document = Cnpj.Normalize(term);
            query = query.Where(c => c.LegalName.Contains(term) || (c.TradeName != null && c.TradeName.Contains(term)) ||
                                     (document.Length > 0 && c.Cnpj.Contains(document)));
        }

        return await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, c =>
            new CompanyListItemResponse(
                c.Id, c.LegalName, c.TradeName, c.Cnpj, c.Address.City, c.Address.State, c.IsActive,
                db.Users.Count(u => u.CompanyId == c.Id)), ct);
    }

    public async Task<CompanyResponse> GetAsync(Guid id, CancellationToken ct)
    {
        EnsureCanAccess(id);
        return ToResponse(await LoadAsync(id, ct));
    }

    public Task<CompanyResponse> GetCurrentAsync(CancellationToken ct) => GetAsync(OwnCompanyId, ct);

    public async Task<CompanyResponse> CreateAsync(CompanyRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var company = new Company();
        await ApplyAsync(request, company, ct);
        db.Companies.Add(company);
        db.DocumentTypes.AddRange(DocumentTypeService.CreateDefaults(company.Id));
        await db.SaveChangesAsync(ct);
        return ToResponse(company);
    }

    public async Task<CompanyResponse> UpdateAsync(Guid id, CompanyRequest request, CancellationToken ct)
    {
        EnsureCanAccess(id);
        await validator.ValidateAndThrowAsync(request, ct);
        var company = await LoadAsync(id, ct);

        if (id == OwnCompanyId && !request.IsActive)
            throw new BusinessRuleException("Você não pode inativar a sua própria empresa: todos os usuários dela perderiam o acesso, inclusive você.");

        await ApplyAsync(request, company, ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(company);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var company = await LoadAsync(id, ct);
        if (id == OwnCompanyId)
            throw new BusinessRuleException("Você não pode excluir a sua própria empresa.");
        if (await db.Users.AnyAsync(u => u.CompanyId == id && u.Status == UserStatus.Active, ct))
            throw new BusinessRuleException("Esta empresa ainda tem usuários ativos. Inative ou exclua os usuários antes, ou apenas inative a empresa.");

        db.Companies.Remove(company);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Returns 404 (not 403) for other companies, so their existence isn't revealed.</summary>
    private void EnsureCanAccess(Guid id)
    {
        if (!IsPlatformAdmin && id != OwnCompanyId) throw NotFound();
    }

    private async Task<Company> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Companies.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound();

    private static NotFoundException NotFound() => new("Empresa não encontrada. Ela pode ter sido excluída.");

    private async Task ApplyAsync(CompanyRequest request, Company company, CancellationToken ct)
    {
        var cnpj = Cnpj.Normalize(request.Cnpj);
        if (await db.Companies.AnyAsync(c => c.Cnpj == cnpj && c.Id != company.Id, ct))
            throw new ConflictException("Já existe uma empresa cadastrada com este CNPJ.", "cnpj");

        company.LegalName = request.LegalName!.Trim();
        company.TradeName = request.TradeName.TrimToNull();
        company.Cnpj = cnpj;
        company.StateRegistration = request.StateRegistration.TrimToNull()?.ToUpperInvariant();
        company.Email = string.IsNullOrWhiteSpace(request.Email) ? null : EmailAddress.Normalize(request.Email);
        company.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : Phone.Normalize(request.Phone);
        request.Address.ApplyTo(company.Address);
        company.IsActive = request.IsActive;
    }

    private static CompanyResponse ToResponse(Company c) => new(
        c.Id, c.LegalName, c.TradeName, c.Cnpj, c.StateRegistration, c.Email, c.Phone,
        AddressDto.From(c.Address), c.IsActive, c.CreatedAt, c.UpdatedAt);
}
