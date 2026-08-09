using Puntiro.Security;

namespace Puntiro.Modules.Identity.Contracts;

public sealed record AdminSessionPrincipal(
    Guid SessionId,
    Guid UserId,
    Guid OrganizationId,
    DateTimeOffset IdleExpiresAt,
    DateTimeOffset AbsoluteExpiresAt,
    DateTimeOffset? SecondFactorVerifiedAt);

public sealed class IssuedAdminSession : IDisposable
{
    public IssuedAdminSession(AdminSessionPrincipal principal, SensitiveValue rawToken)
    {
        Principal = principal;
        RawToken = rawToken;
    }

    public AdminSessionPrincipal Principal { get; }
    public SensitiveValue RawToken { get; }

    public void Dispose() => RawToken.Dispose();
}

public interface IAdminSessionService
{
    Task<IssuedAdminSession> CreateAsync(
        VerifiedIdentity identity,
        Guid organizationId,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken);

    Task<AdminSessionPrincipal?> ValidateAsync(
        string presentedToken,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        Guid sessionId,
        string reason,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(
        Guid userId,
        string reason,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken);
}
