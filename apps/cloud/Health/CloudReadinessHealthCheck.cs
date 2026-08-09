using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Security.Cryptography;
using Puntiro.Cloud.Configuration;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Integrations.Persistence;
using Puntiro.Modules.Tenancy.Persistence;

namespace Puntiro.Cloud.Health;

public sealed class CloudReadinessHealthCheck(
    TenancyDbContext tenancy,
    IdentityDbContext identity,
    IntegrationsDbContext integrations,
    IIdentityProvisioningReadinessService identityReadiness,
    IDataProtectionProvider dataProtection,
    PuntiroCloudOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(options.Security.DataProtectionKeysPath))
            {
                return HealthCheckResult.Unhealthy("The Data Protection directory is unavailable.");
            }

            if (!await tenancy.Database.CanConnectAsync(cancellationToken) ||
                !await identity.Database.CanConnectAsync(cancellationToken) ||
                !await integrations.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
            }

            if ((await tenancy.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                return HealthCheckResult.Unhealthy("Tenancy migrations are pending.");
            }

            if ((await identity.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                return HealthCheckResult.Unhealthy("Identity migrations are pending.");
            }

            if ((await integrations.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                return HealthCheckResult.Unhealthy("Integrations migrations are pending.");
            }

            if (!await identityReadiness.IsReadyForTrustedProvisioningAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Identity key material is unavailable.");
            }

            if (!CanUseCurrentHmac(options.Security.SessionHmac) ||
                !CanUseCurrentHmac(options.Security.RecoveryHmac) ||
                !CanUseCurrentHmac(options.Security.IntegrationHmac))
            {
                return HealthCheckResult.Unhealthy("Current HMAC key material is unavailable.");
            }

            var protector = dataProtection.CreateProtector("Puntiro.Cloud.Readiness.v1");
            var protectedValue = protector.Protect("ready");
            if (!string.Equals(protector.Unprotect(protectedValue), "ready", StringComparison.Ordinal))
            {
                return HealthCheckResult.Unhealthy("Data Protection round trip failed.");
            }

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Readiness validation failed.", exception);
        }
    }

    private static bool CanUseCurrentHmac(VersionedHmacOptions options)
    {
        IReadOnlyDictionary<string, byte[]>? keys = null;
        try
        {
            keys = options.Decode("readiness HMAC");
            var probe = RandomNumberGenerator.GetBytes(16);
            try
            {
                var digest = HMACSHA256.HashData(keys[options.CurrentVersion], probe);
                try
                {
                    return digest.Length == HMACSHA256.HashSizeInBytes;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(digest);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(probe);
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        finally
        {
            if (keys is not null)
            {
                foreach (var key in keys.Values) CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
