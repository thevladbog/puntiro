using Microsoft.AspNetCore.Authorization;
using Puntiro.Cloud.Http;

namespace Puntiro.Cloud.Auth;

public sealed class IntegrationScopeAuthorizationHandler(TenantContext tenantContext)
    : AuthorizationHandler<IntegrationScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IntegrationScopeRequirement requirement)
    {
        var identity = context.User.Identities.SingleOrDefault(candidate => string.Equals(
            candidate.AuthenticationType,
            IntegrationBearerAuthenticationHandler.AuthenticationScheme,
            StringComparison.Ordinal));
        if (identity is not null &&
            IntegrationClaimTypes.TryReadPrincipal(
                context.User,
                out var tokenId,
                out var organizationId) &&
            identity.Claims.Any(claim =>
                claim.Type == IntegrationClaimTypes.Scope &&
                string.Equals(
                    claim.Value,
                    IntegrationClaimTypes.ScopeValue(requirement.Scope),
                    StringComparison.Ordinal)))
        {
            tenantContext.EstablishIntegration(tokenId, organizationId);
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
