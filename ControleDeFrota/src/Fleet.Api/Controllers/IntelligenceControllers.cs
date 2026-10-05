using Fleet.Api.Authorization;
using Fleet.Api.Infrastructure;
using Fleet.Application.Common;
using Fleet.Application.Intelligence;
using Fleet.Domain.Authorization;
using Fleet.Domain.Intelligence;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Controllers;

// Final phase — alerts, notifications and automation (ADR-045). Thin controllers; audience filtering lives in the services.

[ApiController]
[Route("api/v1/alerts")]
public sealed class AlertsController(FleetAlertService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Alerts.View)]
    public Task<PagedResult<FleetAlertResponse>> List([FromQuery] FleetAlertListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("summary"), HasPermission(Permissions.Alerts.View)]
    public Task<FleetAlertSummary> Summary(CancellationToken ct) => service.SummaryAsync(ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Alerts.View)]
    public Task<FleetAlertResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost("{id:guid}/read"), HasPermission(Permissions.Alerts.View)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/status"), HasPermission(Permissions.Alerts.Manage)]
    public Task<FleetAlertResponse> ChangeStatus(Guid id, FleetAlertStatusRequest request, CancellationToken ct) =>
        service.ChangeStatusAsync(id, request, ct);
}

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Alerts.View)]
    public Task<NotificationPage> List([FromQuery] bool unreadOnly, [FromQuery] int page, CancellationToken ct) =>
        service.ListAsync(unreadOnly, page, ct);

    [HttpGet("unread-count"), HasPermission(Permissions.Alerts.View)]
    public Task<int> UnreadCount(CancellationToken ct) => service.UnreadCountAsync(ct);

    [HttpPost("{id:guid}/read"), HasPermission(Permissions.Alerts.View)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("read-all"), HasPermission(Permissions.Alerts.View)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await service.MarkAllReadAsync(ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/automation")]
public sealed class AutomationController(AutomationRuleService service, AutomationRunner runner, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("catalog"), HasPermission(Permissions.Automation.Manage, Permissions.Alerts.View)]
    public AutomationCatalogResponse Catalog() => AutomationRuleService.Catalog();

    [HttpGet("rules"), HasPermission(Permissions.Automation.Manage)]
    public Task<IReadOnlyList<AutomationRuleResponse>> List(CancellationToken ct) => service.ListAsync(ct);

    [HttpGet("rules/{id:guid}"), HasPermission(Permissions.Automation.Manage)]
    public Task<AutomationRuleResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpGet("rules/{id:guid}/executions"), HasPermission(Permissions.Automation.Manage)]
    public Task<IReadOnlyList<AutomationExecutionResponse>> Executions(Guid id, CancellationToken ct) => service.ExecutionsAsync(id, ct);

    [HttpPost("rules"), HasPermission(Permissions.Automation.Manage)]
    public async Task<ActionResult<AutomationRuleResponse>> Create(AutomationRuleRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("rules/{id:guid}"), HasPermission(Permissions.Automation.Manage)]
    public Task<AutomationRuleResponse> Update(Guid id, AutomationRuleRequest request, CancellationToken ct) =>
        service.UpdateAsync(id, request, ct);

    [HttpDelete("rules/{id:guid}"), HasPermission(Permissions.Automation.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Runs the scan now for the caller's company (the scheduled job does the same every hour).</summary>
    [HttpPost("run"), HasPermission(Permissions.Automation.Manage)]
    public Task<AutomationRunResult> Run(CancellationToken ct) =>
        runner.RunForCompanyAsync(currentUser.CompanyId!.Value, AutomationRunSource.Manual, ct);
}

[ApiController]
[Route("api/v1/dashboard/attention")]
public sealed class AttentionController(AttentionService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Dashboard.View)]
    public Task<AttentionResponse> Get(CancellationToken ct) => service.GetAsync(ct);
}
