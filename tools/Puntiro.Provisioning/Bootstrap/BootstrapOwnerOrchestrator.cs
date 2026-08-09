using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Domain;
using Puntiro.Provisioning.Cli;

namespace Puntiro.Provisioning.Bootstrap;

public sealed class BootstrapOwnerOrchestrator(
    ITenancyProvisioningService tenancy,
    ITenantAccessService access,
    IIdentityProvisioningService identity,
    IIdentityProvisioningReadinessService readiness,
    IProvisioningTerminal terminal)
{
    public async Task<ProvisioningExit> RunAsync(
        string organizationName,
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

            var organization = await tenancy.GetOrCreateProvisioningAsync(
                organizationName,
                organizationSlug,
                cancellationToken);
            if (organization.Status == OrganizationStatus.Active)
            {
                return await CompleteInterruptedActivationAsync(
                    organization.Id,
                    email,
                    cancellationToken);
            }

            if (organization.Status != OrganizationStatus.Provisioning)
            {
                return ProvisioningExit.Conflict;
            }

            PendingOwnerIdentity pending;
            using (var password = await terminal.ReadPasswordAsync(cancellationToken))
            {
                try
                {
                    pending = await password.Use(value => identity.BeginOwnerAsync(
                        organization.Id,
                        email,
                        new string(value),
                        new IdentityAuditContext(null, "cli-bootstrap-start"),
                        cancellationToken));
                }
                catch (InvalidOperationException)
                {
                    return ProvisioningExit.Conflict;
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
                    await firstCode.Use(value => identity.ConfirmOwnerTotpAsync(
                        pending.UserId,
                        new string(value),
                        new IdentityAuditContext(pending.UserId, "cli-bootstrap-confirm"),
                        cancellationToken));
                }
                catch (InvalidOperationException)
                {
                    return ProvisioningExit.InvalidCredentials;
                }

                await tenancy.EnsureOwnerMembershipAsync(
                    organization.Id,
                    pending.UserId,
                    cancellationToken);
                await tenancy.ActivateAsync(
                    organization.Id,
                    new TenancyAuditContext(pending.UserId, "cli-bootstrap-activate"),
                    cancellationToken);
                await identity.CompleteOwnerAsync(
                    pending.UserId,
                    organization.Id,
                    new IdentityAuditContext(pending.UserId, "cli-bootstrap-complete"),
                    cancellationToken);
                return ProvisioningExit.Success;
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
        catch (InvalidOperationException)
        {
            return ProvisioningExit.Conflict;
        }
        catch (Exception)
        {
            return ProvisioningExit.InfrastructureFailure;
        }
    }

    private async Task<ProvisioningExit> CompleteInterruptedActivationAsync(
        Guid organizationId,
        string email,
        CancellationToken cancellationToken)
    {
        var userId = await identity.FindUserIdForTrustedProvisioningAsync(email, cancellationToken);
        if (userId is null ||
            !await access.IsActiveOwnerAsync(organizationId, userId.Value, cancellationToken))
        {
            return ProvisioningExit.Conflict;
        }

        await identity.CompleteOwnerAsync(
            userId.Value,
            organizationId,
            new IdentityAuditContext(userId.Value, "cli-bootstrap-cleanup"),
            cancellationToken);
        return ProvisioningExit.Success;
    }
}
