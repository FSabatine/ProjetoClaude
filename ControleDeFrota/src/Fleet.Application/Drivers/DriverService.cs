using Fleet.Application.Common;
using Fleet.Domain.Drivers;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Drivers;

/// <summary>Drivers are tenant-scoped: EF filters every query by the caller's company.</summary>
public sealed class DriverService(IFleetDbContext db, IClock clock, IValidator<DriverRequest> validator)
{
    private static readonly SortMap<Driver> Sorts = new SortMap<Driver>("fullName")
        .Add("fullName", d => d.FullName)
        .Add("cpf", d => d.Cpf)
        .Add("licenseCategory", d => d.LicenseCategory)
        .Add("licenseExpiresOn", d => d.LicenseExpiresOn)
        .Add("status", d => d.Status)
        .Add("createdAt", d => d.CreatedAt);

    public async Task<PagedResult<DriverListItemResponse>> ListAsync(DriverListRequest request, CancellationToken ct)
    {
        var today = clock.Today;
        var alertLimit = today.AddDays(Driver.LicenseExpiryAlertDays);
        var query = db.Drivers.AsQueryable();

        if (request.Status is { } status) query = query.Where(d => d.Status == status);
        query = request.LicenseAlert switch
        {
            LicenseAlertFilter.Expired => query.Where(d => d.LicenseExpiresOn < today),
            LicenseAlertFilter.ExpiringSoon => query.Where(d => d.LicenseExpiresOn >= today && d.LicenseExpiresOn <= alertLimit),
            _ => query,
        };
        if (request.SearchTerm is { } term)
        {
            var digits = Cpf.Normalize(term);
            var searchDocuments = digits.Length >= 3 && DocumentText.IsAllDigits(digits);
            query = query.Where(d => d.FullName.Contains(term) ||
                                     (searchDocuments && (d.Cpf.Contains(digits) || d.LicenseNumber.Contains(digits))));
        }

        return await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, d =>
            new DriverListItemResponse(
                d.Id, d.FullName, d.Cpf, d.Phone, d.LicenseCategory, d.LicenseExpiresOn,
                d.LicenseExpiresOn < today ? LicenseState.Expired
                    : d.LicenseExpiresOn <= alertLimit ? LicenseState.ExpiringSoon : LicenseState.Valid,
                d.Status), ct);
    }

    public async Task<DriverResponse> GetAsync(Guid id, CancellationToken ct) => ToResponse(await LoadAsync(id, ct));

    public async Task<DriverResponse> CreateAsync(DriverRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var driver = new Driver();
        await ApplyAsync(request, driver, ct);
        db.Drivers.Add(driver);
        await db.SaveChangesAsync(ct);
        return ToResponse(driver);
    }

    public async Task<DriverResponse> UpdateAsync(Guid id, DriverRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var driver = await LoadAsync(id, ct);
        await ApplyAsync(request, driver, ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(driver);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var driver = await LoadAsync(id, ct);
        db.Drivers.Remove(driver);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Driver> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Drivers.SingleOrDefaultAsync(d => d.Id == id, ct)
        ?? throw new NotFoundException("Motorista não encontrado. Ele pode ter sido excluído.");

    private async Task ApplyAsync(DriverRequest request, Driver driver, CancellationToken ct)
    {
        var cpf = Cpf.Normalize(request.Cpf);
        var licenseNumber = DriverLicenseNumber.Normalize(request.LicenseNumber);

        if (await db.Drivers.AnyAsync(d => d.Cpf == cpf && d.Id != driver.Id, ct))
            throw new ConflictException("Já existe um motorista com este CPF nesta empresa.", "cpf");
        if (await db.Drivers.AnyAsync(d => d.LicenseNumber == licenseNumber && d.Id != driver.Id, ct))
            throw new ConflictException("Já existe um motorista com este número de CNH nesta empresa.", "licenseNumber");

        driver.FullName = request.FullName!.Trim();
        driver.Cpf = cpf;
        driver.Rg = request.Rg.TrimToNull()?.ToUpperInvariant();
        driver.BirthDate = request.BirthDate!.Value;
        driver.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : Phone.Normalize(request.Phone);
        driver.Email = string.IsNullOrWhiteSpace(request.Email) ? null : EmailAddress.Normalize(request.Email);
        request.Address.ApplyTo(driver.Address);
        driver.LicenseNumber = licenseNumber;
        driver.LicenseCategory = request.LicenseCategory!.Value;
        driver.LicenseExpiresOn = request.LicenseExpiresOn!.Value;
        driver.PerformsPaidActivity = request.PerformsPaidActivity;
        driver.Status = request.Status;
        driver.Notes = request.Notes.TrimToNull();
    }

    private DriverResponse ToResponse(Driver d)
    {
        var today = clock.Today;
        var state = d.IsLicenseExpired(today) ? LicenseState.Expired
            : d.IsLicenseExpiringSoon(today) ? LicenseState.ExpiringSoon : LicenseState.Valid;
        return new DriverResponse(
            d.Id, d.FullName, d.Cpf, d.Rg, d.BirthDate, d.Phone, d.Email, AddressDto.From(d.Address),
            d.LicenseNumber, d.LicenseCategory, d.LicenseExpiresOn, state, d.PerformsPaidActivity,
            d.Status, d.Notes, d.CreatedAt, d.UpdatedAt);
    }
}
