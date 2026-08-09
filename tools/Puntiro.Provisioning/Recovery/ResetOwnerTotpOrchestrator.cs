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
        if (!await ProvisioningReadiness.IsReadyAsync(readiness, cancellationToken))
        {
            return ProvisioningExit.InfrastructureFailure;
        }

        try
        {
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
                return await CompleteUnderFinalAuthorizationAsync(
                    organizationId.Value,
                    userId.Value,
                    pending,
                    firstCode,
                    cancellationToken);
            }
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

    private async Task<ProvisioningExit> CompleteUnderFinalAuthorizationAsync(
        Guid organizationId,
        Guid userId,
        PendingOwnerTotpReset pending,
        Puntiro.Security.SensitiveValue firstCode,
        CancellationToken cancellationToken)
    {
        ITrustedActiveOwnerMutationLease? lease;
        try
        {
            lease = await tenancy.TryAcquireActiveOwnerMutationLeaseForTrustedProvisioningAsync(
                organizationId,
                userId,
                cancellationToken);
        }
        catch (Exception)
        {
            return ProvisioningExit.InfrastructureFailure;
        }

        if (lease is null)
        {
            return ProvisioningExit.InvalidCredentials;
        }

        ProvisioningExit completionExit;
        try
        {
            await firstCode.Use(code => identity.CompleteOwnerTotpResetAsync(
                pending,
                new string(code),
                new IdentityAuditContext(userId, "cli-totp-reset-complete"),
                cancellationToken));
            completionExit = ProvisioningExit.Success;
        }
        catch (InvalidOperationException)
        {
            completionExit = ProvisioningExit.InvalidCredentials;
        }
        catch (Exception)
        {
            completionExit = ProvisioningExit.InfrastructureFailure;
        }

        try
        {
            await lease.DisposeAsync();
        }
        catch (Exception)
        {
            return ProvisioningExit.InfrastructureFailure;
        }

        return completionExit;
    }
}
