using Fleet.Application.Common;
using Fleet.Domain.Authorization;

namespace Fleet.Api.Authorization;

/// <summary>
/// The ICurrentUser of the API: the JWT caller, or — only inside a scope a background job opened with
/// <see cref="SystemExecutionContext.ActAsSystemFor"/> — the system acting for one company with every permission
/// (ADR-045). Read on every access, so the tenant filter of a DbContext created earlier in the scope follows it.
/// </summary>
public sealed class SystemAwareCurrentUser(HttpCurrentUser http, SystemExecutionContext system) : ICurrentUser
{
    private static readonly IReadOnlySet<string> AllPermissions =
        PermissionCatalog.All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    public Guid? UserId => system.IsActive ? null : http.UserId;
    public Guid? CompanyId => system.IsActive ? system.CompanyId : http.CompanyId;
    public IReadOnlySet<string> Permissions => system.IsActive ? AllPermissions : http.Permissions;
    public string? TraceId => http.TraceId;
}
