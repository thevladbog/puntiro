using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Security;
using Puntiro.Modules.Identity.Services;
using Puntiro.Security;

namespace Puntiro.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        string connectionString,
        IdentityKeyOptions keyOptions,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(keyOptions);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A PostgreSQL connection string is required.", nameof(connectionString));
        }

        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
        services.AddDataProtection();
        services.AddSingleton(keyOptions);
        services.TryAddSingleton<ISecretGenerator, SystemSecretGenerator>();
        if (timeProvider is null)
        {
            services.TryAddSingleton(TimeProvider.System);
        }
        else
        {
            services.AddSingleton(timeProvider);
        }

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITotpService, Rfc6238Totp>();
        services.AddScoped<TotpSecretProtector>();
        services.AddScoped<IDataProtectionKeyRingReadiness, DataProtectionKeyRingReadiness>();
        services.AddScoped<SessionTokenCodec>();
        services.AddScoped<IRecoveryCodeService>(provider =>
        {
            var options = provider.GetRequiredService<IdentityKeyOptions>();
            var key = options.GetRecoveryKey(options.CurrentRecoveryKeyVersion);
            try
            {
                return new RecoveryCodeService(
                    key,
                    provider.GetRequiredService<ISecretGenerator>());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        });
        services.AddScoped<IIdentityProvisioningService, IdentityProvisioningService>();
        services.AddScoped<IIdentityProvisioningReadinessService, IdentityProvisioningReadinessService>();
        services.AddScoped<IAdminAuthenticationService, AdminAuthenticationService>();
        services.AddScoped<IAdminSessionService, AdminSessionService>();
        return services;
    }
}
