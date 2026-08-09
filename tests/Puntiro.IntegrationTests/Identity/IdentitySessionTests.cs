using Microsoft.EntityFrameworkCore;
using Npgsql;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Identity.Contracts;
using Xunit;
using Modules = Puntiro.Modules;

namespace Puntiro.IntegrationTests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class IdentitySessionTests(PostgresDatabase database)
{
    [Fact]
    public async Task Recovery_login_does_not_grant_fresh_step_up_and_raw_token_is_not_persisted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var organizationId = Guid.CreateVersion7();
        var email = IdentityTestScope.UniqueEmail();
        using var pending = await scope.Provisioning.BeginOwnerAsync(
            organizationId, email, IdentityTestScope.Password, cancellationToken);
        var recovery = pending.RecoveryCodes[0].Reveal();
        var secret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
        var firstCode = Modules.Identity.Security.Rfc6238Totp.Generate(
            secret, scope.Time.GetUtcNow().ToUnixTimeSeconds());
        await scope.Provisioning.ConfirmOwnerTotpAsync(pending.UserId, firstCode, cancellationToken);
        await scope.Provisioning.CompleteOwnerAsync(pending.UserId, organizationId, cancellationToken);
        var verified = await scope.Authentication.VerifyAsync(
            new AdminCredentials(email, IdentityTestScope.Password, null, recovery), cancellationToken);

        using var session = await scope.Sessions.CreateAsync(verified!, organizationId, cancellationToken);
        var raw = session.RawToken.Reveal();

