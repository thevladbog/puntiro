using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace Puntiro.Modules.Identity.Security;

internal interface IDataProtectionKeyRingReadiness
{
    bool HasUsableCurrentKey();
}

internal sealed class DataProtectionKeyRingReadiness(
    IKeyManager keyManager,
    TimeProvider timeProvider) : IDataProtectionKeyRingReadiness
{
    public bool HasUsableCurrentKey()
    {
        var now = timeProvider.GetUtcNow();
        return keyManager.GetAllKeys().Any(key =>
            !key.IsRevoked &&
            key.ActivationDate <= now &&
            key.ExpirationDate > now);
    }
}
