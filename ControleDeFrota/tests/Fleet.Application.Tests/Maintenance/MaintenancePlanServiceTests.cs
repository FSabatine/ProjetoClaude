using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Maintenance;
using Fleet.Application.Tests.TestSupport;

namespace Fleet.Application.Tests.Maintenance;

public class MaintenancePlanServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private MaintenancePlanService Service => Services.MaintenancePlans(_t);

    [Fact]
    public async Task CreateAsync_Valid_PersistsItems()
    {
        await Scenario.SignedInAsync(_t);

        var plan = await Service.CreateAsync(Requests.MaintenancePlan(), default);

        plan.Items.Should().ContainSingle(i => i.ServiceName == "Troca de óleo" && i.IntervalKm == 10_000);
    }

    [Fact]
    public async Task CreateAsync_NoInterval_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);
        var request = Requests.MaintenancePlan() with
        {
            Items = [new MaintenancePlanItemRequest { ServiceName = "Revisão geral" }],
        };

        var act = () => Service.CreateAsync(request, default);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>().WithMessage("*intervalo*");
    }

    [Fact]
    public async Task UpdateAsync_RemovingItemWithHistory_IsRefused()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var plan = await Service.CreateAsync(Requests.MaintenancePlan(vehicleId: vehicle.Id), default);
        var itemId = plan.Items.Single().Id;

        // Complete a work order against the plan item, which creates a MaintenanceSchedule row for it.
        var workOrder = await Services.WorkOrders(_t).CreateAsync(Requests.WorkOrder(vehicle.Id) with
        {
            Type = Domain.Maintenance.MaintenanceType.Preventive,
            Items = [new WorkOrderItemRequest { Description = "Troca de óleo", MaintenancePlanItemId = itemId }],
        }, default);
        await Services.WorkOrders(_t).ChangeStatusAsync(workOrder.Id, new WorkOrderStatusRequest { Status = Domain.Maintenance.WorkOrderStatus.Approved }, default);
        await Services.WorkOrders(_t).ChangeStatusAsync(workOrder.Id, new WorkOrderStatusRequest { Status = Domain.Maintenance.WorkOrderStatus.InProgress }, default);
        await Services.WorkOrders(_t).CompleteItemAsync(workOrder.Id, workOrder.Items.Single().Id, new WorkOrderItemCompleteRequest(), default);
        await Services.WorkOrders(_t).ChangeStatusAsync(workOrder.Id,
            new WorkOrderStatusRequest { Status = Domain.Maintenance.WorkOrderStatus.Completed, Resolution = "Óleo trocado" }, default);

        var act = () => Service.UpdateAsync(plan.Id, Requests.MaintenancePlan(vehicleId: vehicle.Id) with
        {
            Items = [new MaintenancePlanItemRequest { ServiceName = "Outro serviço", IntervalKm = 5_000 }],
        }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*já tem manutenções registradas*");
    }

    [Fact]
    public async Task GetAsync_PlanOfAnotherCompany_ThrowsNotFound()
    {
        await Scenario.SignedInAsync(_t);
        var plan = await Service.CreateAsync(Requests.MaintenancePlan(), default);
        await Scenario.SignedInAsync(_t, cnpj: "12ABC34501DE35");

        var act = () => Service.GetAsync(plan.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