        Assert.Null(session.Principal.SecondFactorVerifiedAt);
        Assert.NotEqual(
            raw,
            Convert.ToBase64String(scope.Context.Sessions.Single(item =>
                item.Id == session.Principal.SessionId).Verifier));
        Assert.NotNull(await scope.Sessions.ValidateAsync(raw, cancellationToken));
    }

    [Theory]
    [InlineData(29, true)]
    [InlineData(30, false)]
    public async Task Session_honors_idle_timeout(int inactiveMinutes, bool expectedValid)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var organizationId = Guid.CreateVersion7();
        var identity = await scope.CreateVerifiedIdentityAsync(VerifiedFactor.RecoveryCode, organizationId, cancellationToken);
        using var issued = await scope.Sessions.CreateAsync(identity, organizationId, cancellationToken);
        var raw = issued.RawToken.Reveal();

        scope.Time.SetUtcNow(scope.Time.GetUtcNow().AddMinutes(inactiveMinutes));

        Assert.Equal(expectedValid, await scope.Sessions.ValidateAsync(raw, cancellationToken) is not null);
    }

    [Fact]
    public async Task Absolute_expiry_and_revoke_are_immediate_and_wrong_hmac_fails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var organizationId = Guid.CreateVersion7();
        var identity = await scope.CreateVerifiedIdentityAsync(VerifiedFactor.Totp, organizationId, cancellationToken);
        using var issued = await scope.Sessions.CreateAsync(identity, organizationId, cancellationToken);
        var raw = issued.RawToken.Reveal();
        var wrong = raw[..^1] + (raw[^1] == 'A' ? "B" : "A");

        Assert.Null(await scope.Sessions.ValidateAsync(wrong, cancellationToken));
        await scope.Sessions.RevokeAsync(issued.Principal.SessionId, "logout", cancellationToken);
        Assert.Null(await scope.Sessions.ValidateAsync(raw, cancellationToken));

        using var absolute = await scope.Sessions.CreateAsync(identity, organizationId, cancellationToken);
        var absoluteRaw = absolute.RawToken.Reveal();
        scope.Time.SetUtcNow(scope.Time.GetUtcNow().AddHours(12));
        Assert.Null(await scope.Sessions.ValidateAsync(absoluteRaw, cancellationToken));
    }

    [Fact]
    public async Task Step_up_freshness_is_persisted_only_for_a_current_active_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var organizationId = Guid.CreateVersion7();
        var identity = await scope.CreateVerifiedIdentityAsync(
            VerifiedFactor.RecoveryCode,
            organizationId,
            cancellationToken);
        using var issued = await scope.Sessions.CreateAsync(identity, organizationId, cancellationToken);
        var verifiedAt = scope.Time.GetUtcNow();

        var updated = await scope.Sessions.RecordStepUpAsync(
            issued.Principal.SessionId,
            verifiedAt,
            cancellationToken);

        Assert.NotNull(updated);
        Assert.Equal(verifiedAt, updated.SecondFactorVerifiedAt);
        Assert.Equal(
            verifiedAt,
            (await scope.Sessions.ValidateAsync(issued.RawToken.Reveal(), cancellationToken))!
                .SecondFactorVerifiedAt);

        await scope.Sessions.RevokeAsync(issued.Principal.SessionId, "logout", cancellationToken);
        Assert.Null(await scope.Sessions.RecordStepUpAsync(
            issued.Principal.SessionId,
            verifiedAt,
            cancellationToken));
    }

    [Fact]
    public async Task Concurrent_revoke_and_step_up_never_leave_a_revoked_session_usable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Guid sessionId;
        string raw;
        DateTimeOffset verifiedAt;
        await using (var setup = await IdentityTestScope.CreateAsync(database.ConnectionString))
        {
            var organizationId = Guid.CreateVersion7();
            var identity = await setup.CreateVerifiedIdentityAsync(
                VerifiedFactor.RecoveryCode,
                organizationId,
                cancellationToken);
            using var issued = await setup.Sessions.CreateAsync(
                identity,
                organizationId,
                cancellationToken);
            sessionId = issued.Principal.SessionId;
            raw = issued.RawToken.Reveal();
            verifiedAt = setup.Time.GetUtcNow();
        }

        await using var left = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now: verifiedAt,
            migrate: false);
        await using var right = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now: verifiedAt,
            migrate: false);

        await Task.WhenAll(
            left.Sessions.RecordStepUpAsync(sessionId, verifiedAt, cancellationToken),
            right.Sessions.RevokeAsync(sessionId, "logout", cancellationToken));

        Assert.Null(await left.Sessions.ValidateAsync(raw, cancellationToken));
    }

    [Fact]
    public async Task Concurrent_account_suspension_and_step_up_never_leave_the_session_usable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Guid sessionId;
        Guid userId;
        string raw;
        DateTimeOffset verifiedAt;
        await using (var setup = await IdentityTestScope.CreateAsync(database.ConnectionString))
        {
            var organizationId = Guid.CreateVersion7();
            var identity = await setup.CreateVerifiedIdentityAsync(
                VerifiedFactor.RecoveryCode,
                organizationId,
                cancellationToken);
            using var issued = await setup.Sessions.CreateAsync(identity, organizationId, cancellationToken);
            sessionId = issued.Principal.SessionId;
            userId = identity.UserId;
            raw = issued.RawToken.Reveal();
            verifiedAt = setup.Time.GetUtcNow();
        }

        await using var left = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now: verifiedAt,
            migrate: false);
        await using var right = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now: verifiedAt,
            migrate: false);
        await Task.WhenAll(
            left.Sessions.RecordStepUpAsync(sessionId, verifiedAt, cancellationToken),
            right.Provisioning.SuspendAsync(userId, cancellationToken));

        Assert.Null(await left.Sessions.ValidateAsync(raw, cancellationToken));
    }

    [Fact]
    public async Task Concurrent_totp_reset_and_step_up_never_leave_the_old_session_usable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Guid sessionId;
        string raw;
        DateTimeOffset verifiedAt;
        PendingOwnerTotpReset reset;
        string firstNewCode;
        await using (var setup = await IdentityTestScope.CreateAsync(database.ConnectionString))
        {
            var organizationId = Guid.CreateVersion7();
            var email = IdentityTestScope.UniqueEmail();
            using var pending = await setup.Provisioning.BeginOwnerAsync(
                organizationId,
                email,
                IdentityTestScope.Password,
                cancellationToken);
            var oldRecovery = pending.RecoveryCodes[0].Reveal();
            var oldSecret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
            var confirmation = Puntiro.Modules.Identity.Security.Rfc6238Totp.Generate(
                oldSecret,
                setup.Time.GetUtcNow().ToUnixTimeSeconds());
            await setup.Provisioning.ConfirmOwnerTotpAsync(
                pending.UserId,
                confirmation,
                cancellationToken);
            await setup.Provisioning.CompleteOwnerAsync(
                pending.UserId,
                organizationId,
                cancellationToken);
            var identity = new VerifiedIdentity(
                pending.UserId,
                1,
                VerifiedFactor.RecoveryCode,
                setup.Time.GetUtcNow());
            using var issued = await setup.Sessions.CreateAsync(
                identity,
                organizationId,
                cancellationToken);
            sessionId = issued.Principal.SessionId;
            raw = issued.RawToken.Reveal();
            verifiedAt = setup.Time.GetUtcNow();
            reset = await setup.Provisioning.PrepareOwnerTotpResetAsync(
                pending.UserId,
                IdentityTestScope.Password,
                oldRecovery,
                cancellationToken);
            var newSecret = IdentityTestScope.ReadTotpSecret(reset.TotpUri);
            firstNewCode = Puntiro.Modules.Identity.Security.Rfc6238Totp.Generate(
                newSecret,
                verifiedAt.ToUnixTimeSeconds());
        }

        using (reset)
        await using (var left = await IdentityTestScope.CreateAsync(
                         database.ConnectionString,
                         now: verifiedAt,
                         migrate: false))
        await using (var right = await IdentityTestScope.CreateAsync(
                         database.ConnectionString,
                         now: verifiedAt,
                         migrate: false))
        {
            await Task.WhenAll(
                left.Sessions.RecordStepUpAsync(sessionId, verifiedAt, cancellationToken),
                right.Provisioning.CompleteOwnerTotpResetAsync(
                    reset,
                    firstNewCode,
                    cancellationToken));
            Assert.Null(await left.Sessions.ValidateAsync(raw, cancellationToken));
        }
    }

    [Fact]
    public async Task Step_up_rechecks_expiry_after_waiting_for_the_user_lock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Guid sessionId;
        Guid userId;
        DateTimeOffset verifiedAt;
        await using (var setup = await IdentityTestScope.CreateAsync(database.ConnectionString))
        {
            var organizationId = Guid.CreateVersion7();
            var identity = await setup.CreateVerifiedIdentityAsync(
                VerifiedFactor.RecoveryCode,
                organizationId,
                cancellationToken);
            using var issued = await setup.Sessions.CreateAsync(identity, organizationId, cancellationToken);
            sessionId = issued.Principal.SessionId;
            userId = identity.UserId;
            verifiedAt = setup.Time.GetUtcNow();
        }

        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync(cancellationToken);
        await using var blockerTransaction = await blocker.BeginTransactionAsync(cancellationToken);
        await using (var command = blocker.CreateCommand())
        {
            command.Transaction = blockerTransaction;
            command.CommandText = "SELECT id FROM identity.admin_users WHERE id = @id FOR UPDATE";
            command.Parameters.AddWithValue("id", userId);
            Assert.Equal(userId, await command.ExecuteScalarAsync(cancellationToken));
        }

        await using var contender = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now: verifiedAt,
            migrate: false);
        var contenderConnection = (NpgsqlConnection)contender.Context.Database.GetDbConnection();
        await contenderConnection.OpenAsync(cancellationToken);
        var recordTask = contender.Sessions.RecordStepUpAsync(
            sessionId,
            verifiedAt,
            cancellationToken);
        try
        {
            Assert.True(await WaitForLockAsync(
                database.ConnectionString,
                contenderConnection.ProcessID,
                recordTask,
                cancellationToken));
            contender.Time.SetUtcNow(verifiedAt.AddMinutes(30));
        }
        finally
        {
            await blockerTransaction.CommitAsync(cancellationToken);
        }

        Assert.Null(await recordTask);
    }

    private static async Task<bool> WaitForLockAsync(
        string connectionString,
        int processId,
        Task operation,
        CancellationToken cancellationToken)
    {
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync(cancellationToken);
        var deadline = TimeProvider.System.GetUtcNow().AddSeconds(5);
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            await using var command = observer.CreateCommand();
            command.CommandText = """
                SELECT count(*)
                FROM pg_stat_activity
                WHERE pid = @process_id AND wait_event_type = 'Lock'
                """;
            command.Parameters.AddWithValue("process_id", processId);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1)
            {
                return true;
            }

            if (operation.IsCompleted) return false;
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        return false;
    }
}
