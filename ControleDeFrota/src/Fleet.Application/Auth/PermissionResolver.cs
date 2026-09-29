using Fleet.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Auth;

/// <summary>
/// Single source of the rule "effective permissions = union of the permissions of all the user's roles".
/// Used by login, refresh, profile and the anti-escalation checks — never reimplement it.
/// </summary>
public static class PermissionResolver
{
    public static async Task<IReadOnlyList<string>> GetForUserAsync(IFleetDbContext db, Guid userId, CancellationToken ct) =>
        await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Key))
            .Distinct()
            .OrderBy(k => k)
            .ToListAsync(ct);

    public static async Task<IReadOnlyList<string>> GetForRolesAsync(IFleetDbContext db, IEnumerable<int> roleIds, CancellationToken ct)
    {
        var ids = roleIds.ToList();
        return await db.RolePermissions
            .Where(rp => ids.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);
    }
}
