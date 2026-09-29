using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Companies;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Users;
using FluentValidation;

namespace Fleet.Application.Tests.Companies;

public class CompanyServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task CreateAsync_AlphanumericCnpj_IsAcceptedAndNormalized()
    {
        var own = await _t.AddCompanyAsync();
        _t.SignInAs(own, SystemRoles.PlatformAdministrator);

        var created = await Services.Companies(_t).CreateAsync(Requests.Company("12.abc.345/01de-35"), default);

        created.Cnpj.Should().Be("12ABC34501DE35");
        created.Address.State.Should().Be("PR");
        created.Address.ZipCode.Should().Be("86010000");
    }

    [Fact]
    public async Task CreateAsync_DuplicateCnpj_ThrowsConflict()
    {
        var own = await _t.AddCompanyAsync("11222333000181");
        _t.SignInAs(own, SystemRoles.PlatformAdministrator);

        var act = () => Services.Companies(_t).CreateAsync(Requests.Company("11.222.333/0001-81"), default);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Field.Should().Be("cnpj");
    }

    [Fact]
    public async Task CreateAsync_MissingRequiredAddress_ThrowsValidation()
    {
        var own = await _t.AddCompanyAsync();
        _t.SignInAs(own, SystemRoles.PlatformAdministrator);

        var act = () => Services.Companies(_t).CreateAsync(Requests.Company() with { Address = new AddressDto() }, default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Select(e => e.PropertyName)
            .Should().Contain(["Address.Street", "Address.City", "Address.ZipCode"]);
    }

    [Fact]
    public async Task GetAsync_OtherCompanyWithoutPlatformPermission_ThrowsNotFound()
    {
        var own = await _t.AddCompanyAsync("11222333000181");
        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        _t.SignInAs(own, SystemRoles.Administrator);

        await Services.Companies(_t).Invoking(s => s.GetAsync(other.Id, default)).Should().ThrowAsync<NotFoundException>();
        (await Services.Companies(_t).GetCurrentAsync(default)).Id.Should().Be(own.Id);
    }

    [Fact]
    public async Task GetAsync_OtherCompanyAsPlatformAdmin_Succeeds()
    {
        var own = await _t.AddCompanyAsync("11222333000181");
        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        _t.SignInAs(own, SystemRoles.PlatformAdministrator);

        (await Services.Companies(_t).GetAsync(other.Id, default)).LegalName.Should().Be("Outra");
    }

    [Fact]
    public async Task UpdateAsync_DeactivatingOwnCompany_ThrowsBusinessRule()
    {
        var own = await _t.AddCompanyAsync("11222333000181");
        _t.SignInAs(own, SystemRoles.Administrator);

        var act = () => Services.Companies(_t).UpdateAsync(own.Id, Requests.Company("11222333000181") with { IsActive = false }, default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task DeleteAsync_CompanyWithActiveUsers_ThrowsBusinessRule()
    {
        var own = await _t.AddCompanyAsync("11222333000181");
        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        await _t.AddUserAsync(other, "user@outra.com", SystemRoles.Viewer);
        _t.SignInAs(own, SystemRoles.PlatformAdministrator);

        await Services.Companies(_t).Invoking(s => s.DeleteAsync(other.Id, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task DeleteAsync_OwnCompany_ThrowsBusinessRule()
    {
        var own = await _t.AddCompanyAsync();
        _t.SignInAs(own, SystemRoles.PlatformAdministrator);

        await Services.Companies(_t).Invoking(s => s.DeleteAsync(own.Id, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task DeleteAsync_CompanyWithOnlyInactiveUsers_SoftDeletes()
    {
        var own = await _t.AddCompanyAsync("11222333000181");
        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        var user = await _t.AddUserAsync(other, "user@outra.com", SystemRoles.Viewer);
        user.Status = UserStatus.Inactive;
        await _t.Db.SaveChangesAsync();
        _t.SignInAs(own, SystemRoles.PlatformAdministrator);

        await Services.Companies(_t).DeleteAsync(other.Id, default);

        (await Services.Companies(_t).ListAsync(new CompanyListRequest(), default)).Items.Should().ContainSingle(c => c.Id == own.Id);
    }
}
