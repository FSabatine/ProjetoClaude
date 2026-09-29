using System.Diagnostics;
using Fleet.Application.Common;
using Fleet.Infrastructure.Security;

namespace Fleet.Api.Authorization;

/// <summary>ICurrentUser backed by the validated JWT of the current request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private IReadOnlySet<string>? _permissions;

    private System.Security.Claims.ClaimsPrincipal? Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;

    public Guid? UserId => ParseGuid(FleetClaims.UserId);

    public Guid? CompanyId => ParseGuid(FleetClaims.CompanyId);

    public IReadOnlySet<string> Permissions => _permissions ??=
        Principal?.FindAll(FleetClaims.Permission).Select(c => c.Value).ToHashSet(StringComparer.Ordinal)
        ?? new HashSet<string>();

    public string? TraceId => Activity.Current?.TraceId.ToString() ?? accessor.HttpContext?.TraceIdentifier;

    private Guid? ParseGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirst(claimType)?.Value, out var id) ? id : null;
}
