using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Puntiro.Cloud.Http;
using Puntiro.Modules.Tenancy.Contracts;

namespace Puntiro.Cloud.Auth;

public sealed class OwnerRequirement : IAuthorizationRequirement;

public sealed class OwnerAuthorizationHandler(
    ITenantAccessService access,
    TenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<OwnerRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerRequirement requirement)
    {
        if (!AdminClaimTypes.TryReadIds(
                context.User,
                out var userId,
                out _,
                out var organizationId) ||
            !await access.IsActiveOwnerAsync(
                organizationId,
                userId,
                httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None))
        {
            return;
        }

        tenantContext.Establish(userId, organizationId, "owner");
        if (context.User.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "owner"));
        }

        context.Succeed(requirement);
    }
}
