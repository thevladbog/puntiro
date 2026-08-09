using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Provisioning.Cli;

namespace Puntiro.Provisioning.Recovery;

public sealed class ResetOwnerTotpOrchestrator(
    ITenancyProvisioningService tenancy,
    ITenantAccessService access,
    IIdentityProvisioningService identity,
    IIdentityProvisioningReadinessService readiness,
    IProvisioningTerminal terminal)
{
    public async Task<ProvisioningExit> RunAsync(
        string organizationSlug,
        string email,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await readiness.IsReadyForTrustedProvisioningAsync(cancellationToken))
            {
                return ProvisioningExit.InfrastructureFailure;
            }

            var organizationId = await tenancy.FindOrganizationIdForTrustedProvisioningAsync(
                organizationSlug,
                cancellationToken);
            var userId = await identity.FindUserIdForTrustedProvisioningAsync(email, cancellationToken);
            if (organizationId is null || userId is null ||
                !await access.IsActiveOwnerAsync(
                    organizationId.Value,
                    userId.Value,
                    cancellationToken))
            {
                return ProvisioningExit.InvalidCredentials;
            }

            PendingOwnerTotpReset pending;
            using (var password = await terminal.ReadPasswordAsync(cancellationToken))
            using (var recoveryCode = await terminal.ReadRecoveryCodeAsync(cancellationToken))
            {
                try
                {
                    var passwordValue = password.Reveal();
                    var recoveryValue = recoveryCode.Reveal();
                    pending = await identity.PrepareOwnerTotpResetAsync(
                        userId.Value,
                        passwordValue,
                        recoveryValue,
                        new IdentityAuditContext(userId.Value, "cli-totp-reset-prepare"),
                        cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return ProvisioningExit.InvalidCredentials;
                }
            }

            using (pending)
            {
                await terminal.WriteEnrollmentAsync(
                    pending.TotpUri,
                    pending.RecoveryCodes,
                    cancellationToken);
                using var firstCode = await terminal.ReadTotpAsync(cancellationToken);
                try
                {
                    await using var lease = await tenancy
                        .TryAcquireActiveOwnerMutationLeaseForTrustedProvisioningAsync(
                            organizationId.Value,
                            userId.Value,
                            cancellationToken);
                    if (lease is null)
                    {
                        return ProvisioningExit.InvalidCredentials;
                    }

                    await firstCode.Use(code => identity.CompleteOwnerTotpResetAsync(
                        pending,
                        new string(code),
                        new IdentityAuditContext(userId.Value, "cli-totp-reset-complete"),
                        cancellationToken));
                }
                catch (InvalidOperationException)
                {
                    return ProvisioningExit.InvalidCredentials;
                }
            }

            return ProvisioningExit.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return ProvisioningExit.InvalidArguments;
        }
        catch (Exception)
        {
            return ProvisioningExit.InfrastructureFailure;
        }
    }
}
