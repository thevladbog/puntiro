using Npgsql;
using Xunit;

namespace Puntiro.IntegrationTests.Infrastructure;

public sealed class PostgresDatabase : IAsyncLifetime
{
    private readonly string _maintenanceConnectionString;
    private readonly string _databaseName = $"puntiro_test_{Guid.NewGuid():N}";

    public PostgresDatabase()
    {
        _maintenanceConnectionString = Environment.GetEnvironmentVariable("PUNTIRO_TEST_POSTGRES")
            ?? throw new InvalidOperationException(
                "PUNTIRO_TEST_POSTGRES must contain a PostgreSQL maintenance connection string.");

        var testConnection = new NpgsqlConnectionStringBuilder(_maintenanceConnectionString)
        {
            Database = _databaseName,
            Pooling = false
        };
        ConnectionString = testConnection.ConnectionString;
    }

    public string ConnectionString { get; }

    public async ValueTask InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_maintenanceConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_maintenanceConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
