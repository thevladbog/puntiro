using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Puntiro.Modules.Tenancy.Persistence;

public sealed class TenancyDbContextFactory : IDesignTimeDbContextFactory<TenancyDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__Puntiro";

    public TenancyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringEnvironmentVariable} must be configured for tenancy migrations.");
        }

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy"))
            .Options;

        return new TenancyDbContext(options);
    }
}
