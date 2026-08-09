using Microsoft.EntityFrameworkCore;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Security;
using Xunit;

namespace Puntiro.IntegrationTests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class IdentitySecurityEpochTests(PostgresDatabase database)
{
    [Fact]
    public async Task Suspension_advances_the_epoch_and_rejects_a_previously_verified_identity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var organizationId = Guid.CreateVersion7();
        var identity = await scope.CreateVerifiedIdentityAsync(
            VerifiedFactor.Totp,
            organizationId,
            cancellationToken);

        await scope.Provisioning.SuspendAsync(
            identity.UserId,
            new IdentityAuditContext(identity.UserId, "trace-suspend-001"),
            cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sessions.CreateAsync(
            identity,
            organizationId,
            new IdentityAuditContext(identity.UserId, "trace-create-stale-001"),
            cancellationToken));
        var user = await scope.Context.AdminUsers.AsNoTracking()
            .SingleAsync(item => item.Id == identity.UserId, cancellationToken);
        Assert.True(user.AuthenticationEpoch > identity.AuthenticationEpoch);
    }

    [Fact]
    public async Task Suspension_from_another_scope_invalidates_stale_tracked_session_state()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var stale = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var organizationId = Guid.CreateVersion7();
        var identity = await stale.CreateVerifiedIdentityAsync(
            VerifiedFactor.RecoveryCode,
            organizationId,
            cancellationToken);
        using var issued = await stale.Sessions.CreateAsync(identity, organizationId, cancellationToken);
        var raw = issued.RawToken.Reveal();

        await using (var suspension = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            migrate: false))
        {
            await suspension.Provisioning.SuspendAsync(
                identity.UserId,
                new IdentityAuditContext(identity.UserId, "trace-suspend-stale-001"),
                cancellationToken);
        }

        Assert.Null(await stale.Sessions.ValidateAsync(raw, cancellationToken));
    }

    [Fact]
    public async Task Reset_from_another_scope_rejects_session_creation_from_a_stale_verified_identity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var stale = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var organizationId = Guid.CreateVersion7();
        var email = IdentityTestScope.UniqueEmail();
        using var pending = await stale.Provisioning.BeginOwnerAsync(
            organizationId,
            email,
            IdentityTestScope.Password,
            cancellationToken);
        var oldSecret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
        var confirmation = Rfc6238Totp.Generate(
            oldSecret,
            stale.Time.GetUtcNow().ToUnixTimeSeconds());
        await stale.Provisioning.ConfirmOwnerTotpAsync(pending.UserId, confirmation, cancellationToken);
        await stale.Provisioning.CompleteOwnerAsync(pending.UserId, organizationId, cancellationToken);
        stale.Time.SetUtcNow(stale.Time.GetUtcNow().AddSeconds(30));
        var loginCode = Rfc6238Totp.Generate(oldSecret, stale.Time.GetUtcNow().ToUnixTimeSeconds());
        var verified = await stale.Authentication.VerifyAsync(
            new AdminCredentials(email, IdentityTestScope.Password, loginCode, null),
            cancellationToken) ?? throw new InvalidOperationException("Test authentication failed.");
        using var reset = await stale.Provisioning.PrepareOwnerTotpResetAsync(
            pending.UserId,
            IdentityTestScope.Password,
            pending.RecoveryCodes[0].Reveal(),
            cancellationToken);
        var newSecret = IdentityTestScope.ReadTotpSecret(reset.TotpUri);
        var firstNewCode = Rfc6238Totp.Generate(
            newSecret,
            stale.Time.GetUtcNow().ToUnixTimeSeconds());

        await using (var resetScope = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now: stale.Time.GetUtcNow(),
            migrate: false))
        {
            await resetScope.Provisioning.CompleteOwnerTotpResetAsync(
                reset,
                firstNewCode,
                cancellationToken);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => stale.Sessions.CreateAsync(
            verified,
            organizationId,
            cancellationToken));
    }

    [Fact]
    public async Task Concurrent_validation_at_the_exact_idle_boundary_never_revives_the_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var createdAt = new DateTimeOffset(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);
        await using var setup = await IdentityTestScope.CreateAsync(database.ConnectionString, now: createdAt);
        var organizationId = Guid.CreateVersion7();
        var identity = await setup.CreateVerifiedIdentityAsync(
            VerifiedFactor.RecoveryCode,
            organizationId,
            cancellationToken);
        using var issued = await setup.Sessions.CreateAsync(identity, organizationId, cancellationToken);
        var raw = issued.RawToken.Reveal();

        var validators = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            IdentityTestScope.CreateAsync(
                database.ConnectionString,
                now: issued.Principal.IdleExpiresAt,
                migrate: false)));
        try
        {
            var results = await Task.WhenAll(validators.Select(scope =>
                scope.Sessions.ValidateAsync(raw, cancellationToken)));
            Assert.All(results, Assert.Null);
        }
        finally
        {
            foreach (var validator in validators)
            {
                await validator.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Reset_racing_stale_session_creation_leaves_no_post_reset_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = new DateTimeOffset(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);
        Guid organizationId = Guid.CreateVersion7();
        VerifiedIdentity verified;
        PendingOwnerTotpReset reset;
        string firstNewCode;
        await using (var setup = await IdentityTestScope.CreateAsync(database.ConnectionString, now))
        {
            var email = IdentityTestScope.UniqueEmail();
            using var pending = await setup.Provisioning.BeginOwnerAsync(
                organizationId,
                email,
                IdentityTestScope.Password,
                cancellationToken);
            var oldSecret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
            var confirmation = Rfc6238Totp.Generate(oldSecret, setup.Time.GetUtcNow().ToUnixTimeSeconds());
            await setup.Provisioning.ConfirmOwnerTotpAsync(pending.UserId, confirmation, cancellationToken);
            await setup.Provisioning.CompleteOwnerAsync(pending.UserId, organizationId, cancellationToken);
            setup.Time.SetUtcNow(setup.Time.GetUtcNow().AddSeconds(30));
            now = setup.Time.GetUtcNow();
            var loginCode = Rfc6238Totp.Generate(oldSecret, now.ToUnixTimeSeconds());
            verified = await setup.Authentication.VerifyAsync(
                new AdminCredentials(email, IdentityTestScope.Password, loginCode, null),
                cancellationToken) ?? throw new InvalidOperationException("Test authentication failed.");
            reset = await setup.Provisioning.PrepareOwnerTotpResetAsync(
                pending.UserId,
                IdentityTestScope.Password,
                pending.RecoveryCodes[0].Reveal(),
                cancellationToken);
            var newSecret = IdentityTestScope.ReadTotpSecret(reset.TotpUri);
            firstNewCode = Rfc6238Totp.Generate(newSecret, now.ToUnixTimeSeconds());
        }

        using (reset)
        await using (var resetScope = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now,
            migrate: false))
        await using (var createScope = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now,
            migrate: false))
        {
            IssuedAdminSession? issued = null;
            Exception? createFailure = null;
            var createTask = Task.Run(async () =>
            {
                try
                {
                    issued = await createScope.Sessions.CreateAsync(
                        verified,
                        organizationId,
                        new IdentityAuditContext(verified.UserId, "trace-reset-race-create"),
                        cancellationToken);
                }
                catch (InvalidOperationException exception)
                {
                    createFailure = exception;
                }
            }, cancellationToken);
            var resetTask = resetScope.Provisioning.CompleteOwnerTotpResetAsync(
                reset,
                firstNewCode,
                new IdentityAuditContext(verified.UserId, "trace-reset-race-complete"),
                cancellationToken);

            await Task.WhenAll(createTask, resetTask);
            Assert.True((issued is null) ^ (createFailure is null));
            if (issued is not null)
            {
                using (issued)
                {
                    var raw = issued.RawToken.Reveal();
                    await using var verification = await IdentityTestScope.CreateAsync(
                        database.ConnectionString,
                        now,
                        migrate: false);
                    Assert.Null(await verification.Sessions.ValidateAsync(raw, cancellationToken));
                }
            }
        }
    }

    [Fact]
    public async Task Suspend_racing_validation_leaves_no_principal_after_commit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = new DateTimeOffset(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);
        Guid userId;
        string raw;
        await using (var setup = await IdentityTestScope.CreateAsync(database.ConnectionString, now))
        {
            var organizationId = Guid.CreateVersion7();
            var identity = await setup.CreateVerifiedIdentityAsync(
                VerifiedFactor.RecoveryCode,
                organizationId,
                cancellationToken);
            userId = identity.UserId;
            using var issued = await setup.Sessions.CreateAsync(identity, organizationId, cancellationToken);
            raw = issued.RawToken.Reveal();
        }

        await using var suspension = await IdentityTestScope.CreateAsync(database.ConnectionString, now, migrate: false);
        await using var validation = await IdentityTestScope.CreateAsync(database.ConnectionString, now, migrate: false);
        var suspensionTask = suspension.Provisioning.SuspendAsync(
            userId,
            new IdentityAuditContext(userId, "trace-suspend-race"),
            cancellationToken);
        var validationTask = validation.Sessions.ValidateAsync(raw, cancellationToken);
        await Task.WhenAll(suspensionTask, validationTask);

        await using var after = await IdentityTestScope.CreateAsync(database.ConnectionString, now, migrate: false);
        Assert.Null(await after.Sessions.ValidateAsync(raw, cancellationToken));
    }
}
