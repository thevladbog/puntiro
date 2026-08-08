using Microsoft.EntityFrameworkCore;
using Npgsql;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Domain;
using Puntiro.Modules.Tenancy.Services;
using Xunit;

namespace Puntiro.IntegrationTests.Tenancy;

[Collection(PostgresCollection.Name)]
public sealed class TenancyPersistenceRegressionTests(PostgresDatabase database)
{
    [Fact]
    public async Task PostgreSQL_rejects_security_event_modification_and_deletion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (organization, _) = await CreateActiveOrganizationAsync("append-only", cancellationToken);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE tenancy.security_events
            SET reason_code = 'tampered'
            WHERE organization_id = @organization_id
            """;
        update.Parameters.AddWithValue("organization_id", organization.Id);
        var updateError = await Assert.ThrowsAsync<PostgresException>(
            () => update.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal("P0001", updateError.SqlState);

        await using var delete = connection.CreateCommand();
        delete.CommandText = """
            DELETE FROM tenancy.security_events
            WHERE organization_id = @organization_id
            """;
        delete.Parameters.AddWithValue("organization_id", organization.Id);
        var deleteError = await Assert.ThrowsAsync<PostgresException>(
            () => delete.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal("P0001", deleteError.SqlState);
    }

    [Fact]
    public async Task A_stale_second_context_cannot_overwrite_a_lifecycle_change()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (organization, _) = await CreateActiveOrganizationAsync("concurrency", cancellationToken);
        await using var firstContext = TenancyTestScope.CreateContext(database.ConnectionString);
        await using var staleContext = TenancyTestScope.CreateContext(database.ConnectionString);
        var first = await firstContext.Organizations.SingleAsync(
            item => item.Id == organization.Id,
            cancellationToken);
        var stale = await staleContext.Organizations.SingleAsync(
            item => item.Id == organization.Id,
            cancellationToken);

        first.Suspend();
        first.MarkUpdated(new DateTimeOffset(2026, 8, 8, 1, 0, 0, TimeSpan.Zero));
        stale.Suspend();
        stale.MarkUpdated(new DateTimeOffset(2026, 8, 8, 1, 0, 1, TimeSpan.Zero));
        await firstContext.SaveChangesAsync(cancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => staleContext.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task PostgreSQL_rejects_unknown_lifecycle_values()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var organization = await scope.Service.GetOrCreateProvisioningAsync(
            "Unknown",
            $"unknown-{Guid.NewGuid():N}",
            cancellationToken);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE tenancy.organizations
            SET status = 'unknown'
            WHERE id = @organization_id
            """;
        command.Parameters.AddWithValue("organization_id", organization.Id);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Revoked_membership_grants_no_tenant_access()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (organization, userId) = await CreateActiveOrganizationAsync("revoked", cancellationToken);
        await using (var updateContext = TenancyTestScope.CreateContext(database.ConnectionString))
        {
            var membership = await updateContext.Memberships.SingleAsync(
                item => item.OrganizationId == organization.Id && item.UserId == userId,
                cancellationToken);
            membership.Revoke(new DateTimeOffset(2026, 8, 8, 2, 0, 0, TimeSpan.Zero));
            await updateContext.SaveChangesAsync(cancellationToken);
        }

        await using var accessContext = TenancyTestScope.CreateContext(database.ConnectionString);
        var access = new TenantAccessService(accessContext);

        Assert.Null(await access.FindSingleActiveMembershipAsync(userId, cancellationToken));
        Assert.False(await access.IsActiveOwnerAsync(organization.Id, userId, cancellationToken));
    }

    [Fact]
    public async Task Suspended_organization_grants_no_tenant_access()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (organization, userId) = await CreateActiveOrganizationAsync("suspended", cancellationToken);
        await using (var updateContext = TenancyTestScope.CreateContext(database.ConnectionString))
        {
            var stored = await updateContext.Organizations.SingleAsync(
                item => item.Id == organization.Id,
                cancellationToken);
            stored.Suspend();
            stored.MarkUpdated(new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero));
            await updateContext.SaveChangesAsync(cancellationToken);
        }

        await using var accessContext = TenancyTestScope.CreateContext(database.ConnectionString);
        var access = new TenantAccessService(accessContext);

        Assert.Null(await access.FindSingleActiveMembershipAsync(userId, cancellationToken));
        Assert.False(await access.IsActiveOwnerAsync(organization.Id, userId, cancellationToken));
    }

    private async Task<(OrganizationSnapshot Organization, Guid UserId)> CreateActiveOrganizationAsync(
        string prefix,
        CancellationToken cancellationToken)
    {
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var organization = await scope.Service.GetOrCreateProvisioningAsync(
            prefix,
            $"{prefix}-{Guid.NewGuid():N}",
            cancellationToken);
        var userId = Guid.CreateVersion7();
        await scope.Service.EnsureOwnerMembershipAsync(
            organization.Id,
            userId,
            cancellationToken);
        await scope.Service.ActivateAsync(
            organization.Id,
            new TenancyAuditContext(userId, $"trace-{prefix}-activation"),
            cancellationToken);
        return (organization, userId);
    }
}
