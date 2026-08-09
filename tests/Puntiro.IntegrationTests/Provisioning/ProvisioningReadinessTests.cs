using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Puntiro.IntegrationTests.Identity;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Integrations.Persistence;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Security;
using Puntiro.Provisioning.Cli;
using Xunit;

namespace Puntiro.IntegrationTests.Provisioning;

[Collection(PostgresCollection.Name)]
public sealed class ProvisioningReadinessTests(PostgresDatabase database)
{
    [Fact]
    public async Task Missing_current_data_protection_key_fails_before_prompt_or_bootstrap_mutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var unready = await ProvisioningTestScope.CreateAsync(
            database.ConnectionString,
            dataProtectionKeyRingReady: false);
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password);
        await using var beforeIdentity = unready.NewIdentityContext();
        await using var beforeTenancy = unready.NewTenancyContext();
        var before = new CompositionCountState(
            await beforeIdentity.AdminUsers.AsNoTracking().CountAsync(cancellationToken),
            await beforeIdentity.SecurityEvents.AsNoTracking().CountAsync(cancellationToken),
            await beforeTenancy.Organizations.AsNoTracking().CountAsync(cancellationToken),
            await beforeTenancy.Memberships.AsNoTracking().CountAsync(cancellationToken),
            await beforeTenancy.SecurityEvents.AsNoTracking().CountAsync(cancellationToken));

        var exit = await unready.CreateBootstrap(terminal).RunAsync(
            "No key organization",
            "readiness-no-current-dp-key",
            "readiness.no.current.dp@example.test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.InfrastructureFailure, exit);
        Assert.Equal(0, terminal.PasswordReads);
        await using var identity = unready.NewIdentityContext();
        await using var tenancy = unready.NewTenancyContext();
        Assert.Equal(before, new CompositionCountState(
            await identity.AdminUsers.AsNoTracking().CountAsync(cancellationToken),
            await identity.SecurityEvents.AsNoTracking().CountAsync(cancellationToken),
            await tenancy.Organizations.AsNoTracking().CountAsync(cancellationToken),
            await tenancy.Memberships.AsNoTracking().CountAsync(cancellationToken),
            await tenancy.SecurityEvents.AsNoTracking().CountAsync(cancellationToken)));
        Assert.False(await tenancy.Organizations.AsNoTracking().AnyAsync(
            item => item.Slug == "readiness-no-current-dp-key",
            cancellationToken));
    }

    [Fact]
    public async Task Missing_historical_recovery_key_fails_before_prompt_or_mutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var healthy = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await healthy.BootstrapAsync(
            "readiness-recovery-key",
            "readiness.recovery@example.test",
            cancellationToken);
        var before = await CaptureAsync(healthy, fixture.UserId, cancellationToken);
        var keys = CreateKeys(
            "session-v1", 0x51,
            "recovery-v2", 0x73);
        await using var unready = await ProvisioningTestScope.CreateAsync(
            database.ConnectionString,
            keys,
            IdentityTestScope.DataProtectionProvider,
            migrate: false,
            now: healthy.Time.GetUtcNow());
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            RecoveryCode = fixture.RecoveryCodes[0]
        };

        var exit = await unready.CreateReset(terminal).RunAsync(
            "readiness-recovery-key",
            "readiness.recovery@example.test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.InfrastructureFailure, exit);
        Assert.Equal(0, terminal.PasswordReads);
        Assert.Equal(before, await CaptureAsync(unready, fixture.UserId, cancellationToken));
    }

    [Fact]
    public async Task Missing_historical_session_key_fails_before_prompt_or_mutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var healthy = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await healthy.BootstrapAsync(
            "readiness-session-key",
            "readiness.session@example.test",
            cancellationToken);
        healthy.Time.SetUtcNow(healthy.Time.GetUtcNow().AddSeconds(30));
        var verified = await healthy.Services.GetRequiredService<IAdminAuthenticationService>()
            .VerifyAsync(
                new AdminCredentials(
                    "readiness.session@example.test",
                    IdentityTestScope.Password,
                    GenerateTotp(fixture.TotpUri, healthy.Time),
                    null),
                new IdentityAuditContext(null, "readiness-session-login"),
                cancellationToken) ?? throw new InvalidOperationException("Fixture login failed.");
        using var issued = await healthy.Services.GetRequiredService<IAdminSessionService>()
            .CreateAsync(
                verified,
                fixture.OrganizationId,
                new IdentityAuditContext(verified.UserId, "readiness-session-create"),
                cancellationToken);
        var before = await CaptureAsync(healthy, fixture.UserId, cancellationToken);
        var keys = CreateKeys(
            "session-v2", 0x52,
            "recovery-v1", 0x72);
        await using var unready = await ProvisioningTestScope.CreateAsync(
            database.ConnectionString,
            keys,
            IdentityTestScope.DataProtectionProvider,
            migrate: false,
            now: healthy.Time.GetUtcNow());
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            RecoveryCode = fixture.RecoveryCodes[0]
        };

        var exit = await unready.CreateReset(terminal).RunAsync(
            "readiness-session-key",
            "readiness.session@example.test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.InfrastructureFailure, exit);
        Assert.Equal(0, terminal.PasswordReads);
        Assert.Equal(before, await CaptureAsync(unready, fixture.UserId, cancellationToken));
    }

    [Fact]
    public async Task Unreadable_active_totp_payload_fails_before_prompt_or_mutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var healthy = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await healthy.BootstrapAsync(
            "readiness-dp",
            "readiness.dp@example.test",
            cancellationToken);
        var before = await CaptureAsync(healthy, fixture.UserId, cancellationToken);
        await using var unready = await ProvisioningTestScope.CreateAsync(
            database.ConnectionString,
            IdentityTestScope.CreateKeys(),
            new EphemeralDataProtectionProvider(),
            migrate: false,
            now: healthy.Time.GetUtcNow());
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            RecoveryCode = fixture.RecoveryCodes[0]
        };

        var exit = await unready.CreateReset(terminal).RunAsync(
            "readiness-dp",
            "readiness.dp@example.test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.InfrastructureFailure, exit);
        Assert.Equal(0, terminal.PasswordReads);
        Assert.Equal(before, await CaptureAsync(unready, fixture.UserId, cancellationToken));
    }

    [Fact]
    public async Task Provisioning_composition_owns_exact_identity_and_tenancy_schema_table_pairs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        await using (var integrations = new IntegrationsDbContext(
            new DbContextOptionsBuilder<IntegrationsDbContext>()
                .UseNpgsql(database.ConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "integrations"))
                .Options))
        {
            await integrations.Database.MigrateAsync(cancellationToken);
        }
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema = ANY(@schemas)
            ORDER BY table_schema, table_name
            """;
        command.Parameters.AddWithValue("schemas", new[] { "identity", "tenancy" });
        var actual = new List<(string Schema, string Table)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            actual.Add((reader.GetString(0), reader.GetString(1)));
        }

        Assert.Equal(
            new (string Schema, string Table)[]
            {
                ("identity", "__EFMigrationsHistory"),
                ("identity", "admin_users"),
                ("identity", "password_credentials"),
                ("identity", "recovery_codes"),
                ("identity", "security_events"),
                ("identity", "sessions"),
                ("identity", "totp_credentials"),
                ("tenancy", "__EFMigrationsHistory"),
                ("tenancy", "memberships"),
                ("tenancy", "organizations"),
                ("tenancy", "security_events")
            },
            actual);
    }

    private static IdentityKeyOptions CreateKeys(
        string sessionVersion,
        byte sessionByte,
        string recoveryVersion,
        byte recoveryByte) => new(
            sessionVersion,
            new Dictionary<string, byte[]> { [sessionVersion] = Enumerable.Repeat(sessionByte, 32).ToArray() },
            recoveryVersion,
            new Dictionary<string, byte[]> { [recoveryVersion] = Enumerable.Repeat(recoveryByte, 32).ToArray() });

    private static async Task<ReadinessState> CaptureAsync(
        ProvisioningTestScope scope,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var context = scope.NewIdentityContext();
        await using var tenancy = scope.NewTenancyContext();
        var protectedTotp = await context.TotpCredentials.AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => item.ProtectedSecret)
                .SingleAsync(cancellationToken);
        return new ReadinessState(
            Convert.ToHexString(protectedTotp),
            await context.RecoveryCodes.AsNoTracking()
                .Where(item => item.UserId == userId && item.UsedAtUtc == null)
                .CountAsync(cancellationToken),
            await context.Sessions.AsNoTracking()
                .Where(item => item.UserId == userId && item.RevokedAtUtc == null)
                .CountAsync(cancellationToken),
            await context.SecurityEvents.AsNoTracking().CountAsync(cancellationToken),
            await tenancy.Organizations.AsNoTracking().CountAsync(cancellationToken),
            await tenancy.Memberships.AsNoTracking().CountAsync(cancellationToken),
            await tenancy.SecurityEvents.AsNoTracking().CountAsync(cancellationToken));
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

    private sealed record ReadinessState(
        string ProtectedTotp,
        int UnusedRecoveryCodes,
        int ActiveSessions,
        int IdentitySecurityEvents,
        int Organizations,
        int Memberships,
        int TenancySecurityEvents);

    private sealed record CompositionCountState(
        int AdminUsers,
        int IdentitySecurityEvents,
        int Organizations,
        int Memberships,
        int TenancySecurityEvents);
}
