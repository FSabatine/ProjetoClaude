using Fleet.Api.Authorization;
using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Application.Tires;
using Fleet.Domain.Authorization;
using Fleet.Domain.Tires;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Controllers;

// Phase 5 — tires. Same shape as the other modules: permission gate + service call, no logic here. Target-dependent rules
// (destination of a removal, kind of a service order, typing money) are checked again in the services.

[ApiController]
[Route("api/v1/tire-models")]
public sealed class TireModelsController(TireModelService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Tires.View, Permissions.Tires.Create, Permissions.Tires.ManageSettings)]
    public Task<IReadOnlyList<TireModelResponse>> List([FromQuery] bool includeInactive, CancellationToken ct) => service.ListAsync(includeInactive, ct);

    [HttpGet("brands"), HasPermission(Permissions.Tires.View)]
    public Task<IReadOnlyList<string>> Brands(CancellationToken ct) => service.BrandsAsync(ct);

    /// <summary>Whoever registers tires may add the model they are holding (seção 3); editing the catalog is configuration.</summary>
    [HttpPost, HasPermission(Permissions.Tires.Create, Permissions.Tires.ManageSettings)]
    public async Task<ActionResult<TireModelResponse>> Create(TireModelRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Tires.ManageSettings)]
    public Task<TireModelResponse> Update(Guid id, TireModelRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Tires.ManageSettings)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/tire-layouts")]
public sealed class TireLayoutsController(TireLayoutService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Tires.View, Permissions.Tires.ManageSettings)]
    public Task<IReadOnlyList<TireLayoutResponse>> List([FromQuery] TireLayoutTarget? target, [FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(target, includeInactive, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Tires.View, Permissions.Tires.ManageSettings)]
    public Task<TireLayoutResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Tires.ManageSettings)]
    public async Task<ActionResult<TireLayoutResponse>> Create(TireLayoutRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Tires.ManageSettings)]
    public Task<TireLayoutResponse> Update(Guid id, TireLayoutRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Tires.ManageSettings)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/tire-settings")]
public sealed class TireSettingsController(TireSettingsService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Tires.View, Permissions.Tires.ManageSettings)]
    public Task<TireSettingsResponse> Get(CancellationToken ct) => service.GetAsync(ct);

    [HttpPut, HasPermission(Permissions.Tires.ManageSettings)]
    public Task<TireSettingsResponse> Update(TireSettingsRequest request, CancellationToken ct) => service.UpdateAsync(request, ct);
}

