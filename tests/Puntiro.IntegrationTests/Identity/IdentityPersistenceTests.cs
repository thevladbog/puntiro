using Microsoft.EntityFrameworkCore;
using Npgsql;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Domain;
using Xunit;
using Modules = Puntiro.Modules;
using Security = Puntiro.Security;

namespace Puntiro.IntegrationTests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class IdentityPersistenceTests(PostgresDatabase database)
{
    [Fact]
    public async Task Migration_owns_only_identity_schema_and_enforces_normalized_email_uniqueness()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        using var first = await scope.Provisioning.BeginOwnerAsync(
            Guid.CreateVersion7(), "Owner@Example.COM", IdentityTestScope.Password, cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Provisioning.BeginOwnerAsync(
                Guid.CreateVersion7(), " owner@example.com ", IdentityTestScope.Password, cancellationToken));

        var schemas = await scope.Context.Database.SqlQueryRaw<string>(
                "SELECT DISTINCT table_schema AS \"Value\" FROM information_schema.tables " +
                "WHERE table_name IN ('admin_users','password_credentials','totp_credentials'," +
                "'recovery_codes','sessions','security_events')")
            .ToListAsync(cancellationToken);
        Assert.Equal(["identity"], schemas);
        Assert.Empty(await scope.Context.Database.GetPendingMigrationsAsync(cancellationToken));

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var duplicate = connection.CreateCommand();
        duplicate.CommandText =
            """
            INSERT INTO identity.admin_users
                (id, display_email, normalized_email, status,
                 provisioning_organization_id, created_at, updated_at, version)
            VALUES ($1, 'Duplicate', 'owner@example.com', 'provisioning', $2, $3, $3, 1)
            """;
        duplicate.Parameters.AddWithValue(Guid.CreateVersion7());
        duplicate.Parameters.AddWithValue(Guid.CreateVersion7());
        duplicate.Parameters.AddWithValue(DateTimeOffset.UtcNow);
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            duplicate.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public async Task Totp_secret_is_encrypted_before_persistence_and_recovery_codes_are_hmac_only()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        using var pending = await scope.Provisioning.BeginOwnerAsync(
            Guid.CreateVersion7(), IdentityTestScope.UniqueEmail(), IdentityTestScope.Password, cancellationToken);
        var secret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT protected_secret FROM identity.totp_credentials WHERE user_id = $1";
        command.Parameters.AddWithValue(pending.UserId);
        var protectedSecret = (byte[])(await command.ExecuteScalarAsync(cancellationToken))!;

        Assert.NotEqual(secret, protectedSecret);
        Assert.DoesNotContain(secret, protectedSecret);
        Assert.All(scope.Context.RecoveryCodes, row => Assert.Equal(32, row.Verifier.Length));

        await using var mutation = connection.CreateCommand();
        mutation.CommandText =
            "UPDATE identity.security_events SET result = 'failure' WHERE user_id = $1";
        mutation.Parameters.AddWithValue(pending.UserId);
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            mutation.ExecuteNonQueryAsync(cancellationToken));
        Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, error.SqlState);
    }
}

internal sealed class IdentityTestScope : IAsyncDisposable
{
    internal const string Password = "correct horse battery staple";
    private static readonly Microsoft.AspNetCore.DataProtection.IDataProtectionProvider DataProtectionProvider =
        new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();
    private readonly Modules.Identity.Security.RecoveryCodeService _recoveryCodes;

    private IdentityTestScope(
        Modules.Identity.Persistence.IdentityDbContext context,
        ManualTimeProvider time,
        Modules.Identity.Services.IdentityProvisioningService provisioning,
        Modules.Identity.Services.AdminAuthenticationService authentication,
        Modules.Identity.Services.AdminSessionService sessions,
        Modules.Identity.Security.RecoveryCodeService recoveryCodes)
    {
        Context = context;
        Time = time;
        Provisioning = provisioning;
        Authentication = authentication;
        Sessions = sessions;
        _recoveryCodes = recoveryCodes;
    }

    internal Modules.Identity.Persistence.IdentityDbContext Context { get; }
    internal ManualTimeProvider Time { get; }
    internal Modules.Identity.Services.IdentityProvisioningService Provisioning { get; }
    internal Modules.Identity.Services.AdminAuthenticationService Authentication { get; }
    internal Modules.Identity.Services.AdminSessionService Sessions { get; }

