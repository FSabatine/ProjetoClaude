using System.Text.Json;
using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Tests.TestSupport;
using Fleet.Application.Vehicles;
using Fleet.Domain.Auditing;
using Fleet.Domain.Authorization;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Vehicles;

public class VehicleServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private async Task<VehicleService> SignedInServiceAsync(string cnpj = "11222333000181")
    {
        var company = await _t.AddCompanyAsync(cnpj);
        _t.SignInAs(company, SystemRoles.FleetManager);
        return Services.Vehicles(_t);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_NormalizesAndStampsTenantAndAuthor()
    {
        var service = await SignedInServiceAsync();

        var created = await service.CreateAsync(Requests.Vehicle(plate: "abc-1d23", chassis: "9bwzzz377vt004251"), default);

        created.LicensePlate.Should().Be("ABC1D23");
        created.Chassis.Should().Be("9BWZZZ377VT004251");
        var stored = await _t.NewContext().Vehicles.IgnoreQueryFilters().SingleAsync();
        stored.CompanyId.Should().Be(_t.CurrentUser.CompanyId!.Value);
        stored.CreatedBy.Should().Be(_t.CurrentUser.UserId);
        stored.CreatedAt.Should().Be(_t.Clock.UtcNow);
    }

    [Fact]
    public async Task CreateAsync_WithHourMeter_SeedsHourMeterHistory()
    {
        var service = await SignedInServiceAsync();

        var created = await service.CreateAsync(Requests.Vehicle() with { HourMeter = 500m }, default);

        created.HourMeter.Should().Be(500m);
        var db = _t.NewContext();
        (await db.HourMeterReadings.SingleAsync(r => r.VehicleId == created.Id)).Source.Should().Be(Domain.Maintenance.HourMeterReadingSource.Registration);
    }

    [Fact]
    public async Task UpdateAsync_ChangingHourMeterDirectly_IsRefused()
    {
        var service = await SignedInServiceAsync();
        var created = await service.CreateAsync(Requests.Vehicle() with { HourMeter = 500m }, default);

        var act = () => service.UpdateAsync(created.Id, Requests.Vehicle() with { HourMeter = 600m }, default);

        var error = (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single();
        error.PropertyName.Should().Be("hourMeter");
        error.ErrorMessage.Should().Contain("registro de leituras");
    }

    [Fact]
    public async Task CreateAsync_DuplicatePlateInSameCompany_ThrowsConflict()
    {
        var service = await SignedInServiceAsync();
        await service.CreateAsync(Requests.Vehicle(), default);

        var act = () => service.CreateAsync(Requests.Vehicle(renavam: "00123456789", chassis: "9BSR6X400J1234567"), default);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Field.Should().Be("licensePlate");
    }

    [Fact]
    public async Task CreateAsync_PlateAlreadyUsedByImplement_ThrowsConflict()
    {
        var service = await SignedInServiceAsync();
        await Services.Implements(_t).CreateAsync(Requests.Implement(plate: "ABC1D23"), default);

        var act = () => service.CreateAsync(Requests.Vehicle(plate: "ABC-1D23"), default);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("implemento");
    }

    [Fact]
    public async Task CreateAsync_SamePlateInAnotherCompany_IsAllowed()
    {
        var service = await SignedInServiceAsync();
        await service.CreateAsync(Requests.Vehicle(), default);

        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        _t.SignInAs(other, SystemRoles.FleetManager);
        var act = () => Services.Vehicles(_t).CreateAsync(Requests.Vehicle(), default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateAsync_InvalidData_ThrowsValidationWithFieldErrors()
    {
        var service = await SignedInServiceAsync();
        var request = Requests.Vehicle() with { LicensePlate = "XX-99", Renavam = "12345678901", ModelYear = 2030, CurrentOdometerKm = -5 };

        var act = () => service.CreateAsync(request, default);

        var errors = (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Select(e => e.PropertyName);
        errors.Should().Contain(["LicensePlate", "Renavam", "ModelYear", "CurrentOdometerKm"]);
    }

    [Fact]
    public async Task CreateAsync_FutureAcquisitionDate_ThrowsValidation()
    {
        var service = await SignedInServiceAsync();

        var act = () => service.CreateAsync(Requests.Vehicle() with { AcquisitionDate = _t.Clock.Today.AddDays(1) }, default);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetAsync_VehicleOfAnotherCompany_ThrowsNotFound()
    {
        var service = await SignedInServiceAsync();
        var created = await service.CreateAsync(Requests.Vehicle(), default);

        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        _t.SignInAs(other, SystemRoles.Administrator);
        var otherService = Services.Vehicles(_t);

        await ((Func<Task>)(() => otherService.GetAsync(created.Id, default))).Should().ThrowAsync<NotFoundException>();
        await ((Func<Task>)(() => otherService.UpdateAsync(created.Id, Requests.Vehicle(), default))).Should().ThrowAsync<NotFoundException>();
        await ((Func<Task>)(() => otherService.DeleteAsync(created.Id, default))).Should().ThrowAsync<NotFoundException>();
        (await otherService.ListAsync(new VehicleListRequest(), default)).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndFreesThePlate()
    {
        var service = await SignedInServiceAsync();
        var created = await service.CreateAsync(Requests.Vehicle(), default);

        await service.DeleteAsync(created.Id, default);

        var stored = await _t.NewContext().Vehicles.IgnoreQueryFilters().SingleAsync(v => v.Id == created.Id);
        stored.DeletedAt.Should().Be(_t.Clock.UtcNow);
        stored.DeletedBy.Should().Be(_t.CurrentUser.UserId);
        (await service.ListAsync(new VehicleListRequest(), default)).TotalCount.Should().Be(0);
        await service.Invoking(s => s.CreateAsync(Requests.Vehicle(), default)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_VehicleOnTrip_ThrowsBusinessRule()
    {
        var service = await SignedInServiceAsync();
        var created = await service.CreateAsync(Requests.Vehicle() with { Status = VehicleStatus.OnTrip }, default);

        await service.Invoking(s => s.DeleteAsync(created.Id, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UpdateAsync_WritesAuditLogWithOldAndNewValues()
    {
        var service = await SignedInServiceAsync();
        var created = await service.CreateAsync(Requests.Vehicle(), default);

        await service.UpdateAsync(created.Id, Requests.Vehicle() with { Color = "Azul", Status = VehicleStatus.UnderMaintenance }, default);

        var logs = await _t.NewContext().AuditLogs.Where(a => a.EntityId == created.Id.ToString()).OrderBy(a => a.Id).ToListAsync();
        logs.Select(l => l.Action).Should().Equal(AuditAction.Created, AuditAction.Updated);
        var changes = JsonDocument.Parse(logs[1].Changes).RootElement;
        changes.GetProperty("Color").GetProperty("new").GetString().Should().Be("Azul");
        changes.GetProperty("Status").GetProperty("old").GetString().Should().Be("Available");
        changes.GetProperty("Status").GetProperty("new").GetString().Should().Be("UnderMaintenance");
        changes.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["Color", "Status"]);
        logs[1].UserId.Should().Be(_t.CurrentUser.UserId);
        logs[1].CompanyId.Should().Be(_t.CurrentUser.CompanyId);
    }

    [Fact]
    public async Task UpdateAsync_NoChanges_DoesNotWriteAuditLogNorTouchUpdatedAt()
    {
        var service = await SignedInServiceAsync();
        var created = await service.CreateAsync(Requests.Vehicle(), default);

        var updated = await service.UpdateAsync(created.Id, Requests.Vehicle(), default);

        updated.UpdatedAt.Should().BeNull();
        (await _t.NewContext().AuditLogs.CountAsync()).Should().Be(3); // company + vehicle + initial odometer reading
    }

    [Fact]
    public async Task ListAsync_FiltersSearchesSortsAndPaginates()
    {
        var service = await SignedInServiceAsync();
        await service.CreateAsync(Requests.Vehicle("AAA1A11", "12345678900", "9BWZZZ377VT004251") with { Model = "Actros" }, default);
        await service.CreateAsync(Requests.Vehicle("BBB2B22", "00123456789", "9BSR6X400J1234567") with { Status = VehicleStatus.UnderMaintenance }, default);
        await service.CreateAsync(Requests.Vehicle("CCC3C33", "23456789013", "9BM958074AB123456"), default);

        var maintenance = await service.ListAsync(new VehicleListRequest { Status = VehicleStatus.UnderMaintenance }, default);
        maintenance.Items.Should().ContainSingle(v => v.LicensePlate == "BBB2B22");

        var search = await service.ListAsync(new VehicleListRequest { Search = "Actros" }, default);
        search.Items.Should().ContainSingle(v => v.LicensePlate == "AAA1A11");

        var plateSearch = await service.ListAsync(new VehicleListRequest { Search = "ccc-3c" }, default);
        plateSearch.Items.Should().ContainSingle(v => v.LicensePlate == "CCC3C33");

        var page = await service.ListAsync(new VehicleListRequest { SortBy = "licensePlate", SortDirection = SortDirection.Desc, PageSize = 2, Page = 1 }, default);
        page.Items.Select(v => v.LicensePlate).Should().Equal("CCC3C33", "BBB2B22");
        page.TotalCount.Should().Be(3);
        page.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task ListAsync_UnknownSortColumn_FallsBackToDefaultAndClampsPageSize()
    {
        var service = await SignedInServiceAsync();
        await service.CreateAsync(Requests.Vehicle(), default);

        var result = await service.ListAsync(new VehicleListRequest { SortBy = "PasswordHash; DROP TABLE", PageSize = 10_000 }, default);

        result.Items.Should().HaveCount(1);
        result.PageSize.Should().Be(ListRequest.MaxPageSize);
    }
}
