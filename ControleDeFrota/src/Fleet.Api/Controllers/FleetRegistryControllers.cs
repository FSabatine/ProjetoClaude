using Fleet.Api.Authorization;
using Fleet.Application.Common;
using Fleet.Application.Drivers;
using Fleet.Application.Implements;
using Fleet.Application.Vehicles;
using Fleet.Domain.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
public sealed class VehiclesController(VehicleService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Vehicles.View)]
    public Task<PagedResult<VehicleListItemResponse>> List([FromQuery] VehicleListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Vehicles.View)]
    public Task<VehicleResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Vehicles.Create)]
    public async Task<ActionResult<VehicleResponse>> Create(VehicleRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Vehicles.Update)]
    public Task<VehicleResponse> Update(Guid id, VehicleRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Vehicles.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/implements")]
public sealed class ImplementsController(ImplementService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Implements.View)]
    public Task<PagedResult<ImplementListItemResponse>> List([FromQuery] ImplementListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Implements.View)]
    public Task<ImplementResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Implements.Create)]
    public async Task<ActionResult<ImplementResponse>> Create(ImplementRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Implements.Update)]
    public Task<ImplementResponse> Update(Guid id, ImplementRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Implements.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/drivers")]
public sealed class DriversController(DriverService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Drivers.View)]
    public Task<PagedResult<DriverListItemResponse>> List([FromQuery] DriverListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Drivers.View)]
    public Task<DriverResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Drivers.Create)]
    public async Task<ActionResult<DriverResponse>> Create(DriverRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Drivers.Update)]
    public Task<DriverResponse> Update(Guid id, DriverRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Drivers.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