[ApiController]
[Route("api/v1/tires")]
public sealed class TiresController(
    TireService tires,
    TireOperationsService operations,
    TireInspectionService inspections,
    TireServiceOrderService services,
    TireAnalyticsService analytics) : ControllerBase
{
    // ---- registry ----

    [HttpGet, HasPermission(Permissions.Tires.View)]
    public Task<PagedResult<TireListItemResponse>> List([FromQuery] TireListRequest request, CancellationToken ct) => tires.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Tires.View)]
    public Task<TireResponse> Get(Guid id, CancellationToken ct) => tires.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Tires.Create)]
    public async Task<ActionResult<TireResponse>> Create(TireRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await tires.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Tires.Edit)]
    public Task<TireResponse> Update(Guid id, TireRequest request, CancellationToken ct) => tires.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Tires.Edit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tires.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/history"), HasPermission(Permissions.Tires.View)]
    public Task<PagedResult<HistoryEntryResponse>> History(Guid id, [FromQuery] HistoryRequest request, CancellationToken ct) =>
        tires.HistoryAsync(id, request, ct);

    [HttpGet("{id:guid}/installations"), HasPermission(Permissions.Tires.View)]
    public Task<PagedResult<TireInstallationResponse>> Installations(Guid id, [FromQuery] ListRequest request, CancellationToken ct) =>
        tires.InstallationsAsync(id, request, ct);

    [HttpGet("{id:guid}/compatibility"), HasPermission(Permissions.Tires.Install, Permissions.Tires.Rotate)]
    public Task<TireCompatibilityResponse> Compatibility(Guid id, [FromQuery] Guid? vehicleId, [FromQuery] Guid? implementId,
        [FromQuery] string? positionCode, CancellationToken ct) => tires.CompatibilityAsync(id, vehicleId, implementId, positionCode, ct);

    // ---- lifecycle operations ----

    [HttpPost("{id:guid}/install"), HasPermission(Permissions.Tires.Install)]
    public Task<TireResponse> Install(Guid id, TireInstallRequest request, CancellationToken ct) => operations.InstallAsync(id, request, ct);

    [HttpPost("{id:guid}/remove"), HasPermission(Permissions.Tires.Remove)]
    public Task<TireResponse> Remove(Guid id, TireRemovalRequest request, CancellationToken ct) => operations.RemoveAsync(id, request, ct);

    /// <summary>Removal of this tire + installation of the replacement (the service also requires tires.remove).</summary>
    [HttpPost("{id:guid}/replace"), HasPermission(Permissions.Tires.Install)]
    public Task<TireResponse> Replace(Guid id, TireReplaceRequest request, CancellationToken ct) => operations.ReplaceAsync(id, request, ct);

    [HttpPost("{id:guid}/transfer"), HasPermission(Permissions.Tires.Install)]
    public Task<TireResponse> Transfer(Guid id, TireTransferRequest request, CancellationToken ct) => operations.TransferAsync(id, request, ct);

    [HttpPost("{id:guid}/evaluation"), HasPermission(Permissions.Tires.Inspect)]
    public Task<TireResponse> SendToEvaluation(Guid id, TireStockRequest request, CancellationToken ct) =>
        operations.SendToEvaluationAsync(id, request, ct);

    [HttpPost("{id:guid}/return-to-stock"), HasPermission(Permissions.Tires.Inspect)]
    public Task<TireResponse> ReturnToStock(Guid id, TireStockRequest request, CancellationToken ct) => operations.ReturnToStockAsync(id, request, ct);

    [HttpPost("{id:guid}/dispose"), HasPermission(Permissions.Tires.Dispose)]
    public Task<TireResponse> DisposeTire(Guid id, TireDisposalRequest request, CancellationToken ct) => operations.DisposeAsync(id, request, ct);

    // ---- inspections ----

    [HttpGet("{id:guid}/inspections"), HasPermission(Permissions.Tires.View)]
    public Task<PagedResult<TireInspectionResponse>> Inspections(Guid id, [FromQuery] ListRequest request, CancellationToken ct) =>
        inspections.ListAsync(id, request, ct);

    [HttpPost("{id:guid}/inspections"), HasPermission(Permissions.Tires.Inspect)]
    public async Task<ActionResult<TireInspectionResponse>> Inspect(Guid id, TireInspectionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await inspections.InspectAsync(id, request, ct));

    // ---- repairs, retreads, costs ----

    [HttpGet("{id:guid}/service-orders"), HasPermission(Permissions.Tires.View)]
    public Task<IReadOnlyList<TireServiceOrderResponse>> ServiceOrders(Guid id, CancellationToken ct) => services.ListAsync(id, ct);

    /// <summary>Repair or retread: the service checks the permission of the kind requested.</summary>
    [HttpPost("{id:guid}/service-orders"), HasPermission(Permissions.Tires.Repair, Permissions.Tires.Retread)]
    public async Task<ActionResult<TireServiceOrderResponse>> SendToService(Guid id, TireServiceRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await services.SendAsync(id, request, ct));

    [HttpGet("{id:guid}/costs"), HasPermission(Permissions.Tires.ViewCosts)]
    public Task<IReadOnlyList<TireCostResponse>> Costs(Guid id, CancellationToken ct) => services.ListCostsAsync(id, ct);

    [HttpPost("{id:guid}/costs"), HasPermission(Permissions.Tires.ViewCosts)]
    public Task<TireResponse> AddCost(Guid id, TireCostRequest request, CancellationToken ct) => services.AddCostAsync(id, request, ct);

    [HttpDelete("{id:guid}/costs/{costId:guid}"), HasPermission(Permissions.Tires.ViewCosts)]
    public Task<TireResponse> DeleteCost(Guid id, Guid costId, CancellationToken ct) => services.DeleteCostAsync(id, costId, ct);

    // ---- dashboard and reports ----

    [HttpGet("dashboard"), HasPermission(Permissions.Tires.View)]
    public Task<TireDashboardResponse> Dashboard(CancellationToken ct) => analytics.GetDashboardAsync(ct);

    [HttpGet("reports/inventory"), HasPermission(Permissions.Tires.View)]
    public Task<TireReportPage<TireListItemResponse>> Inventory([FromQuery] TireReportRequest request, CancellationToken ct) =>
        analytics.InventoryAsync(request, ct);

    [HttpGet("reports/lifecycle"), HasPermission(Permissions.Tires.View)]
    public Task<TireReportPage<TireLifecycleReportRow>> Lifecycle([FromQuery] TireReportRequest request, CancellationToken ct) =>
        analytics.LifecycleAsync(request, ct);

    [HttpGet("reports/inspections"), HasPermission(Permissions.Tires.View)]
    public Task<TireReportPage<TireInspectionReportRow>> InspectionReport([FromQuery] TireReportRequest request, CancellationToken ct) =>
        analytics.InspectionsAsync(request, ct);

    [HttpGet("reports/costs"), HasPermission(Permissions.Tires.ViewCosts)]
    public Task<TireReportPage<TireCostReportRow>> CostReport([FromQuery] TireReportRequest request, CancellationToken ct) =>
        analytics.CostsAsync(request, ct);
}

