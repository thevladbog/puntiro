using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Puntiro.IntegrationTests.Identity;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Security;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Domain;
using Puntiro.Modules.Tenancy.Persistence;
using Puntiro.Modules.Tenancy.Services;
using Puntiro.Provisioning.Cli;
using Xunit;

namespace Puntiro.IntegrationTests.Provisioning;

[Collection(PostgresCollection.Name)]
public sealed class OwnerRecoveryAuthorizationRaceTests(PostgresDatabase database)
{
    [Fact]
    public async Task Revocation_winning_before_final_authorization_prevents_factor_rotation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await scope.BootstrapAsync(
            "race-revoke-lost",
            "race.revoke.lost@example.test",
            cancellationToken);
        var tenancy = scope.Services.GetRequiredService<ITenancyProvisioningService>();
        await tenancy.EnsureOwnerMembershipAsync(
            fixture.OrganizationId,
            Guid.CreateVersion7(),
            cancellationToken);
        var before = await ReadFactorsAsync(scope, fixture.UserId, cancellationToken);
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            RecoveryCode = fixture.RecoveryCodes[0],
            EnrollmentCaptured = scope.RememberEnrollment,
            TotpFactory = async token =>
            {
                await using var competitor = await ProvisioningTestScope.CreateAsync(
                    database.ConnectionString,
                    migrate: false,
                    now: scope.Time.GetUtcNow());
                await competitor.Services.GetRequiredService<ITenancyProvisioningService>()
                    .RevokeOwnerMembershipAsync(fixture.OrganizationId, fixture.UserId, token);
                return scope.GenerateCurrentTotp();
            }
        };

        var exit = await scope.CreateReset(terminal).RunAsync(
            "race-revoke-lost",
            "race.revoke.lost@example.test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.InvalidCredentials, exit);
        Assert.Equal(before, await ReadFactorsAsync(scope, fixture.UserId, cancellationToken));
    }

    [Fact]
    public async Task Suspension_winning_before_final_authorization_prevents_factor_rotation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await scope.BootstrapAsync(
            "race-suspend-lost",
            "race.suspend.lost@example.test",
            cancellationToken);
        var before = await ReadFactorsAsync(scope, fixture.UserId, cancellationToken);
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            RecoveryCode = fixture.RecoveryCodes[0],
            EnrollmentCaptured = scope.RememberEnrollment,
            TotpFactory = async token =>
            {
                await using var competitor = await ProvisioningTestScope.CreateAsync(
                    database.ConnectionString,
                    migrate: false,
                    now: scope.Time.GetUtcNow());
                await competitor.Services.GetRequiredService<ITenancyProvisioningService>()
                    .SuspendAsync(
                        fixture.OrganizationId,
                        new TenancyAuditContext(fixture.UserId, "race-suspend-wins"),
                        token);
                return scope.GenerateCurrentTotp();
            }
        };

        var exit = await scope.CreateReset(terminal).RunAsync(
            "race-suspend-lost",
            "race.suspend.lost@example.test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.InvalidCredentials, exit);
        Assert.Equal(before, await ReadFactorsAsync(scope, fixture.UserId, cancellationToken));
    }

    [Fact]
    public async Task Final_authorization_lease_serializes_reset_before_concurrent_revocation_without_deadlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await scope.BootstrapAsync(
            "race-revoke-reset-wins",
            "race.revoke.reset@example.test",
            cancellationToken);
        var tenancy = scope.Services.GetRequiredService<ITenancyProvisioningService>();
        await tenancy.EnsureOwnerMembershipAsync(
            fixture.OrganizationId,
            Guid.CreateVersion7(),
            cancellationToken);

        await AssertResetLeaseSerializesAsync(
            scope,
            fixture,
            (service, token) => service.RevokeOwnerMembershipAsync(
                fixture.OrganizationId,
                fixture.UserId,
                token),
            cancellationToken);
    }

    [Fact]
    public async Task Final_authorization_lease_serializes_reset_before_concurrent_suspension_without_deadlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await scope.BootstrapAsync(
            "race-suspend-reset-wins",
            "race.suspend.reset@example.test",
            cancellationToken);

        await AssertResetLeaseSerializesAsync(
            scope,
            fixture,
            (service, token) => service.SuspendAsync(
                fixture.OrganizationId,
                new TenancyAuditContext(fixture.UserId, "race-suspend-after-reset"),
                token),
            cancellationToken);
    }

    private async Task AssertResetLeaseSerializesAsync(
        ProvisioningTestScope scope,
        BootstrapFixture fixture,
        Func<ITenancyProvisioningService, CancellationToken, Task> competingOperation,
        CancellationToken cancellationToken)
    {
        var identity = scope.Services.GetRequiredService<IIdentityProvisioningService>();
        using var pending = await identity.PrepareOwnerTotpResetAsync(
            fixture.UserId,
            IdentityTestScope.Password,
            fixture.RecoveryCodes[0],
            new IdentityAuditContext(fixture.UserId, "race-reset-prepare"),
            cancellationToken);
        var newTotp = GenerateTotp(pending.TotpUri.Reveal(), scope.Time);
        await using var lease = await scope.Services.GetRequiredService<ITenancyProvisioningService>()
            .TryAcquireActiveOwnerMutationLeaseForTrustedProvisioningAsync(
                fixture.OrganizationId,
                fixture.UserId,
                cancellationToken);
        Assert.NotNull(lease);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var context = CreateTenancyContext(connection);
        var competitor = new TenancyProvisioningService(context, scope.Time);
        var competingTask = competingOperation(competitor, cancellationToken);
        Assert.True(await WaitForOrganizationLockAsync(connection.ProcessID, cancellationToken));

        await identity.CompleteOwnerTotpResetAsync(
            pending,
            newTotp,
            new IdentityAuditContext(fixture.UserId, "race-reset-complete"),
            cancellationToken);
        await lease.DisposeAsync();

        var finished = await Task.WhenAny(
            competingTask,
            Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
        Assert.Same(competingTask, finished);
        await competingTask;
        await using var identityContext = scope.NewIdentityContext();
        Assert.Equal(1, await identityContext.SecurityEvents.AsNoTracking().CountAsync(
            item => item.UserId == fixture.UserId && item.EventType == "owner.totp_reset",
            cancellationToken));
    }

    private async Task<bool> WaitForOrganizationLockAsync(
        int processId,
        CancellationToken cancellationToken)
    {
        await using var observer = new NpgsqlConnection(database.ConnectionString);
        await observer.OpenAsync(cancellationToken);
        var deadline = TimeProvider.System.GetUtcNow() + TimeSpan.FromSeconds(5);
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            await using var command = observer.CreateCommand();
            command.CommandText = """
                SELECT wait_event_type = 'Lock'
                FROM pg_stat_activity
                WHERE pid = @pid
                """;
            command.Parameters.AddWithValue("pid", processId);
            if (await command.ExecuteScalarAsync(cancellationToken) is true)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        return false;
    }

    private static TenancyDbContext CreateTenancyContext(NpgsqlConnection connection) => new(
        new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(connection, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy"))
            .Options);

    private static async Task<FactorState> ReadFactorsAsync(
        ProvisioningTestScope scope,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var context = scope.NewIdentityContext();
        var protectedTotp = await context.TotpCredentials.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.ProtectedSecret)
            .SingleAsync(cancellationToken);
        return new FactorState(
            Convert.ToHexString(protectedTotp),
            string.Join(",", await context.RecoveryCodes.AsNoTracking()
                .Where(item => item.UserId == userId && item.UsedAtUtc == null)
                .Select(item => item.Id)
                .OrderBy(item => item)
                .ToArrayAsync(cancellationToken)),
            await context.SecurityEvents.AsNoTracking().CountAsync(
                item => item.UserId == userId && item.EventType == "owner.totp_reset",
                cancellationToken));
    }

    private static string GenerateTotp(string uri, ManualTimeProvider time)
    {
        using var sensitiveUri = new Puntiro.Security.SensitiveValue(uri);
        var secret = IdentityTestScope.ReadTotpSecret(sensitiveUri);
        try
        {
            return Rfc6238Totp.Generate(secret, time.GetUtcNow().ToUnixTimeSeconds());
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(secret);
        }
    }

    private sealed record FactorState(
        string ProtectedTotp,
        string RecoveryCodeIds,
        int ResetEvents);
}
