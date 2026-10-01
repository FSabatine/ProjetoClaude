using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Maintenance;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Maintenance;

public class MaintenanceRequestServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private MaintenanceRequestService Service => Services.MaintenanceRequests(_t);

    [Fact]
    public async Task CreateAsync_FromOccurrence_ForcesSourceOccurrence()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var occurrence = await Services.Occurrences(_t).CreateAsync(new Fleet.Application.Occurrences.OccurrenceRequest
        {
            VehicleId = vehicle.Id, Type = Domain.Occurrences.OccurrenceType.MechanicalIssue,
            Severity = Domain.Occurrences.OccurrenceSeverity.High, Description = "Barulho estranho no motor",
        }, default);

        var request = await Service.CreateAsync(Requests.MaintenanceRequest(vehicle.Id) with
        {
            Source = MaintenanceRequestSource.Driver, OccurrenceId = occurrence.Id,
        }, default);

        request.Source.Should().Be(MaintenanceRequestSource.Occurrence);
        request.OccurrenceId.Should().Be(occurrence.Id);
        (await _t.NewContext().OperationalEvents.AnyAsync(e => e.Type == OperationalEventType.MaintenanceRequestCreated)).Should().BeTrue();
    }

    [Fact]
    public async Task ApproveAsync_OpensApprovedWorkOrderAndConvertsRequest()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var request = await Service.CreateAsync(Requests.MaintenanceRequest(vehicle.Id), default);

        var approved = await Service.ApproveAsync(request.Id, default);

        approved.Status.Should().Be(MaintenanceRequestStatus.Converted);
        approved.WorkOrderId.Should().NotBeNull();
        var workOrder = await Services.WorkOrders(_t).GetAsync(approved.WorkOrderId!.Value, default);
        workOrder.Status.Should().Be(WorkOrderStatus.Approved);
        workOrder.MaintenanceRequestId.Should().Be(request.Id);
    }

    [Fact]
    public async Task RejectAsync_RequiresReasonAndIsFinal()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var request = await Service.CreateAsync(Requests.MaintenanceRequest(vehicle.Id), default);

        var act = () => Service.RejectAsync(request.Id, new MaintenanceRequestRejectRequest(), default);
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();

        var rejected = await Service.RejectAsync(request.Id, new MaintenanceRequestRejectRequest { Reason = "Duplicada" }, default);
        rejected.Status.Should().Be(MaintenanceRequestStatus.Rejected);

        var reApprove = () => Service.ApproveAsync(request.Id, default);
        await reApprove.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task GetAsync_RequestOfAnotherCompany_ThrowsNotFound()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var request = await Service.CreateAsync(Requests.MaintenanceRequest(vehicle.Id), default);
        await Scenario.SignedInAsync(_t, cnpj: "12ABC34501DE35");

        var act = () => Service.GetAsync(request.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