[ApiController]
[Route("api/v1")]
public sealed class TireOperationsController(
    TireOperationsService operations,
    TireServiceOrderService services,
    TireInspectionService inspections) : ControllerBase
{
    [HttpPost("tire-rotations"), HasPermission(Permissions.Tires.Rotate)]
    public Task<AssetTiresResponse> Rotate(TireRotationRequest request, CancellationToken ct) => operations.RotateAsync(request, ct);

    [HttpPost("tire-installations/{id:guid}/correct"), HasPermission(Permissions.Tires.Edit)]
    public Task<TireInstallationResponse> CorrectInstallation(Guid id, TireInstallationCorrectionRequest request, CancellationToken ct) =>
        operations.CorrectInstallationAsync(id, request, ct);

    [HttpPost("tire-service-orders/{id:guid}/complete"), HasPermission(Permissions.Tires.Repair, Permissions.Tires.Retread)]
    public Task<TireServiceOrderResponse> CompleteService(Guid id, TireServiceCompletionRequest request, CancellationToken ct) =>
        services.CompleteAsync(id, request, ct);

    [HttpPost("tire-service-orders/{id:guid}/cancel"), HasPermission(Permissions.Tires.Repair, Permissions.Tires.Retread)]
    public Task<TireServiceOrderResponse> CancelService(Guid id, TireServiceCancelRequest request, CancellationToken ct) =>
        services.CancelAsync(id, request, ct);

    [HttpPost("tire-anomalies/{id:guid}/review"), HasPermission(Permissions.Tires.Edit)]
    public Task<TireResponse> ReviewAnomaly(Guid id, TireAnomalyReviewRequest request, CancellationToken ct) =>
        inspections.ReviewAnomalyAsync(id, request, ct);

    // ---- the tires of a vehicle / implement ----

    [HttpGet("vehicles/{vehicleId:guid}/tires"), HasPermission(Permissions.Tires.View)]
    public Task<AssetTiresResponse> VehicleTires(Guid vehicleId, CancellationToken ct) => operations.GetAssetAsync(vehicleId, null, ct);

    [HttpPut("vehicles/{vehicleId:guid}/tires/layout"), HasPermission(Permissions.Tires.ManageSettings)]
    public Task<AssetTiresResponse> SetVehicleLayout(Guid vehicleId, TireLayoutAssignmentRequest request, CancellationToken ct) =>
        operations.SetLayoutAsync(vehicleId, null, request, ct);

    [HttpGet("implements/{implementId:guid}/tires"), HasPermission(Permissions.Tires.View)]
    public Task<AssetTiresResponse> ImplementTires(Guid implementId, CancellationToken ct) => operations.GetAssetAsync(null, implementId, ct);

    [HttpPut("implements/{implementId:guid}/tires/layout"), HasPermission(Permissions.Tires.ManageSettings)]
    public Task<AssetTiresResponse> SetImplementLayout(Guid implementId, TireLayoutAssignmentRequest request, CancellationToken ct) =>
        operations.SetLayoutAsync(null, implementId, request, ct);
}
