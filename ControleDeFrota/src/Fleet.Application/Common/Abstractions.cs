using Fleet.Domain.Auditing;
using Fleet.Domain.Companies;
using Fleet.Domain.Drivers;
using Fleet.Domain.Implements;
using Fleet.Domain.Users;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Common;

/// <summary>
/// Persistence boundary used directly by services (ADR-004). Tenant and soft-delete filters are
/// applied by the implementation, so queries here never need to filter by CompanyId or DeletedAt.
/// </summary>
public interface IFleetDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<Implement> Implements { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The authenticated caller. Never trust company/user ids sent by the client — use this.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? CompanyId { get; }
    IReadOnlySet<string> Permissions { get; }
    string? TraceId { get; }

    bool HasPermission(string permission) => Permissions.Contains(permission);
}

public interface IClock
{
    DateTime UtcNow { get; }
    /// <summary>Business date in Brazil (America/Sao_Paulo) — used for license expiry, ages etc.</summary>
    DateOnly Today { get; }
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordCheck Verify(string hash, string password);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, IReadOnlyCollection<string> permissions);
    /// <summary>Cryptographically random opaque token (sent to the browser in an HttpOnly cookie).</summary>
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
    TimeSpan RefreshTokenLifetime { get; }
}
