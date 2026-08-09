using Microsoft.EntityFrameworkCore;
using Npgsql;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Domain;
using Puntiro.Modules.Integrations.Persistence;
using Xunit;

namespace Puntiro.IntegrationTests.Integrations;

[Collection(PostgresCollection.Name)]
public sealed class IntegrationTokenPersistenceTests(PostgresDatabase database)
{
    [Fact]
    public void Design_time_factory_requires_the_explicit_connection_environment_variable()
    {
        const string variable = "ConnectionStrings__Puntiro";
        var previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, null);
            var error = Assert.Throws<InvalidOperationException>(() =>
                new IntegrationsDbContextFactory().CreateDbContext([]));
            Assert.Contains(variable, error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public async Task Migration_owns_only_the_integrations_schema_and_is_current()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);

        var tables = await scope.Context.Database.SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables " +
                "WHERE table_schema = 'integrations' ORDER BY table_name")
            .ToListAsync(cancellationToken);

        Assert.Equal(
            ["__EFMigrationsHistory", "integration_token_scopes", "integration_tokens", "security_events"],
            tables);
        Assert.Empty(await scope.Context.Database.GetPendingMigrationsAsync(cancellationToken));
    }

    [Fact]
    public async Task Postgres_enforces_unique_public_ids_active_slots_and_closed_scopes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var first = await scope.Service.CreateAsync(
            scope.Command("Primary"), cancellationToken);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var duplicatePublicId = connection.CreateCommand();
        duplicatePublicId.CommandText =
            """
            INSERT INTO integrations.integration_tokens
                (id, public_id, organization_id, display_name, secret_verifier,
                 key_version, active_slot, created_by_user_id, created_at, version)
            VALUES ($1, $2, $3, 'Duplicate public ID', $4, 'integration-v1', 2, $5, $6, 1)
            """;
        duplicatePublicId.Parameters.AddWithValue(Guid.CreateVersion7());
        duplicatePublicId.Parameters.AddWithValue(first.Metadata.PublicId);
        duplicatePublicId.Parameters.AddWithValue(scope.OrganizationId);
        duplicatePublicId.Parameters.AddWithValue(Enumerable.Repeat((byte)0x22, 32).ToArray());
        duplicatePublicId.Parameters.AddWithValue(scope.ActorUserId);
        duplicatePublicId.Parameters.AddWithValue(scope.Time.GetUtcNow());
        var publicIdError = await Assert.ThrowsAsync<PostgresException>(() =>
            duplicatePublicId.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, publicIdError.SqlState);

        using var second = await scope.Service.CreateAsync(
            scope.Command("Secondary"), cancellationToken);
        await using var thirdSlot = connection.CreateCommand();
        thirdSlot.CommandText =
            """
            INSERT INTO integrations.integration_tokens
                (id, public_id, organization_id, display_name, secret_verifier,
                 key_version, active_slot, created_by_user_id, created_at, version)
            VALUES ($1, $2, $3, 'Third slot', $4, 'integration-v1', 2, $5, $6, 1)
            """;
        thirdSlot.Parameters.AddWithValue(Guid.CreateVersion7());
        thirdSlot.Parameters.AddWithValue("zAECAwQFBgcICQoLDA0ODw");
        thirdSlot.Parameters.AddWithValue(scope.OrganizationId);
        thirdSlot.Parameters.AddWithValue(Enumerable.Repeat((byte)0x33, 32).ToArray());
        thirdSlot.Parameters.AddWithValue(scope.ActorUserId);
        thirdSlot.Parameters.AddWithValue(scope.Time.GetUtcNow());
        var slotError = await Assert.ThrowsAsync<PostgresException>(() =>
            thirdSlot.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, slotError.SqlState);

        await using var unknownScope = connection.CreateCommand();
        unknownScope.CommandText =
            "INSERT INTO integrations.integration_token_scopes (token_id, scope) VALUES ($1, 'admin.all')";
        unknownScope.Parameters.AddWithValue(first.Metadata.Id);
        var scopeError = await Assert.ThrowsAsync<PostgresException>(() =>
            unknownScope.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal(PostgresErrorCodes.CheckViolation, scopeError.SqlState);
    }

    [Fact]
    public async Task Security_events_are_append_only_and_contain_only_bounded_audit_metadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var issued = await scope.Service.CreateAsync(scope.Command("ERP"), cancellationToken);
        await scope.Service.RevokeAsync(
            scope.OrganizationId,
            issued.Metadata.Id,
            scope.ActorUserId,
            issued.Metadata.Version,
            cancellationToken);

        var events = await scope.Context.SecurityEvents
            .AsNoTracking()
            .Where(item => item.TokenId == issued.Metadata.Id)
            .OrderBy(item => item.OccurredAtUtc)
            .ToListAsync(cancellationToken);
        Assert.Equal(2, events.Count);
        Assert.Equal(["integration_token.created", "integration_token.revoked"],
            events.Select(static item => item.EventType));
        Assert.All(events, item =>
        {
            Assert.Equal(scope.OrganizationId, item.OrganizationId);
            Assert.Equal(scope.ActorUserId, item.ActorUserId);
            Assert.Matches("^trace:integration:[0-9a-f]{32}$", item.TraceId);
            Assert.Equal("success", item.Result);
        });

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var mutation = connection.CreateCommand();
        mutation.CommandText =
            "UPDATE integrations.security_events SET result = 'failure' WHERE token_id = $1";
        mutation.Parameters.AddWithValue(issued.Metadata.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            mutation.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, error.SqlState);
    }
}
