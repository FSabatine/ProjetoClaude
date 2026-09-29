using Fleet.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Roles;

public sealed record PermissionResponse(int Id, string Key, string Module, string Description);

public sealed record RoleResponse(
    int Id,
    string Key,
    string Name,
    string Description,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    // False when the role grants something the caller doesn't have (anti-escalation); the UI disables it.
    bool IsAssignable);

/// <summary>Read-only in Phase 1: roles and permissions are seeded system data.</summary>
public sealed class RoleService(IFleetDbContext db, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken ct)
    {
        var roles = await db.Roles
            .Where(r => r.CompanyId == null || r.CompanyId == currentUser.CompanyId)
            .OrderBy(r => r.Id)
            .Select(r => new
            {
                r.Id, r.Key, r.Name, r.Description, r.IsSystem,
                Permissions = r.RolePermissions.Select(rp => rp.Permission.Key).OrderBy(k => k).ToList(),
            })
            .ToListAsync(ct);

        return roles.Select(r => new RoleResponse(
                r.Id, r.Key, r.Name, r.Description, r.IsSystem, r.Permissions,
                r.Permissions.All(currentUser.HasPermission)))
            .ToList();
    }

    public async Task<IReadOnlyList<PermissionResponse>> ListPermissionsAsync(CancellationToken ct) =>
        await db.Permissions
            .OrderBy(p => p.Id)
            .Select(p => new PermissionResponse(p.Id, p.Key, p.Module, p.Description))
            .ToListAsync(ct);
}
