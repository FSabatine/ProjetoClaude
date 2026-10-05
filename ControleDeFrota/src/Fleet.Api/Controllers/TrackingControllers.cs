using Fleet.Api.Authorization;
using Fleet.Application.Tracking;
using Fleet.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fleet.Api.Controllers;

// Final phase, stage D — tracking foundation and integrations (ADR-051).

[ApiController]
[Route("api/v1/tracking")]
public sealed class TrackingController(
    TrackingProviderService providers, TrackingDeviceService devices, TrackingQueryService queries, TrackingIngestionService ingestion) : ControllerBase
{
    public const string IngestRateLimitPolicy = "tracking-ingest";
    public const string DeviceKeyHeader = "X-Device-Key";

    /// <summary>
    /// Trackers push positions here. Authenticated by the device key (header), not by a user token — the only
    /// non-user endpoint besides login/refresh. Invalid key = 401; rate limited per key.
    /// </summary>
    [HttpPost("ingest"), AllowAnonymous, EnableRateLimiting(IngestRateLimitPolicy)]
    public Task<IngestResponse> Ingest([FromHeader(Name = DeviceKeyHeader)] string? deviceKey, IngestRequest request, CancellationToken ct) =>
        ingestion.IngestAsync(deviceKey, request, ct);

    [HttpGet("fleet"), HasPermission(Permissions.Tracking.View)]
    public Task<IReadOnlyList<VehicleLocation>> Fleet(CancellationToken ct) => queries.FleetAsync(ct);

    [HttpGet("providers"), HasPermission(Permissions.Tracking.Manage)]
    public Task<IReadOnlyList<TrackingProviderResponse>> Providers(CancellationToken ct) => providers.ListAsync(ct);

    [HttpPost("providers"), HasPermission(Permissions.Tracking.Manage)]
    public async Task<ActionResult<TrackingProviderResponse>> CreateProvider(TrackingProviderRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await providers.SaveAsync(null, request, ct));

    [HttpPut("providers/{id:guid}"), HasPermission(Permissions.Tracking.Manage)]
    public Task<TrackingProviderResponse> UpdateProvider(Guid id, TrackingProviderRequest request, CancellationToken ct) => providers.SaveAsync(id, request, ct);

    [HttpDelete("providers/{id:guid}"), HasPermission(Permissions.Tracking.Manage)]
    public async Task<IActionResult> DeleteProvider(Guid id, CancellationToken ct)
    {
        await providers.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("devices"), HasPermission(Permissions.Tracking.Manage)]
    public Task<IReadOnlyList<TrackingDeviceResponse>> Devices(CancellationToken ct) => devices.ListAsync(ct);

    [HttpPost("devices"), HasPermission(Permissions.Tracking.Manage)]
    public async Task<ActionResult<TrackingDeviceKeyResponse>> CreateDevice(TrackingDeviceRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await devices.CreateAsync(request, ct));

    [HttpPut("devices/{id:guid}"), HasPermission(Permissions.Tracking.Manage)]
    public Task<TrackingDeviceResponse> UpdateDevice(Guid id, TrackingDeviceRequest request, CancellationToken ct) => devices.UpdateAsync(id, request, ct);

    [HttpPost("devices/{id:guid}/rotate-key"), HasPermission(Permissions.Tracking.Manage)]
    public Task<TrackingDeviceKeyResponse> RotateKey(Guid id, CancellationToken ct) => devices.RotateKeyAsync(id, ct);

    [HttpPost("devices/{id:guid}/vehicle"), HasPermission(Permissions.Tracking.Manage)]
    public Task<TrackingDeviceResponse> LinkVehicle(Guid id, LinkVehicleRequest request, CancellationToken ct) => devices.LinkAsync(id, request, ct);

    [HttpDelete("devices/{id:guid}"), HasPermission(Permissions.Tracking.Manage)]
    public async Task<IActionResult> DeleteDevice(Guid id, CancellationToken ct)
    {
        await devices.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/vehicles/{vehicleId:guid}/track")]
public sealed class VehicleTrackController(TrackingQueryService queries) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Tracking.View)]
    public Task<VehicleTrackResponse> Get(Guid vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        queries.TrackAsync(vehicleId, from, to, ct);
}

[ApiController]
[Route("api/v1/integrations")]
public sealed class IntegrationsController(IntegrationStatusService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Tracking.Manage, Permissions.Automation.Manage)]
    public Task<IReadOnlyList<IntegrationStatus>> Get(CancellationToken ct) => service.GetAsync(ct);
}
