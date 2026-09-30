using Fleet.Application.Assignments;
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
        var active = AssignmentService.Active(db);

        if (request.Status is { } status) query = query.Where(d => d.Status == status);
        if (request.LicenseCategory is { } category) query = query.Where(d => d.LicenseCategory == category);
        query = request.Assignment switch
        {
            DriverAssignmentFilter.WithVehicle => query.Where(d => active.Any(a => a.DriverId == d.Id)),
            DriverAssignmentFilter.WithoutVehicle => query.Where(d => !active.Any(a => a.DriverId == d.Id)),
            _ => query,
        };
        if (request.VehicleId is { } vehicleId) query = query.Where(d => active.Any(a => a.DriverId == d.Id && a.VehicleId == vehicleId));
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
            var plate = LicensePlate.Normalize(term);
            query = query.Where(d => d.FullName.Contains(term) ||
                                     (searchDocuments && (d.Cpf.Contains(digits) || d.LicenseNumber.Contains(digits))) ||
                                     active.Any(a => a.DriverId == d.Id && a.Vehicle.LicensePlate.Contains(plate)));
        }

        return await Sorts.Apply(query, request.SortBy, request.SortDirection).ToPagedResultAsync(request, d =>
            new DriverListItemResponse(
                d.Id, d.FullName, d.Cpf, d.Phone, d.LicenseCategory, d.LicenseExpiresOn,
                d.LicenseExpiresOn < today ? LicenseState.Expired
                    : d.LicenseExpiresOn <= alertLimit ? LicenseState.ExpiringSoon : LicenseState.Valid,
                d.Status,
                active.Where(a => a.DriverId == d.Id).Select(a => (Guid?)a.VehicleId).FirstOrDefault(),
                active.Where(a => a.DriverId == d.Id).Select(a => a.Vehicle.LicensePlate).FirstOrDefault()), ct);
    }

    public async Task<DriverResponse> GetAsync(Guid id, CancellationToken ct) => await ToResponseAsync(await LoadAsync(id, ct), ct);

    public async Task<DriverResponse> CreateAsync(DriverRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var driver = new Driver();
        await ApplyAsync(request, driver, ct);
        db.Drivers.Add(driver);
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(driver, ct);
    }

    public async Task<DriverResponse> UpdateAsync(Guid id, DriverRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var driver = await LoadAsync(id, ct);
        // A driver on leave keeps the vehicle reserved; a dismissed one must hand it over first (ADR-020).
        if (request.Status == DriverStatus.Inactive && driver.Status != DriverStatus.Inactive &&
            await AssignmentService.Active(db).AnyAsync(a => a.DriverId == id, ct))
            throw new BusinessRuleException("Este motorista está alocado a um veículo. Encerre a alocação antes de desligá-lo.");
        await ApplyAsync(request, driver, ct);
        await db.SaveChangesAsync(ct);
        return await ToResponseAsync(driver, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var driver = await LoadAsync(id, ct);
        if (await db.VehicleAssignments.AnyAsync(a => a.DriverId == id, ct) ||
            await db.ChecklistExecutions.AnyAsync(e => e.DriverId == id, ct) ||
            await db.Occurrences.AnyAsync(o => o.DriverId == id, ct))
            throw new BusinessRuleException("Este motorista tem histórico operacional (alocações, checklists ou ocorrências) e não pode ser excluído. Altere a situação para Desligado.");
        foreach (var document in await db.Documents.Where(d => d.DriverId == id).ToListAsync(ct)) db.Documents.Remove(document);
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

    private async Task<DriverResponse> ToResponseAsync(Driver d, CancellationToken ct)
    {
        var today = clock.Today;
        var vehicle = await AssignmentService.Active(db).Where(a => a.DriverId == d.Id)
            .Select(a => new DriverCurrentVehicleResponse(a.Id, a.VehicleId, a.Vehicle.LicensePlate, a.Vehicle.Manufacturer + " " + a.Vehicle.Model, a.StartedAt))
            .FirstOrDefaultAsync(ct);
        var state = d.IsLicenseExpired(today) ? LicenseState.Expired
            : d.IsLicenseExpiringSoon(today) ? LicenseState.ExpiringSoon : LicenseState.Valid;
        return new DriverResponse(
            d.Id, d.FullName, d.Cpf, d.Rg, d.BirthDate, d.Phone, d.Email, AddressDto.From(d.Address),
            d.LicenseNumber, d.LicenseCategory, d.LicenseExpiresOn, state, d.PerformsPaidActivity,
            d.Status, vehicle, d.Notes, d.CreatedAt, d.UpdatedAt);
    }
}
