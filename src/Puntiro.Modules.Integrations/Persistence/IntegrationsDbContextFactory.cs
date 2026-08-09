using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Puntiro.Modules.Integrations.Persistence;

public sealed class IntegrationsDbContextFactory
    : IDesignTimeDbContextFactory<IntegrationsDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__Puntiro";

    public IntegrationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringEnvironmentVariable} must be configured for integration migrations.");
        }

        var options = new DbContextOptionsBuilder<IntegrationsDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    "integrations"))
            .Options;
        return new IntegrationsDbContext(options);
    }
}
