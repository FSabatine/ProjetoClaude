using Fleet.Api.Authorization;
using Fleet.Application.Analytics;
using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Controllers;

// Final phase, stage B — cross-module analytics. Each figure inside is gated by its own module permission in the services.

[ApiController]
[Route("api/v1/analytics")]
public sealed class AnalyticsController(FleetReportsService reports, InsightService insights) : ControllerBase
{
    [HttpGet("fleet-performance"), HasPermission(Permissions.Vehicles.View)]
    public Task<PagedResult<VehicleMetrics>> FleetPerformance([FromQuery] FleetPerformanceRequest request, CancellationToken ct) =>
        reports.FleetPerformanceAsync(request, ct);

    [HttpGet("recurring-problems"), HasPermission(Permissions.Maintenance.View)]
    public Task<IReadOnlyList<RecurringProblemRow>> RecurringProblems([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        reports.RecurringProblemsAsync(new PeriodRequest(from, to), ct);

    [HttpGet("compare"), HasPermission(Permissions.Vehicles.View)]
    public Task<VehicleComparisonResponse> Compare(
        [FromQuery] Guid[] vehicleIds, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        reports.CompareAsync(vehicleIds, from, to, ct);

    [HttpGet("insights"), HasPermission(Permissions.Dashboard.View)]
    public Task<IReadOnlyList<Insight>> Insights(CancellationToken ct) => insights.GetAsync(ct);
}

[ApiController]
[Route("api/v1/vehicles/{vehicleId:guid}/health")]
public sealed class VehicleHealthController(VehicleHealthService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Vehicles.View)]
    public Task<VehicleHealthResponse> Get(Guid vehicleId, CancellationToken ct) => service.GetAsync(vehicleId, ct);
}

[ApiController]
[Route("api/v1/search")]
public sealed class SearchController(GlobalSearchService service) : ControllerBase
{
    /// <summary>Any of these lets a user search; each result type is still filtered by its own permission.</summary>
    [HttpGet, HasPermission(Permissions.Dashboard.View, Permissions.Vehicles.View, Permissions.Drivers.View)]
    public Task<SearchResponse> Search([FromQuery] string? q, CancellationToken ct) => service.SearchAsync(q, ct);
}
