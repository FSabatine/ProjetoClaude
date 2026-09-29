using Microsoft.AspNetCore.Authorization;
using REC4.Application.Common;

namespace REC4.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(Rec4Claims.Permission, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
