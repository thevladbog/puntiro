using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Persistence;
using Puntiro.Modules.Tenancy.Services;

namespace Puntiro.Modules.Tenancy;

public static class TenancyModule
{
    public static IServiceCollection AddTenancyModule(
        this IServiceCollection services,
        string connectionString,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A PostgreSQL connection string is required.", nameof(connectionString));
        }

        services.AddDbContext<TenancyDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy")));
        if (timeProvider is null)
        {
            services.TryAddSingleton(TimeProvider.System);
        }
        else
        {
            services.AddSingleton(timeProvider);
        }
        services.AddScoped<ITenancyProvisioningService, TenancyProvisioningService>();
        services.AddScoped<ITenantAccessService, TenantAccessService>();
        return services;
    }
}
