using Microsoft.AspNetCore.Authorization;
using Puntiro.Modules.Integrations.Domain;

namespace Puntiro.Cloud.Auth;

public sealed class IntegrationScopeRequirement(IntegrationScope scope) : IAuthorizationRequirement
{
    public IntegrationScope Scope { get; } = scope is
        IntegrationScope.ShipmentsRead or IntegrationScope.ShipmentsWrite
        ? scope
        : throw new ArgumentOutOfRangeException(nameof(scope));
}
