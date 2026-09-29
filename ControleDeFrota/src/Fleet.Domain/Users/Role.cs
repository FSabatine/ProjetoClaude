namespace Fleet.Domain.Users;

/// <summary>
/// Named set of permissions. Authorization never checks the role itself — only its permissions.
/// CompanyId == null means a system role; the column exists for future per-company custom roles.
/// </summary>
public class Role
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public Guid? CompanyId { get; set; }

    public List<RolePermission> RolePermissions { get; set; } = [];
}

public class Permission
{
    public int Id { get; set; }
    /// <summary>"module.action", e.g. "vehicles.update".</summary>
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

/// <summary>Only the SHA-256 hash is stored; the raw token lives in an HttpOnly cookie (ADR-007).</summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
