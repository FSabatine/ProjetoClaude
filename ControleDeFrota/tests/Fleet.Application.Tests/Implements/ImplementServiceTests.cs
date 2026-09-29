using FluentAssertions;
using Fleet.Application.Audit;
using Fleet.Application.Common;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Auditing;
using Fleet.Domain.Authorization;
using Fleet.Domain.Implements;
using FluentValidation;

namespace Fleet.Application.Tests.Implements;

public class ImplementServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private async Task SignInAsync()
    {
        var company = await _t.AddCompanyAsync();
        _t.SignInAs(company, SystemRoles.FleetManager);
    }

    [Fact]
    public async Task CreateAsync_CapacityWithoutUnit_ThrowsValidation()
    {
        await SignInAsync();

        var act = () => Services.Implements(_t).CreateAsync(Requests.Implement() with { CapacityUnit = null }, default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "CapacityUnit");
    }

    [Fact]
    public async Task CreateAsync_PlateUsedByVehicle_ThrowsConflict()
    {
        await SignInAsync();
        await Services.Vehicles(_t).CreateAsync(Requests.Vehicle(plate: "IMP1A23"), default);

        await Services.Implements(_t).Invoking(s => s.CreateAsync(Requests.Implement(plate: "IMP-1A23"), default))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task DeleteAsync_ImplementInUse_ThrowsBusinessRule()
    {
        await SignInAsync();
        var created = await Services.Implements(_t).CreateAsync(Requests.Implement() with { Status = ImplementStatus.InUse }, default);

        await Services.Implements(_t).Invoking(s => s.DeleteAsync(created.Id, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task AuditHistory_ShowsCreateUpdateDeleteForOwnCompanyOnly()
    {
        await SignInAsync();
        var service = Services.Implements(_t);
        var created = await service.CreateAsync(Requests.Implement(), default);
        await service.UpdateAsync(created.Id, Requests.Implement() with { Status = ImplementStatus.UnderMaintenance }, default);
        await service.DeleteAsync(created.Id, default);

        var history = await new AuditService(_t.Db, _t.CurrentUser).GetHistoryAsync("Implement", created.Id, default);

        history.Select(h => h.Action).Should().Equal(AuditAction.Deleted, AuditAction.Updated, AuditAction.Created);
        history[1].Changes.Should().ContainSingle(c => c.Field == "Status");

        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        _t.SignInAs(other, SystemRoles.Administrator);
        (await new AuditService(_t.Db, _t.CurrentUser).GetHistoryAsync("Implement", created.Id, default)).Should().BeEmpty();
    }
}
