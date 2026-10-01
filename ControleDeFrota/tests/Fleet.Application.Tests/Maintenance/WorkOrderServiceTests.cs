using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Maintenance;
using Fleet.Application.Tests.TestSupport;
using Fleet.Application.Vehicles;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Operations;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Maintenance;

public class WorkOrderServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private WorkOrderService Service => Services.WorkOrders(_t);

    private Task<WorkOrderResponse> MoveAsync(Guid id, WorkOrderStatus to, string? resolution = null, string? reason = null) =>
        Service.ChangeStatusAsync(id, new WorkOrderStatusRequest { Status = to, Resolution = resolution, Reason = reason }, default);

    [Fact]
    public async Task CreateAsync_Valid_OpensDraftWithSequentialNumber()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);

        var first = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);
        var second = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);

        first.Status.Should().Be(WorkOrderStatus.Draft);
        first.Number.Should().Be("OS-000001");
        second.Number.Should().Be("OS-000002");
        (await _t.NewContext().OperationalEvents.AnyAsync(e => e.Type == OperationalEventType.WorkOrderOpened)).Should().BeTrue();
    }

    [Fact]
    public async Task ChangeStatusAsync_ToInProgress_SetsVehicleUnderMaintenance()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var workOrder = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);

        await MoveAsync(workOrder.Id, WorkOrderStatus.Approved);
        var started = await MoveAsync(workOrder.Id, WorkOrderStatus.InProgress);

        started.StartedAt.Should().Be(_t.Clock.UtcNow);
        (await Services.Vehicles(_t).GetAsync(vehicle.Id, default)).Status.Should().Be(VehicleStatus.UnderMaintenance);
        (await _t.NewContext().OperationalEvents.CountAsync(e => e.Type == OperationalEventType.VehicleStatusChanged)).Should().Be(1);
    }

    [Fact]
    public async Task ChangeStatusAsync_OnTripVehicle_CannotStartMaintenance()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        await Services.Vehicles(_t).UpdateAsync(vehicle.Id, Requests.Vehicle() with { Status = VehicleStatus.OnTrip }, default);
        var workOrder = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);
        await MoveAsync(workOrder.Id, WorkOrderStatus.Approved);

        var act = () => MoveAsync(workOrder.Id, WorkOrderStatus.InProgress);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*em viagem*");
    }

    [Fact]
    public async Task ChangeStatusAsync_Complete_RequiredItemPending_IsRefused()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var workOrder = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id) with
        {
            Items = [new WorkOrderItemRequest { Description = "Trocar pastilhas", IsRequired = true }],
        }, default);
        await MoveAsync(workOrder.Id, WorkOrderStatus.Approved);
        await MoveAsync(workOrder.Id, WorkOrderStatus.InProgress);

        var act = () => MoveAsync(workOrder.Id, WorkOrderStatus.Completed, resolution: "Feito");

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*itens obrigatórios pendentes*");
    }

    [Fact]
    public async Task ChangeStatusAsync_Complete_WithoutResolution_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var workOrder = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);
        await MoveAsync(workOrder.Id, WorkOrderStatus.Approved);
        await MoveAsync(workOrder.Id, WorkOrderStatus.InProgress);

        var act = () => MoveAsync(workOrder.Id, WorkOrderStatus.Completed);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task ChangeStatusAsync_Complete_ReleasesVehicleOnlyWhenNoOtherActiveOrder()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var first = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);
        var second = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);
        foreach (var id in new[] { first.Id, second.Id })
        {
            await MoveAsync(id, WorkOrderStatus.Approved);
            await MoveAsync(id, WorkOrderStatus.InProgress);
        }

        await MoveAsync(first.Id, WorkOrderStatus.Completed, resolution: "Feito");
        (await Services.Vehicles(_t).GetAsync(vehicle.Id, default)).Status.Should().Be(VehicleStatus.UnderMaintenance);

        await MoveAsync(second.Id, WorkOrderStatus.Completed, resolution: "Feito");
        (await Services.Vehicles(_t).GetAsync(vehicle.Id, default)).Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public async Task ChangeStatusAsync_Cancel_DoesNotOverrideManuallyChangedVehicleStatus()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var workOrder = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);
        await MoveAsync(workOrder.Id, WorkOrderStatus.Approved);
        await MoveAsync(workOrder.Id, WorkOrderStatus.InProgress);
        // Manager manually marks the vehicle unavailable for another reason while the WO is open.
        await Services.Vehicles(_t).UpdateAsync(vehicle.Id, Requests.Vehicle() with { Status = VehicleStatus.Unavailable }, default);

        await MoveAsync(workOrder.Id, WorkOrderStatus.Cancelled, reason: "Peça fora de linha");

        (await Services.Vehicles(_t).GetAsync(vehicle.Id, default)).Status.Should().Be(VehicleStatus.Unavailable);
    }

    [Fact]
    public async Task AddPartAndLabor_RecalculatesTotalCost()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var workOrder = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);

        await Service.AddPartAsync(workOrder.Id, new WorkOrderPartRequest { PartName = "Filtro de óleo", Quantity = 2, UnitCost = 50 }, default);
        var withLabor = await Service.AddLaborAsync(workOrder.Id, new WorkOrderLaborRequest { TechnicianName = "Carlos", Hours = 3, HourlyRate = 40 }, default);

        withLabor.PartsCost.Should().Be(100);
        withLabor.LaborCost.Should().Be(120);
        withLabor.TotalCost.Should().Be(220);
    }

    [Fact]
    public async Task ViewCosts_WithoutPermission_CostsAreHidden()
    {
        await Scenario.SignedInAsync(_t); // FleetManager has maintenance.viewcosts
        var vehicle = await Scenario.VehicleAsync(_t);
        var workOrder = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);
        await Service.AddPartAsync(workOrder.Id, new WorkOrderPartRequest { PartName = "Filtro", Quantity = 1, UnitCost = 80 }, default);

        _t.CurrentUser.PermissionSet.Remove(Domain.Authorization.Permissions.Maintenance.ViewCosts);
        var withoutCosts = await Service.GetAsync(workOrder.Id, default);

        withoutCosts.TotalCost.Should().BeNull();
        withoutCosts.Parts.Single().UnitCost.Should().Be(0);
    }

    [Fact]
    public async Task ChangeStatusAsync_CompletePreventiveItem_RecalculatesMaintenanceSchedule()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var plan = await Services.MaintenancePlans(_t).CreateAsync(Requests.MaintenancePlan(vehicleId: vehicle.Id), default);
        var planItemId = plan.Items.Single().Id;
        var workOrder = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id) with
        {
            Type = MaintenanceType.Preventive,
            Items = [new WorkOrderItemRequest { Description = "Troca de óleo", MaintenancePlanItemId = planItemId }],
        }, default);
        await MoveAsync(workOrder.Id, WorkOrderStatus.Approved);
        await MoveAsync(workOrder.Id, WorkOrderStatus.InProgress);
        await Service.CompleteItemAsync(workOrder.Id, workOrder.Items.Single().Id, new WorkOrderItemCompleteRequest(), default);

        await MoveAsync(workOrder.Id, WorkOrderStatus.Completed, resolution: "Óleo trocado");

        var schedule = await Services.MaintenanceSchedules(_t).ForVehicleAsync(vehicle.Id, default);
        var item = schedule.Single(s => s.MaintenancePlanItemId == planItemId);
        item.LastPerformedKm.Should().Be(vehicle.CurrentOdometerKm);
        item.NextDueKm.Should().Be(vehicle.CurrentOdometerKm + 10_000);
        item.Status.Should().Be(MaintenanceScheduleStatus.Scheduled);
    }

    [Fact]
    public async Task GetAsync_WorkOrderOfAnotherCompany_ThrowsNotFound()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var workOrder = await Service.CreateAsync(Requests.WorkOrder(vehicle.Id), default);
        await Scenario.SignedInAsync(_t, cnpj: "12ABC34501DE35");

        var act = () => Service.GetAsync(workOrder.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
