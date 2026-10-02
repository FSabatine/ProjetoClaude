using Fleet.Api.Authorization;
using Fleet.Application.Common;
using Fleet.Application.Fuel;
using Fleet.Domain.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Controllers;

// Phase 4 — fuel. Same shape as the other modules: permission gate + service call, no logic here.
// Money visibility (fuel.viewcosts or the record's author) is decided in the services, per record.

[ApiController]
[Route("api/v1/fuel-types")]
public sealed class FuelTypesController(FuelTypeService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Fuel.View, Permissions.Fuel.Create, Permissions.Fuel.Configure)]
    public Task<IReadOnlyList<FuelTypeResponse>> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(includeInactive, ct);

    [HttpPost, HasPermission(Permissions.Fuel.Configure)]
    public async Task<ActionResult<FuelTypeResponse>> Create(FuelTypeRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Fuel.Configure)]
    public Task<FuelTypeResponse> Update(Guid id, FuelTypeRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Fuel.Configure)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/fuel-stations")]
public sealed class FuelStationsController(FuelStationService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Fuel.View, Permissions.Fuel.Create, Permissions.Fuel.ManageStations)]
    public Task<PagedResult<FuelStationResponse>> List([FromQuery] FuelStationListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Fuel.View, Permissions.Fuel.ManageStations)]
    public Task<FuelStationResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Fuel.ManageStations)]
    public async Task<ActionResult<FuelStationResponse>> Create(FuelStationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Fuel.ManageStations)]
    public Task<FuelStationResponse> Update(Guid id, FuelStationRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Fuel.ManageStations)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Reference prices can be negotiated values: cost permission or station management.</summary>
    [HttpGet("{id:guid}/prices"), HasPermission(Permissions.Fuel.ViewCosts, Permissions.Fuel.ManageStations)]
    public Task<IReadOnlyList<FuelPriceResponse>> ListPrices(Guid id, CancellationToken ct) => service.ListPricesAsync(id, ct);

    [HttpPost("{id:guid}/prices"), HasPermission(Permissions.Fuel.ManageStations)]
    public async Task<ActionResult<FuelPriceResponse>> AddPrice(Guid id, FuelPriceRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.AddPriceAsync(id, request, ct));

    [HttpDelete("{id:guid}/prices/{priceId:guid}"), HasPermission(Permissions.Fuel.ManageStations)]
    public async Task<IActionResult> DeletePrice(Guid id, Guid priceId, CancellationToken ct)
    {
        await service.DeletePriceAsync(id, priceId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/fuel-settings")]
public sealed class FuelSettingsController(FuelSettingsService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Fuel.View, Permissions.Fuel.Configure)]
    public Task<FuelSettingsResponse> Get(CancellationToken ct) => service.GetAsync(ct);

    [HttpPut, HasPermission(Permissions.Fuel.Configure)]
    public Task<FuelSettingsResponse> Update(FuelSettingsRequest request, CancellationToken ct) => service.UpdateAsync(request, ct);
}

[ApiController]
[Route("api/v1/fuelings")]
public sealed class FuelingsController(FuelingService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Fuel.View)]
    public Task<PagedResult<FuelingListItemResponse>> List([FromQuery] FuelingListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Fuel.View)]
    public Task<FuelingResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpGet("form-defaults"), HasPermission(Permissions.Fuel.Create, Permissions.Fuel.Correct)]
    public Task<FuelingFormDefaultsResponse> FormDefaults([FromQuery] Guid vehicleId, CancellationToken ct) =>
        service.GetFormDefaultsAsync(vehicleId, ct);

    /// <summary>204 when there is no known price for the station and product.</summary>
    [HttpGet("price-hint"), HasPermission(Permissions.Fuel.Create, Permissions.Fuel.Correct)]
    public Task<FuelPriceHintResponse?> PriceHint([FromQuery] Guid fuelStationId, [FromQuery] Guid fuelTypeId, CancellationToken ct) =>
        service.GetPriceHintAsync(fuelStationId, fuelTypeId, ct);

    [HttpPost, HasPermission(Permissions.Fuel.Create)]
    public async Task<ActionResult<FuelingResponse>> Create(FuelingRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPost("{id:guid}/correct"), HasPermission(Permissions.Fuel.Correct)]
    public Task<FuelingResponse> Correct(Guid id, FuelingCorrectionRequest request, CancellationToken ct) => service.CorrectAsync(id, request, ct);

    [HttpPost("{id:guid}/cancel"), HasPermission(Permissions.Fuel.Cancel)]
    public Task<FuelingResponse> Cancel(Guid id, FuelingReasonRequest request, CancellationToken ct) => service.CancelAsync(id, request, ct);

    [HttpPost("{id:guid}/review"), HasPermission(Permissions.Fuel.ReviewAnomalies)]
    public Task<FuelingResponse> Review(Guid id, FuelingReasonRequest request, CancellationToken ct) => service.ReviewAsync(id, request, ct);

    [HttpPost("{id:guid}/files"), HasPermission(Permissions.Fuel.Create, Permissions.Fuel.Correct)]
    public Task<FuelingResponse> AttachFiles(Guid id, FuelingFilesRequest request, CancellationToken ct) => service.AttachFilesAsync(id, request, ct);
}

[ApiController]
[Route("api/v1")]
public sealed class FuelAnalyticsController(FuelAnalyticsService service) : ControllerBase
{
    [HttpGet("fuel/dashboard"), HasPermission(Permissions.Fuel.View)]
    public Task<FuelDashboardResponse> Dashboard([FromQuery] FuelPeriodRequest request, CancellationToken ct) =>
        service.GetDashboardAsync(request, ct);

    [HttpGet("vehicles/{vehicleId:guid}/fuel"), HasPermission(Permissions.Fuel.View)]
    public Task<VehicleFuelResponse> Vehicle(Guid vehicleId, [FromQuery] VehicleFuelRequest request, CancellationToken ct) =>
        service.GetVehicleAsync(vehicleId, request, ct);

    [HttpGet("fuel/reports/consumption"), HasPermission(Permissions.Fuel.View)]
    public Task<FuelReportPage<ConsumptionReportRow>> Consumption([FromQuery] FuelReportRequest request, CancellationToken ct) =>
        service.GetConsumptionReportAsync(request, ct);

    [HttpGet("fuel/reports/costs"), HasPermission(Permissions.Fuel.ViewCosts)]
    public Task<FuelReportPage<CostReportRow>> Costs([FromQuery] FuelReportRequest request, CancellationToken ct) =>
        service.GetCostReportAsync(request, ct);

    [HttpGet("fuel/reports/stations"), HasPermission(Permissions.Fuel.View)]
    public Task<FuelReportPage<StationReportRow>> Stations([FromQuery] FuelReportRequest request, CancellationToken ct) =>
        service.GetStationReportAsync(request, ct);

    [HttpGet("fuel/reports/prices"), HasPermission(Permissions.Fuel.ViewCosts)]
    public Task<FuelReportPage<PriceReportRow>> Prices([FromQuery] FuelReportRequest request, CancellationToken ct) =>
        service.GetPriceReportAsync(request, ct);
}
