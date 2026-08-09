namespace Puntiro.Modules.Identity.Contracts;

/// <summary>
/// Performs read-only cryptographic readiness checks for trusted, non-HTTP
/// provisioning and recovery composition. It returns no identity data and
/// performs no authentication or recovery mutation.
/// </summary>
public interface IIdentityProvisioningReadinessService
{
    Task<bool> IsReadyForTrustedProvisioningAsync(CancellationToken cancellationToken);
}
