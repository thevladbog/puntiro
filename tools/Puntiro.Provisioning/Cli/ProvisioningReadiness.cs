using Puntiro.Modules.Identity.Contracts;

namespace Puntiro.Provisioning.Cli;

internal static class ProvisioningReadiness
{
    internal static async Task<bool> IsReadyAsync(
        IIdentityProvisioningReadinessService readiness,
        CancellationToken cancellationToken)
    {
        try
        {
            return await readiness.IsReadyForTrustedProvisioningAsync(cancellationToken);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
