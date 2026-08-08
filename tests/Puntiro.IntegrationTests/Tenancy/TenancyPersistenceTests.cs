using Microsoft.EntityFrameworkCore;
using Npgsql;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Domain;
using Puntiro.Modules.Tenancy.Services;
using Xunit;

namespace Puntiro.IntegrationTests.Tenancy;

[Collection(PostgresCollection.Name)]
public sealed class TenancyPersistenceTests(PostgresDatabase database)
{
    private static readonly Guid UserId = Guid.Parse("01989f73-0f9a-7aa2-9acf-8781f8a00001");
    private static readonly TenancyAuditContext AuditContext = new(UserId, "trace-tenancy-test-001");

    [Fact]
    public async Task Slug_and_membership_constraints_are_enforced_by_postgres()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var first = await scope.Service.GetOrCreateProvisioningAsync(
            "Puntiro",
            "puntiro",
            cancellationToken);
        var repeated = await scope.Service.GetOrCreateProvisioningAsync(
            "Puntiro",
            "PUNTIRO",
            cancellationToken);
        Assert.Equal(first.Id, repeated.Id);

        var membership = await scope.Service.EnsureOwnerMembershipAsync(
            first.Id,
            UserId,
            cancellationToken);
        var duplicate = await scope.Service.EnsureOwnerMembershipAsync(
            first.Id,
            UserId,
            cancellationToken);
        Assert.Equal(membership.Id, duplicate.Id);

        var duplicateOrganization = Organization.StartProvisioning(
            Guid.CreateVersion7(),
            "Duplicate",
            "puntiro");
        duplicateOrganization.SetCreatedAt(DateTimeOffset.UtcNow);
        scope.Context.Organizations.Add(duplicateOrganization);
        await Assert.ThrowsAsync<DbUpdateException>(
            () => scope.Context.SaveChangesAsync(cancellationToken));
        scope.Context.ChangeTracker.Clear();

        var duplicateMembership = Membership.CreateOwner(
            Guid.CreateVersion7(),
            first.Id,
            UserId,
            DateTimeOffset.UtcNow);
        scope.Context.Memberships.Add(duplicateMembership);
        await Assert.ThrowsAsync<DbUpdateException>(
            () => scope.Context.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Activation_requires_an_owner_and_appends_one_redacted_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var organization = await scope.Service.GetOrCreateProvisioningAsync(
            "Activation",
            $"activation-{Guid.NewGuid():N}",
            cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scope.Service.ActivateAsync(organization.Id, AuditContext, cancellationToken));
        Assert.Empty(await scope.Context.SecurityEvents
            .Where(item => item.OrganizationId == organization.Id)
            .ToListAsync(cancellationToken));

        await scope.Service.EnsureOwnerMembershipAsync(
            organization.Id,
            UserId,
            cancellationToken);
        await scope.Service.ActivateAsync(organization.Id, AuditContext, cancellationToken);

        var activated = await scope.Context.Organizations
            .AsNoTracking()
            .SingleAsync(item => item.Id == organization.Id, cancellationToken);
        Assert.Equal(OrganizationStatus.Active, activated.Status);

        var securityEvent = await scope.Context.SecurityEvents
            .AsNoTracking()
            .SingleAsync(item => item.OrganizationId == organization.Id, cancellationToken);
        Assert.Equal("organization.activated", securityEvent.EventType);
        Assert.Equal("success", securityEvent.Result);
        Assert.Equal("provisioning_completed", securityEvent.ReasonCode);
        Assert.Equal(UserId, securityEvent.ActorUserId);
        Assert.Equal("trace-tenancy-test-001", securityEvent.TraceId);
    }

    [Fact]
    public async Task Activation_rejects_a_different_actor_without_state_or_event_changes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var organization = await scope.Service.GetOrCreateProvisioningAsync(
            "Actor mismatch",
            $"actor-mismatch-{Guid.NewGuid():N}",
            cancellationToken);
        var actualOwnerId = Guid.CreateVersion7();
        await scope.Service.EnsureOwnerMembershipAsync(
            organization.Id,
            actualOwnerId,
            cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Service.ActivateAsync(
                organization.Id,
                new TenancyAuditContext(Guid.CreateVersion7(), "trace-actor-mismatch"),
                cancellationToken));

