using System.Security.Claims;

namespace Puntiro.Cloud.Http;

public sealed class TenantContext
{
    public Guid UserId { get; private set; }
    public Guid IntegrationTokenId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public bool IsEstablished { get; private set; }
    public bool IsIntegration { get; private set; }

    internal void Establish(Guid userId, Guid organizationId, string role)
    {
        if (userId == Guid.Empty || organizationId == Guid.Empty ||
            !string.Equals(role, "owner", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Trusted tenant context is invalid.");
        }

        if (IsEstablished && (UserId != userId || OrganizationId != organizationId || Role != role))
        {
            throw new InvalidOperationException("Tenant context cannot be changed during a request.");
        }

        UserId = userId;
        IntegrationTokenId = Guid.Empty;
        OrganizationId = organizationId;
        Role = role;
        IsIntegration = false;
        IsEstablished = true;
    }

    internal void EstablishIntegration(Guid tokenId, Guid organizationId)
    {
        if (tokenId == Guid.Empty || organizationId == Guid.Empty)
        {
            throw new InvalidOperationException("Trusted integration tenant context is invalid.");
        }

        if (IsEstablished && (!IsIntegration || IntegrationTokenId != tokenId ||
                OrganizationId != organizationId))
        {
            throw new InvalidOperationException("Tenant context cannot be changed during a request.");
        }

        UserId = Guid.Empty;
        IntegrationTokenId = tokenId;
        OrganizationId = organizationId;
        Role = string.Empty;
        IsIntegration = true;
        IsEstablished = true;
    }
}

internal static class AdminClaimTypes
{
    internal const string SessionId = "puntiro:session_id";
    internal const string OrganizationId = "puntiro:organization_id";
    internal const string Email = "puntiro:email";
    internal const string IdleExpiresAt = "puntiro:idle_expires_at";
    internal const string AbsoluteExpiresAt = "puntiro:absolute_expires_at";
    internal const string SecondFactorVerifiedAt = "puntiro:second_factor_verified_at";

    internal static bool TryReadIds(
        ClaimsPrincipal principal,
        out Guid userId,
        out Guid sessionId,
        out Guid organizationId)
    {
        userId = Guid.Empty;
        sessionId = Guid.Empty;
        organizationId = Guid.Empty;
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId) &&
            Guid.TryParse(principal.FindFirstValue(SessionId), out sessionId) &&
            Guid.TryParse(principal.FindFirstValue(OrganizationId), out organizationId);
    }
}
