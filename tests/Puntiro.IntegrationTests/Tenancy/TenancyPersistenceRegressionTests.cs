using Microsoft.EntityFrameworkCore;
using Npgsql;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Domain;
using Puntiro.Modules.Tenancy.Persistence;
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
        var remainingOwnerId = Guid.CreateVersion7();
        await using var updateScope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        await updateScope.Service.EnsureOwnerMembershipAsync(
            organization.Id,
            remainingOwnerId,
            cancellationToken);
        await updateScope.Service.RevokeOwnerMembershipAsync(
            organization.Id,
            userId,
            cancellationToken);

        await using var accessContext = TenancyTestScope.CreateContext(database.ConnectionString);
        var access = new TenantAccessService(accessContext);

        Assert.Null(await access.FindSingleActiveMembershipAsync(userId, cancellationToken));
        Assert.False(await access.IsActiveOwnerAsync(organization.Id, userId, cancellationToken));
        Assert.True(await access.IsActiveOwnerAsync(
            organization.Id,
            remainingOwnerId,
            cancellationToken));
    }

    [Fact]
    public async Task Last_active_owner_cannot_be_revoked_from_an_active_organization()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (organization, userId) = await CreateActiveOrganizationAsync(
            "last-owner",
            cancellationToken);
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Service.RevokeOwnerMembershipAsync(
                organization.Id,
                userId,
                cancellationToken));

        scope.Context.ChangeTracker.Clear();
        var storedOrganization = await scope.Context.Organizations
            .AsNoTracking()
            .SingleAsync(item => item.Id == organization.Id, cancellationToken);
        var storedMembership = await scope.Context.Memberships
            .AsNoTracking()
            .SingleAsync(
                item => item.OrganizationId == organization.Id && item.UserId == userId,
                cancellationToken);
        Assert.Equal(OrganizationStatus.Active, storedOrganization.Status);
        Assert.Equal(MembershipStatus.Active, storedMembership.Status);
    }

    [Fact]
    public async Task Concurrent_owner_revocations_leave_one_active_owner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (organization, firstOwnerId) = await CreateActiveOrganizationAsync(
            "parallel-revoke",
            cancellationToken);
        var secondOwnerId = Guid.CreateVersion7();
        await using (var setup = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken))
        {
            await setup.Service.EnsureOwnerMembershipAsync(
                organization.Id,
                secondOwnerId,
                cancellationToken);
        }

        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await using var firstConnection = new NpgsqlConnection(database.ConnectionString);
        await using var secondConnection = new NpgsqlConnection(database.ConnectionString);
        await Task.WhenAll(
            blocker.OpenAsync(cancellationToken),
            firstConnection.OpenAsync(cancellationToken),
            secondConnection.OpenAsync(cancellationToken));
        await using var blockerTransaction = await blocker.BeginTransactionAsync(cancellationToken);
        await LockOrganizationAsync(
            blocker,
            blockerTransaction,
            organization.Id,
            cancellationToken);
        await using var firstContext = CreateContext(firstConnection);
        await using var secondContext = CreateContext(secondConnection);
        var firstService = new TenancyProvisioningService(firstContext, TimeProvider.System);
        var secondService = new TenancyProvisioningService(secondContext, TimeProvider.System);
        var firstTask = ObserveAsync(firstService.RevokeOwnerMembershipAsync(
            organization.Id,
            firstOwnerId,
            cancellationToken));
        var secondTask = ObserveAsync(secondService.RevokeOwnerMembershipAsync(
            organization.Id,
            secondOwnerId,
            cancellationToken));

        bool bothWaitedForTheOrganizationLock;
        try
        {
            bothWaitedForTheOrganizationLock = await WaitForLockAsync(
                database.ConnectionString,
                [firstConnection.ProcessID, secondConnection.ProcessID],
                firstTask,
                secondTask,
                cancellationToken);
        }
        finally
        {
            await blockerTransaction.CommitAsync(cancellationToken);
        }

        Assert.True(bothWaitedForTheOrganizationLock);
        var outcomes = await Task.WhenAll(firstTask, secondTask);
        Assert.Single(outcomes, outcome => outcome.Error is null);
        Assert.Single(outcomes, outcome => outcome.Error is InvalidOperationException);

        await using var verification = TenancyTestScope.CreateContext(database.ConnectionString);
        Assert.Equal(1, await verification.Memberships.CountAsync(
            item => item.OrganizationId == organization.Id
                && item.Role == MembershipRole.Owner
                && item.Status == MembershipStatus.Active,
            cancellationToken));
        Assert.Equal(OrganizationStatus.Active, await verification.Organizations
            .Where(item => item.Id == organization.Id)
            .Select(item => item.Status)
            .SingleAsync(cancellationToken));
    }

    [Fact]
    public async Task Concurrent_activation_and_owner_revocation_preserve_the_invariant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var setup = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var organization = await setup.Service.GetOrCreateProvisioningAsync(
            "Activation race",
            $"activation-race-{Guid.NewGuid():N}",
            cancellationToken);
        var ownerId = Guid.CreateVersion7();
        await setup.Service.EnsureOwnerMembershipAsync(
            organization.Id,
            ownerId,
            cancellationToken);

        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await using var activationConnection = new NpgsqlConnection(database.ConnectionString);
        await using var revocationConnection = new NpgsqlConnection(database.ConnectionString);
        await Task.WhenAll(
            blocker.OpenAsync(cancellationToken),
            activationConnection.OpenAsync(cancellationToken),
            revocationConnection.OpenAsync(cancellationToken));
        await using var blockerTransaction = await blocker.BeginTransactionAsync(cancellationToken);
        await LockOrganizationAsync(
            blocker,
            blockerTransaction,
            organization.Id,
            cancellationToken);
        await using var activationContext = CreateContext(activationConnection);
        await using var revocationContext = CreateContext(revocationConnection);
        var activationService = new TenancyProvisioningService(
            activationContext,
            TimeProvider.System);
        var revocationService = new TenancyProvisioningService(
            revocationContext,
            TimeProvider.System);
        var activationTask = ObserveAsync(activationService.ActivateAsync(
            organization.Id,
            new TenancyAuditContext(ownerId, "trace-activation-race"),
            cancellationToken));
        var revocationTask = ObserveAsync(revocationService.RevokeOwnerMembershipAsync(
            organization.Id,
            ownerId,
            cancellationToken));

        bool bothWaitedForTheOrganizationLock;
        try
        {
            bothWaitedForTheOrganizationLock = await WaitForLockAsync(
                database.ConnectionString,
                [activationConnection.ProcessID, revocationConnection.ProcessID],
                activationTask,
                revocationTask,
                cancellationToken);
        }
        finally
        {
            await blockerTransaction.CommitAsync(cancellationToken);
        }

        Assert.True(bothWaitedForTheOrganizationLock);
        var outcomes = await Task.WhenAll(activationTask, revocationTask);
        Assert.Single(outcomes, outcome => outcome.Error is null);
        Assert.Single(outcomes, outcome => outcome.Error is InvalidOperationException);

        await using var verification = TenancyTestScope.CreateContext(database.ConnectionString);
        var storedOrganization = await verification.Organizations
            .AsNoTracking()
            .SingleAsync(item => item.Id == organization.Id, cancellationToken);
        var storedMembership = await verification.Memberships
            .AsNoTracking()
            .SingleAsync(
                item => item.OrganizationId == organization.Id && item.UserId == ownerId,
                cancellationToken);
        Assert.True(
            storedOrganization.Status != OrganizationStatus.Active
                || storedMembership.Status == MembershipStatus.Active,
            "An active organization must retain an active owner.");
        Assert.Equal(
            storedOrganization.Status == OrganizationStatus.Active ? 1 : 0,
            await verification.SecurityEvents.CountAsync(
                item => item.OrganizationId == organization.Id,
                cancellationToken));
    }

    [Fact]
    public async Task Revocation_refreshes_tracked_state_after_acquiring_the_organization_lock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var setup = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var organization = await setup.Service.GetOrCreateProvisioningAsync(
            "Tracked state",
            $"tracked-state-{Guid.NewGuid():N}",
            cancellationToken);
        var ownerId = Guid.CreateVersion7();
        await setup.Service.EnsureOwnerMembershipAsync(
            organization.Id,
            ownerId,
            cancellationToken);

        await using var staleContext = TenancyTestScope.CreateContext(database.ConnectionString);
        _ = await staleContext.Organizations.SingleAsync(
            item => item.Id == organization.Id,
            cancellationToken);
        _ = await staleContext.Memberships.SingleAsync(
            item => item.OrganizationId == organization.Id && item.UserId == ownerId,
            cancellationToken);

        await using (var activation = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken))
        {
            await activation.Service.ActivateAsync(
                organization.Id,
                new TenancyAuditContext(ownerId, "trace-tracked-state"),
                cancellationToken);
        }

        var staleService = new TenancyProvisioningService(staleContext, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            staleService.RevokeOwnerMembershipAsync(
                organization.Id,
                ownerId,
                cancellationToken));

        await using var verification = TenancyTestScope.CreateContext(database.ConnectionString);
        Assert.Equal(OrganizationStatus.Active, await verification.Organizations
            .Where(item => item.Id == organization.Id)
            .Select(item => item.Status)
            .SingleAsync(cancellationToken));
        Assert.Equal(MembershipStatus.Active, await verification.Memberships
            .Where(item => item.OrganizationId == organization.Id && item.UserId == ownerId)
            .Select(item => item.Status)
            .SingleAsync(cancellationToken));
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

    private static TenancyDbContext CreateContext(NpgsqlConnection connection)
    {
        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(
                connection,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy"))
            .Options;
        return new TenancyDbContext(options);
    }

    private static async Task LockOrganizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM tenancy.organizations WHERE id = @id FOR UPDATE";
        command.Parameters.AddWithValue("id", organizationId);
        Assert.Equal(organizationId, await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<bool> WaitForLockAsync(
        string connectionString,
        int[] processIds,
        Task<OperationOutcome> first,
        Task<OperationOutcome> second,
        CancellationToken cancellationToken)
    {
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync(cancellationToken);
        var deadline = TimeProvider.System.GetUtcNow() + TimeSpan.FromSeconds(5);

        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            await using var command = observer.CreateCommand();
            command.CommandText = """
                SELECT count(*)
                FROM pg_stat_activity
                WHERE pid = ANY(@process_ids) AND wait_event_type = 'Lock'
                """;
            command.Parameters.AddWithValue("process_ids", processIds);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == processIds.Length)
            {
                return true;
            }

            if (first.IsCompleted || second.IsCompleted)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        return false;
    }

    private static async Task<OperationOutcome> ObserveAsync(Task operation)
    {
        try
        {
            await operation;
            return new OperationOutcome(null);
        }
        catch (Exception exception)
        {
            return new OperationOutcome(exception);
        }
    }

    private sealed record OperationOutcome(Exception? Error);
}
