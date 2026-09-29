using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Drivers;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Drivers;

public class DriverServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private DateOnly Today => _t.Clock.Today;

    private async Task<DriverService> SignedInServiceAsync()
    {
        var company = await _t.AddCompanyAsync();
        _t.SignInAs(company, SystemRoles.Operations);
        return Services.Drivers(_t);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_StoresNormalizedDocuments()
    {
        var service = await SignedInServiceAsync();

        var created = await service.CreateAsync(Requests.Driver(Today) with { Phone = "(41) 99999-8888", Email = " Joao@Empresa.com " }, default);

        created.Cpf.Should().Be("52998224725");
        created.Phone.Should().Be("41999998888");
        created.Email.Should().Be("joao@empresa.com");
        created.LicenseState.Should().Be(LicenseState.Valid);
    }

    [Theory]
    [InlineData("529.982.247-24")]
    [InlineData("111.111.111-11")]
    public async Task CreateAsync_InvalidCpf_ThrowsValidation(string cpf)
    {
        var service = await SignedInServiceAsync();

        var act = () => service.CreateAsync(Requests.Driver(Today, cpf: cpf), default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "Cpf");
    }

    [Theory]
    [InlineData(17)]
    [InlineData(101)]
    public async Task CreateAsync_AgeOutOfRange_ThrowsValidation(int age)
    {
        var service = await SignedInServiceAsync();

        var act = () => service.CreateAsync(Requests.Driver(Today) with { BirthDate = Today.AddYears(-age) }, default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "BirthDate");
    }

    [Fact]
    public async Task CreateAsync_InvalidOptionalAddress_ThrowsValidation()
    {
        var service = await SignedInServiceAsync();
        var request = Requests.Driver(Today) with { Address = new AddressDto { ZipCode = "123", State = "XX" } };

        var act = () => service.CreateAsync(request, default);

        var errors = (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Select(e => e.PropertyName);
        errors.Should().Contain(["Address.ZipCode", "Address.State"]);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCpfOrLicense_ThrowsConflict()
    {
        var service = await SignedInServiceAsync();
        await service.CreateAsync(Requests.Driver(Today), default);

        (await service.Invoking(s => s.CreateAsync(Requests.Driver(Today, license: "05623456789"), default))
            .Should().ThrowAsync<ConflictException>()).Which.Field.Should().Be("cpf");
        (await service.Invoking(s => s.CreateAsync(Requests.Driver(Today, cpf: "123.456.789-09"), default))
            .Should().ThrowAsync<ConflictException>()).Which.Field.Should().Be("licenseNumber");
    }

    [Fact]
    public async Task CreateAsync_ExpiredLicense_IsAllowedButFlagged()
    {
        var service = await SignedInServiceAsync();

        var created = await service.CreateAsync(Requests.Driver(Today) with { LicenseExpiresOn = Today.AddDays(-1) }, default);

        created.LicenseState.Should().Be(LicenseState.Expired);
    }

    [Fact]
    public async Task ListAsync_LicenseAlertFilter_ReturnsOnlyMatchingDrivers()
    {
        var service = await SignedInServiceAsync();
        await service.CreateAsync(Requests.Driver(Today, "529.982.247-25", "04512345678") with { LicenseExpiresOn = Today.AddDays(-5) }, default);
        await service.CreateAsync(Requests.Driver(Today, "123.456.789-09", "05623456789") with { LicenseExpiresOn = Today.AddDays(10) }, default);
        await service.CreateAsync(Requests.Driver(Today, "987.654.321-00", "06734567890"), default);

        var expired = await service.ListAsync(new DriverListRequest { LicenseAlert = LicenseAlertFilter.Expired }, default);
        var expiring = await service.ListAsync(new DriverListRequest { LicenseAlert = LicenseAlertFilter.ExpiringSoon }, default);
        var byCpf = await service.ListAsync(new DriverListRequest { Search = "987.654" }, default);

        expired.Items.Should().ContainSingle().Which.LicenseState.Should().Be(LicenseState.Expired);
        expiring.Items.Should().ContainSingle().Which.LicenseState.Should().Be(LicenseState.ExpiringSoon);
        byCpf.Items.Should().ContainSingle().Which.Cpf.Should().Be("98765432100");
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndKeepsRow()
    {
        var service = await SignedInServiceAsync();
        var created = await service.CreateAsync(Requests.Driver(Today), default);

        await service.DeleteAsync(created.Id, default);

        await service.Invoking(s => s.GetAsync(created.Id, default)).Should().ThrowAsync<NotFoundException>();
        (await _t.NewContext().Drivers.IgnoreQueryFilters().SingleAsync()).DeletedAt.Should().NotBeNull();
    }
}
