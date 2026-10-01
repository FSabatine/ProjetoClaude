using Fleet.Api.Authorization;
using Fleet.Application.Common;
using Fleet.Application.Maintenance;
using Fleet.Domain.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Controllers;

// Phase 3 — maintenance. Same shape as Phase 2 controllers: permission gate + service call, no logic here.

[ApiController]
[Route("api/v1/workshops")]
public sealed class WorkshopsController(WorkshopService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Maintenance.View, Permissions.Maintenance.ManageWorkshops)]
    public Task<PagedResult<WorkshopResponse>> List([FromQuery] WorkshopListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Maintenance.View, Permissions.Maintenance.ManageWorkshops)]
    public Task<WorkshopResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Maintenance.ManageWorkshops)]
    public async Task<ActionResult<WorkshopResponse>> Create(WorkshopRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Maintenance.ManageWorkshops)]
    public Task<WorkshopResponse> Update(Guid id, WorkshopRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Maintenance.ManageWorkshops)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/maintenance-plans")]
public sealed class MaintenancePlansController(MaintenancePlanService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Maintenance.View, Permissions.Maintenance.ManagePlans)]
    public Task<IReadOnlyList<MaintenancePlanListItemResponse>> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(includeInactive, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Maintenance.View, Permissions.Maintenance.ManagePlans)]
    public Task<MaintenancePlanResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Maintenance.ManagePlans)]
    public async Task<ActionResult<MaintenancePlanResponse>> Create(MaintenancePlanRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Maintenance.ManagePlans)]
    public Task<MaintenancePlanResponse> Update(Guid id, MaintenancePlanRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Maintenance.ManagePlans)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1")]
public sealed class HourMeterController(HourMeterService service) : ControllerBase
{
    [HttpGet("vehicles/{vehicleId:guid}/hour-meter-readings"), HasPermission(Permissions.Maintenance.View)]
    public Task<PagedResult<HourMeterReadingResponse>> List(Guid vehicleId, [FromQuery] HourMeterReadingListRequest request, CancellationToken ct) =>
        service.ListAsync(vehicleId, request, ct);

    [HttpPost("vehicles/{vehicleId:guid}/hour-meter-readings"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public async Task<ActionResult<HourMeterReadingResponse>> Record(Guid vehicleId, HourMeterReadingRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.RecordAsync(vehicleId, request, ct));

    [HttpPost("hour-meter-readings/{id:guid}/approve"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public Task<HourMeterReadingResponse> Approve(Guid id, HourMeterReviewRequest request, CancellationToken ct) => service.ApproveAsync(id, request, ct);

    [HttpPost("hour-meter-readings/{id:guid}/reject"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public Task<HourMeterReadingResponse> Reject(Guid id, HourMeterReviewRequest request, CancellationToken ct) => service.RejectAsync(id, request, ct);
}

[ApiController]
[Route("api/v1/vehicles/{vehicleId:guid}/maintenance")]
public sealed class VehicleMaintenanceController(MaintenanceScheduleService schedules, WorkOrderService workOrders) : ControllerBase
{
    [HttpGet("schedule"), HasPermission(Permissions.Maintenance.View)]
    public Task<IReadOnlyList<MaintenanceScheduleItemResponse>> Schedule(Guid vehicleId, CancellationToken ct) =>
        schedules.ForVehicleAsync(vehicleId, ct);

    [HttpGet("history"), HasPermission(Permissions.Maintenance.View)]
    public Task<PagedResult<WorkOrderListItemResponse>> History(Guid vehicleId, [FromQuery] ListRequest request, CancellationToken ct) =>
        workOrders.ListAsync(new WorkOrderListRequest
        {
            VehicleId = vehicleId, Search = request.Search, Page = request.Page, PageSize = request.PageSize,
            SortBy = request.SortBy, SortDirection = request.SortDirection,
        }, ct);

    [HttpGet("repeated-problems"), HasPermission(Permissions.Maintenance.View)]
    public Task<IReadOnlyList<RepeatedProblemResponse>> RepeatedProblems(Guid vehicleId, CancellationToken ct) =>
        workOrders.RepeatedProblemsAsync(vehicleId, ct);
}

[ApiController]
[Route("api/v1/maintenance-requests")]
public sealed class MaintenanceRequestsController(MaintenanceRequestService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Maintenance.View)]
    public Task<PagedResult<MaintenanceRequestListItemResponse>> List([FromQuery] MaintenanceRequestListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Maintenance.View)]
    public Task<MaintenanceRequestResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Maintenance.CreateRequest)]
    public async Task<ActionResult<MaintenanceRequestResponse>> Create(MaintenanceRequestRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPost("{id:guid}/approve"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public Task<MaintenanceRequestResponse> Approve(Guid id, CancellationToken ct) => service.ApproveAsync(id, ct);

    [HttpPost("{id:guid}/reject"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public Task<MaintenanceRequestResponse> Reject(Guid id, MaintenanceRequestRejectRequest request, CancellationToken ct) =>
        service.RejectAsync(id, request, ct);
}

[ApiController]
[Route("api/v1/work-orders")]
public sealed class WorkOrdersController(WorkOrderService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Maintenance.View)]
    public Task<PagedResult<WorkOrderListItemResponse>> List([FromQuery] WorkOrderListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Maintenance.View)]
    public Task<WorkOrderResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public async Task<ActionResult<WorkOrderResponse>> Create(WorkOrderRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public Task<WorkOrderResponse> Update(Guid id, WorkOrderRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/status"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public Task<WorkOrderResponse> ChangeStatus(Guid id, WorkOrderStatusRequest request, CancellationToken ct) =>
        service.ChangeStatusAsync(id, request, ct);

    [HttpPost("{id:guid}/items/{itemId:guid}/complete"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public Task<WorkOrderResponse> CompleteItem(Guid id, Guid itemId, WorkOrderItemCompleteRequest request, CancellationToken ct) =>
        service.CompleteItemAsync(id, itemId, request, ct);

    [HttpPost("{id:guid}/parts"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public async Task<ActionResult<WorkOrderResponse>> AddPart(Guid id, WorkOrderPartRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.AddPartAsync(id, request, ct));

    [HttpDelete("{id:guid}/parts/{partId:guid}"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public Task<WorkOrderResponse> RemovePart(Guid id, Guid partId, CancellationToken ct) => service.RemovePartAsync(id, partId, ct);

    [HttpPost("{id:guid}/labor"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public async Task<ActionResult<WorkOrderResponse>> AddLabor(Guid id, WorkOrderLaborRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.AddLaborAsync(id, request, ct));

    [HttpDelete("{id:guid}/labor/{laborId:guid}"), HasPermission(Permissions.Maintenance.ManageWorkOrders)]
    public Task<WorkOrderResponse> RemoveLabor(Guid id, Guid laborId, CancellationToken ct) => service.RemoveLaborAsync(id, laborId, ct);
}
