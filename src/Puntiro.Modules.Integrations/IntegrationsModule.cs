using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Persistence;
using Puntiro.Modules.Integrations.Security;
using Puntiro.Modules.Integrations.Services;
using Puntiro.Security;

namespace Puntiro.Modules.Integrations;

public static class IntegrationsModule
{
    public static IServiceCollection AddIntegrationsModule(
        this IServiceCollection services,
        string connectionString,
        IntegrationKeyOptions keyOptions,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(keyOptions);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A PostgreSQL connection string is required.",
                nameof(connectionString));
        }

        services.AddDbContext<IntegrationsDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "integrations")));
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

        services.AddScoped<IIntegrationTokenCodec, IntegrationTokenCodec>();
        services.AddScoped<
            IIntegrationTokenTransactionFactory,
            EfIntegrationTokenTransactionFactory>();
        services.AddScoped<IIntegrationTokenService, IntegrationTokenService>();
        return services;
    }
}
