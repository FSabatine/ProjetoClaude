using Fleet.Api.Authorization;
using Fleet.Application.Audit;
using Fleet.Application.Common;
using Fleet.Application.Companies;
using Fleet.Application.Dashboard;
using Fleet.Application.Roles;
using Fleet.Application.Users;
using Fleet.Domain.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Controllers;

[ApiController]
[Route("api/v1/companies")]
public sealed class CompaniesController(CompanyService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Companies.Manage)]
    public Task<PagedResult<CompanyListItemResponse>> List([FromQuery] CompanyListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("current"), HasPermission(Permissions.Companies.View, Permissions.Companies.Manage)]
    public Task<CompanyResponse> GetCurrent(CancellationToken ct) => service.GetCurrentAsync(ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Companies.View, Permissions.Companies.Manage)]
    public Task<CompanyResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Companies.Manage)]
    public async Task<ActionResult<CompanyResponse>> Create(CompanyRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Companies.Update, Permissions.Companies.Manage)]
    public Task<CompanyResponse> Update(Guid id, CompanyRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Companies.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(UserService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Users.View)]
    public Task<PagedResult<UserListItemResponse>> List([FromQuery] UserListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Users.View)]
    public Task<UserResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Users.Manage)]
    public async Task<ActionResult<UserResponse>> Create(UserCreateRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Users.Manage)]
    public Task<UserResponse> Update(Guid id, UserUpdateRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/reset-password"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken ct)
    {
        await service.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1")]
public sealed class RolesController(RoleService service) : ControllerBase
{
    /// <summary>Also readable with users.manage, because the user form needs the role list.</summary>
    [HttpGet("roles"), HasPermission(Permissions.Roles.View, Permissions.Users.Manage)]
    public Task<IReadOnlyList<RoleResponse>> ListRoles(CancellationToken ct) => service.ListAsync(ct);

    [HttpGet("permissions"), HasPermission(Permissions.Roles.View)]
    public Task<IReadOnlyList<PermissionResponse>> ListPermissions(CancellationToken ct) => service.ListPermissionsAsync(ct);
}

[ApiController]
[Route("api/v1/dashboard")]
public sealed class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Dashboard.View)]
    public Task<DashboardResponse> Get(CancellationToken ct) => service.GetAsync(ct);
}

[ApiController]
[Route("api/v1/audit")]
public sealed class AuditController(AuditService service) : ControllerBase
{
    private static readonly HashSet<string> AuditedEntities =
    [
        "Company", "User", "Driver", "Vehicle", "Implement",
        "VehicleAssignment", "OdometerReading", "DocumentType", "Document", "StoredFile", "ChecklistTemplate", "ChecklistExecution", "Occurrence",
    ];

    [HttpGet("{entityName}/{entityId:guid}"), HasPermission(Permissions.Audit.View)]
    public async Task<ActionResult<IReadOnlyList<AuditEntryResponse>>> GetHistory(string entityName, Guid entityId, CancellationToken ct)
    {
        var name = AuditedEntities.FirstOrDefault(e => e.Equals(entityName, StringComparison.OrdinalIgnoreCase));
        if (name is null) throw new NotFoundException("Histórico não disponível para este tipo de registro.");
        return Ok(await service.GetHistoryAsync(name, entityId, ct));
    }
}
