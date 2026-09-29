using Fleet.Domain.Common;
using Fleet.Domain.Companies;

namespace Fleet.Domain.Users;

public enum UserStatus
{
    Active,
    Inactive,
}

/// <summary>
/// Not ITenantScoped on purpose: login must find the user by e-mail before the tenant is known.
/// UserService filters by company explicitly.
/// </summary>
public class User : AuditableEntity, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 150;

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    /// <summary>Login; lowercase, unique across the whole system.</summary>
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Active;

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }

    public List<UserRole> UserRoles { get; set; } = [];

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public bool IsLockedOut(DateTime utcNow) => LockoutEndAt is { } end && end > utcNow;
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