    internal static async Task<IdentityTestScope> CreateAsync(
        string connectionString,
        DateTimeOffset? now = null,
        bool migrate = true)
    {
        var options = new DbContextOptionsBuilder<Modules.Identity.Persistence.IdentityDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options;
        var context = new Modules.Identity.Persistence.IdentityDbContext(options);
        if (migrate)
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        var time = new ManualTimeProvider(now ?? new DateTimeOffset(2026, 8, 8, 10, 0, 0, TimeSpan.Zero));
        var keys = Modules.Identity.Security.IdentityKeyOptions.ForTesting(
            "session-v1", Enumerable.Repeat((byte)0x51, 32).ToArray(),
            "recovery-v1", Enumerable.Repeat((byte)0x72, 32).ToArray());
        var secrets = new Security.SystemSecretGenerator();
        var hasher = new Modules.Identity.Security.PasswordHasher(secrets);
        var totp = new Modules.Identity.Security.Rfc6238Totp(time, secrets);
        var recovery = new Modules.Identity.Security.RecoveryCodeService(
            keys.GetRecoveryKey(keys.CurrentRecoveryKeyVersion), secrets);
        var protector = new Modules.Identity.Security.TotpSecretProtector(
            DataProtectionProvider);
        var codec = new Modules.Identity.Security.SessionTokenCodec(keys, secrets);
        var provisioning = new Modules.Identity.Services.IdentityProvisioningService(
            context,
            hasher,
            totp,
            recovery,
            protector,
            time,
            keys.CurrentRecoveryKeyVersion,
            keys,
            secrets);
        var authentication = new Modules.Identity.Services.AdminAuthenticationService(
            context, hasher, totp, protector, time, keys, secrets);
        var sessions = new Modules.Identity.Services.AdminSessionService(context, codec, time);
        return new IdentityTestScope(context, time, provisioning, authentication, sessions, recovery);
    }

    internal async Task<(PendingOwnerIdentity Pending, string TotpSecret, string RecoveryCode)> BeginAsync(
        Guid? organizationId = null,
        string? email = null)
    {
        var pending = await Provisioning.BeginOwnerAsync(
            organizationId ?? Guid.CreateVersion7(),
            email ?? UniqueEmail(),
            Password,
            TestContext.Current.CancellationToken);
        return (pending, Convert.ToBase64String(ReadTotpSecret(pending.TotpUri)), pending.RecoveryCodes[0].Reveal());
    }

    internal async Task<VerifiedIdentity> CreateVerifiedIdentityAsync(
        VerifiedFactor factor,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var email = UniqueEmail();
        using var pending = await Provisioning.BeginOwnerAsync(
            organizationId,
            email,
            Password,
            cancellationToken);
        var secret = ReadTotpSecret(pending.TotpUri);
        var firstCode = Modules.Identity.Security.Rfc6238Totp.Generate(
            secret,
            Time.GetUtcNow().ToUnixTimeSeconds());
        await Provisioning.ConfirmOwnerTotpAsync(pending.UserId, firstCode, cancellationToken);
        await Provisioning.CompleteOwnerAsync(pending.UserId, organizationId, cancellationToken);

        AdminCredentials credentials;
        if (factor == VerifiedFactor.Totp)
        {
            Time.SetUtcNow(Time.GetUtcNow().AddSeconds(30));
            var nextCode = Modules.Identity.Security.Rfc6238Totp.Generate(
                secret,
                Time.GetUtcNow().ToUnixTimeSeconds());
            credentials = new AdminCredentials(email, Password, nextCode, null);
        }
        else
        {
            credentials = new AdminCredentials(email, Password, null, pending.RecoveryCodes[0].Reveal());
        }

        return await Authentication.VerifyAsync(credentials, cancellationToken)
            ?? throw new InvalidOperationException("Test identity could not authenticate.");
    }

    internal static byte[] ReadTotpSecret(Security.SensitiveValue uri)
    {
        var raw = uri.Reveal();
        var query = raw[(raw.IndexOf('?') + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        Span<byte> secret = stackalloc byte[20];
        Assert.True(Security.Base32.TryDecode(query["secret"], secret, out var written));
        Assert.Equal(20, written);
        return secret.ToArray();
    }

    internal static string UniqueEmail() => $"owner-{Guid.NewGuid():N}@example.test";

    public async ValueTask DisposeAsync()
    {
        _recoveryCodes.Dispose();
        await Context.DisposeAsync();
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;
    public override DateTimeOffset GetUtcNow() => _utcNow;
    internal void SetUtcNow(DateTimeOffset value) => _utcNow = value;
}
