using Fleet.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Fleet.Api.Authorization;

/// <summary>
/// [HasPermission(Permissions.Vehicles.Update)] — authorization is always by permission, never by role (ADR-006).
/// Several permissions mean "any of them".
/// </summary>
public sealed class HasPermissionAttribute(params string[] permissions)
    : AuthorizeAttribute(PermissionPolicy.Prefix + string.Join(PermissionPolicy.Separator, permissions));

public static class PermissionPolicy
{
    public const string Prefix = "perm:";
    public const char Separator = '|';
}

public sealed class PermissionRequirement(IReadOnlyList<string> permissions) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (requirement.Permissions.Any(p => context.User.HasClaim(FleetClaims.Permission, p)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Builds "perm:..." policies on demand, so new permissions never need AddPolicy.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        var permissions = policyName[PermissionPolicy.Prefix.Length..].Split(PermissionPolicy.Separator, StringSplitOptions.RemoveEmptyEntries);
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissions))
            .Build();
    }
}
