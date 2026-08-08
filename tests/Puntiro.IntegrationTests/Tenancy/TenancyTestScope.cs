using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Tenancy.Persistence;
using Puntiro.Modules.Tenancy.Services;

namespace Puntiro.IntegrationTests.Tenancy;

internal sealed class TenancyTestScope : IAsyncDisposable
{
    private TenancyTestScope(TenancyDbContext context, TenancyProvisioningService service)
    {
        Context = context;
        Service = service;
    }

    internal TenancyDbContext Context { get; }

    internal TenancyProvisioningService Service { get; }

    internal static async Task<TenancyTestScope> CreateAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy"))
            .Options;
        var context = new TenancyDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);

        return new TenancyTestScope(
            context,
            new TenancyProvisioningService(context, TimeProvider.System));
    }

    public ValueTask DisposeAsync()
    {
        return Context.DisposeAsync();
    }
}
