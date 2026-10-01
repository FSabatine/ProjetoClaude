using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Maintenance;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Maintenance;

namespace Fleet.Application.Tests.Maintenance;

public class WorkshopServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private WorkshopService Service => Services.Workshops(_t);

    [Fact]
    public async Task CreateAsync_Valid_PersistsNormalizedDocumentAndPhone()
    {
        await Scenario.SignedInAsync(_t);

        var workshop = await Service.CreateAsync(Requests.Workshop() with { Document = "123.456.789-09", Phone = "(41) 99999-9999" }, default);

        workshop.Document.Should().Be("12345678909");
        workshop.Phone.Should().Be("41999999999");
        workshop.Status.Should().Be(WorkshopStatus.Active);
    }

    [Fact]
    public async Task CreateAsync_InvalidDocument_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);

        var act = () => Service.CreateAsync(Requests.Workshop() with { Document = "000.000.000-00" }, default);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task DeleteAsync_WorkshopWithWorkOrders_IsRefused()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var workshop = await Service.CreateAsync(Requests.Workshop(), default);
        await Services.WorkOrders(_t).CreateAsync(Requests.WorkOrder(vehicle.Id) with { WorkshopId = workshop.Id }, default);

        var act = () => Service.DeleteAsync(workshop.Id, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*não pode ser excluída*");
    }

    [Fact]
    public async Task GetAsync_WorkshopOfAnotherCompany_ThrowsNotFound()
    {
        await Scenario.SignedInAsync(_t);
        var workshop = await Service.CreateAsync(Requests.Workshop(), default);
        await Scenario.SignedInAsync(_t, cnpj: "12ABC34501DE35");

        var act = () => Service.GetAsync(workshop.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
