using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Services;

namespace Puntiro.IntegrationTests.Identity;

internal static class IdentityTestAuditExtensions
{
    private static readonly IdentityAuditContext Audit = new(null, "trace-identity-test");

    internal static Task<PendingOwnerIdentity> BeginOwnerAsync(
        this IdentityProvisioningService service,
        Guid provisioningOrganizationId,
        string email,
        string password,
        CancellationToken cancellationToken) =>
        service.BeginOwnerAsync(provisioningOrganizationId, email, password, Audit, cancellationToken);

    internal static Task ConfirmOwnerTotpAsync(
        this IdentityProvisioningService service,
        Guid userId,
        string code,
        CancellationToken cancellationToken) =>
        service.ConfirmOwnerTotpAsync(userId, code, Audit, cancellationToken);

    internal static Task CompleteOwnerAsync(
        this IdentityProvisioningService service,
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        service.CompleteOwnerAsync(userId, organizationId, Audit, cancellationToken);

    internal static Task<PendingOwnerTotpReset> PrepareOwnerTotpResetAsync(
        this IdentityProvisioningService service,
        Guid userId,
        string password,
        string recoveryCode,
        CancellationToken cancellationToken) =>
        service.PrepareOwnerTotpResetAsync(userId, password, recoveryCode, Audit, cancellationToken);

    internal static Task CompleteOwnerTotpResetAsync(
        this IdentityProvisioningService service,
        PendingOwnerTotpReset pending,
        string firstTotpCode,
        CancellationToken cancellationToken) =>
        service.CompleteOwnerTotpResetAsync(pending, firstTotpCode, Audit, cancellationToken);

    internal static Task<VerifiedIdentity?> VerifyAsync(
        this AdminAuthenticationService service,
        AdminCredentials credentials,
        CancellationToken cancellationToken) =>
        service.VerifyAsync(credentials, Audit, cancellationToken);

    internal static Task<IssuedAdminSession> CreateAsync(
        this AdminSessionService service,
        VerifiedIdentity identity,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        service.CreateAsync(identity, organizationId, Audit, cancellationToken);

    internal static Task RevokeAsync(
        this AdminSessionService service,
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken) =>
        service.RevokeAsync(sessionId, reason, Audit, cancellationToken);
}