        scope.Context.ChangeTracker.Clear();
        var stored = await scope.Context.Organizations
            .AsNoTracking()
            .SingleAsync(item => item.Id == organization.Id, cancellationToken);
        Assert.Equal(OrganizationStatus.Provisioning, stored.Status);
        Assert.Equal(organization.Version, stored.Version);
        Assert.Empty(await scope.Context.SecurityEvents
            .AsNoTracking()
            .Where(item => item.OrganizationId == organization.Id)
            .ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task Idempotent_activation_still_rejects_a_non_owner_actor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var organization = await CreateActiveOrganizationAsync(
            scope,
            "active-actor",
            UserId,
            cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Service.ActivateAsync(
                organization.Id,
                new TenancyAuditContext(Guid.CreateVersion7(), "trace-active-non-owner"),
                cancellationToken));

        Assert.Equal(1, await scope.Context.SecurityEvents
            .AsNoTracking()
            .CountAsync(item => item.OrganizationId == organization.Id, cancellationToken));
    }

    [Fact]
    public async Task Access_service_never_chooses_between_multiple_active_memberships()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var first = await CreateActiveOrganizationAsync(scope, "first", UserId, cancellationToken);
        var second = await CreateActiveOrganizationAsync(scope, "second", UserId, cancellationToken);
        var access = new TenantAccessService(scope.Context);

        await Assert.ThrowsAsync<OrganizationSelectionRequiredException>(
            () => access.FindSingleActiveMembershipAsync(UserId, cancellationToken));
        Assert.True(await access.IsActiveOwnerAsync(first.Id, UserId, cancellationToken));
        Assert.True(await access.IsActiveOwnerAsync(second.Id, UserId, cancellationToken));
    }

    [Fact]
    public async Task Initial_migration_places_its_tables_and_history_in_the_tenancy_schema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_name IN (
                'organizations',
                'memberships',
                'security_events',
                '__EFMigrationsHistory')
            ORDER BY table_schema, table_name
            """;

        var tables = new List<(string Schema, string Table)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        Assert.Equal(4, tables.Count);
        Assert.Contains(("tenancy", "__EFMigrationsHistory"), tables);
        Assert.Contains(("tenancy", "organizations"), tables);
        Assert.Contains(("tenancy", "memberships"), tables);
        Assert.Contains(("tenancy", "security_events"), tables);
    }

    [Fact]
    public async Task Lifecycle_values_are_stored_in_the_approved_lowercase_form()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var organization = await scope.Service.GetOrCreateProvisioningAsync(
            "Lowercase",
            $"lowercase-{Guid.NewGuid():N}",
            cancellationToken);
        await scope.Service.EnsureOwnerMembershipAsync(
            organization.Id,
            UserId,
            cancellationToken);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT o.status, m.role, m.status
            FROM tenancy.organizations AS o
            JOIN tenancy.memberships AS m ON m.organization_id = o.id
            WHERE o.id = @organization_id
            """;
        command.Parameters.AddWithValue("organization_id", organization.Id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal("provisioning", reader.GetString(0));
        Assert.Equal("owner", reader.GetString(1));
        Assert.Equal("active", reader.GetString(2));
    }

    private static async Task<OrganizationSnapshot> CreateActiveOrganizationAsync(
        TenancyTestScope scope,
        string prefix,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organization = await scope.Service.GetOrCreateProvisioningAsync(
            prefix,
            $"{prefix}-{Guid.NewGuid():N}",
            cancellationToken);
        await scope.Service.EnsureOwnerMembershipAsync(
            organization.Id,
            userId,
            cancellationToken);
        await scope.Service.ActivateAsync(
            organization.Id,
            new TenancyAuditContext(userId, $"trace-{prefix}-activation"),
            cancellationToken);
        return organization;
    }
}
