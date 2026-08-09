using Microsoft.EntityFrameworkCore;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Security;
using Xunit;

namespace Puntiro.IntegrationTests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class IdentityAuthenticationTests(PostgresDatabase database)
{
    [Fact]
    public async Task Totp_counter_is_accepted_once_and_event_is_committed_with_the_factor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var email = IdentityTestScope.UniqueEmail();
        var organizationId = Guid.CreateVersion7();
        using var pending = await scope.Provisioning.BeginOwnerAsync(
            organizationId, email, IdentityTestScope.Password, cancellationToken);
        var secret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
        var code = Rfc6238Totp.Generate(secret, scope.Time.GetUtcNow().ToUnixTimeSeconds());
        await scope.Provisioning.ConfirmOwnerTotpAsync(pending.UserId, code, cancellationToken);
        await scope.Provisioning.CompleteOwnerAsync(pending.UserId, organizationId, cancellationToken);
        scope.Time.SetUtcNow(scope.Time.GetUtcNow().AddSeconds(30));
        var nextCode = Rfc6238Totp.Generate(secret, scope.Time.GetUtcNow().ToUnixTimeSeconds());

        var first = await scope.Authentication.VerifyAsync(
            new AdminCredentials(email, IdentityTestScope.Password, nextCode, recoveryCode: null), cancellationToken);
        var replay = await scope.Authentication.VerifyAsync(
            new AdminCredentials(email, IdentityTestScope.Password, nextCode, recoveryCode: null), cancellationToken);

        Assert.NotNull(first);
        Assert.Equal(VerifiedFactor.Totp, first.Factor);
        Assert.Null(replay);
        Assert.Contains(scope.Context.SecurityEvents, item =>
            item.EventType == "authentication.login" && item.Result == "success");
    }

    [Fact]
    public async Task Concurrent_recovery_use_has_exactly_one_winner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityTestScope.UniqueEmail();
        string recoveryCode;
        await using (var setup = await IdentityTestScope.CreateAsync(database.ConnectionString))
        {
            var organizationId = Guid.CreateVersion7();
            using var pending = await setup.Provisioning.BeginOwnerAsync(
                organizationId, email, IdentityTestScope.Password, cancellationToken);
            recoveryCode = pending.RecoveryCodes[0].Reveal();
            var secret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
            var firstCode = Rfc6238Totp.Generate(secret, setup.Time.GetUtcNow().ToUnixTimeSeconds());
            await setup.Provisioning.ConfirmOwnerTotpAsync(pending.UserId, firstCode, cancellationToken);
            await setup.Provisioning.CompleteOwnerAsync(pending.UserId, organizationId, cancellationToken);
        }

        await using var left = await IdentityTestScope.CreateAsync(database.ConnectionString, migrate: false);
        await using var right = await IdentityTestScope.CreateAsync(database.ConnectionString, migrate: false);
        var credentials = new AdminCredentials(email, IdentityTestScope.Password, null, recoveryCode);
        var results = await Task.WhenAll(
            left.Authentication.VerifyAsync(credentials, cancellationToken),
            right.Authentication.VerifyAsync(credentials, cancellationToken));

        Assert.Single(results, result => result is not null);
        Assert.Single(results, result => result is null);
    }

    [Fact]
    public async Task Concurrent_totp_use_has_exactly_one_winner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = IdentityTestScope.UniqueEmail();
        byte[] secret;
        DateTimeOffset loginAt;
        await using (var setup = await IdentityTestScope.CreateAsync(database.ConnectionString))
        {
            var organizationId = Guid.CreateVersion7();
            using var pending = await setup.Provisioning.BeginOwnerAsync(
                organizationId,
                email,
                IdentityTestScope.Password,
                cancellationToken);
            secret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
            var confirmation = Rfc6238Totp.Generate(
                secret,
                setup.Time.GetUtcNow().ToUnixTimeSeconds());
            await setup.Provisioning.ConfirmOwnerTotpAsync(
                pending.UserId,
                confirmation,
                cancellationToken);
            await setup.Provisioning.CompleteOwnerAsync(
                pending.UserId,
                organizationId,
                cancellationToken);
            loginAt = setup.Time.GetUtcNow().AddSeconds(30);
        }

        await using var left = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now: loginAt,
            migrate: false);
        await using var right = await IdentityTestScope.CreateAsync(
            database.ConnectionString,
            now: loginAt,
            migrate: false);
        var code = Rfc6238Totp.Generate(secret, loginAt.ToUnixTimeSeconds());
        var credentials = new AdminCredentials(email, IdentityTestScope.Password, code, null);
        var results = await Task.WhenAll(
            left.Authentication.VerifyAsync(credentials, cancellationToken),
            right.Authentication.VerifyAsync(credentials, cancellationToken));

        Assert.Single(results, result => result is not null);
        Assert.Single(results, result => result is null);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_are_both_generic_failures_without_email_in_audit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var realEmail = IdentityTestScope.UniqueEmail();
        using var pending = await scope.Provisioning.BeginOwnerAsync(
            Guid.CreateVersion7(), realEmail, IdentityTestScope.Password, cancellationToken);

        var unknown = await scope.Authentication.VerifyAsync(
            new AdminCredentials("absent@example.test", IdentityTestScope.Password, "123456", null), cancellationToken);
        var wrongPassword = await scope.Authentication.VerifyAsync(
            new AdminCredentials(realEmail, "this is not the password", "123456", null), cancellationToken);

        Assert.Null(unknown);
        Assert.Null(wrongPassword);
        var events = await scope.Context.SecurityEvents.AsNoTracking().ToListAsync(cancellationToken);
        Assert.DoesNotContain(events, item =>
            item.ReasonCode.Contains("absent", StringComparison.OrdinalIgnoreCase) ||
            item.ReasonCode.Contains("example", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Totp_reset_commits_new_factors_and_session_revocation_only_after_first_code()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var organizationId = Guid.CreateVersion7();
        var email = IdentityTestScope.UniqueEmail();
        using var pending = await scope.Provisioning.BeginOwnerAsync(
            organizationId,
            email,
            IdentityTestScope.Password,
            cancellationToken);
        var oldRecovery = pending.RecoveryCodes[0].Reveal();
        var oldSecret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
        var confirmation = Rfc6238Totp.Generate(oldSecret, scope.Time.GetUtcNow().ToUnixTimeSeconds());
        await scope.Provisioning.ConfirmOwnerTotpAsync(pending.UserId, confirmation, cancellationToken);
        await scope.Provisioning.CompleteOwnerAsync(pending.UserId, organizationId, cancellationToken);
        scope.Time.SetUtcNow(scope.Time.GetUtcNow().AddSeconds(30));
        var loginCode = Rfc6238Totp.Generate(oldSecret, scope.Time.GetUtcNow().ToUnixTimeSeconds());
        var verified = await scope.Authentication.VerifyAsync(
            new AdminCredentials(email, IdentityTestScope.Password, loginCode, null),
            cancellationToken);
        using var session = await scope.Sessions.CreateAsync(verified!, organizationId, cancellationToken);
        var rawSession = session.RawToken.Reveal();

        using var reset = await scope.Provisioning.PrepareOwnerTotpResetAsync(
            pending.UserId,
            IdentityTestScope.Password,
            oldRecovery,
            cancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Provisioning.CompleteOwnerTotpResetAsync(reset, "not-a-code", cancellationToken));
        Assert.NotNull(await scope.Sessions.ValidateAsync(rawSession, cancellationToken));

        var newSecret = IdentityTestScope.ReadTotpSecret(reset.TotpUri);
        var firstNewCode = Rfc6238Totp.Generate(newSecret, scope.Time.GetUtcNow().ToUnixTimeSeconds());
        await scope.Provisioning.CompleteOwnerTotpResetAsync(reset, firstNewCode, cancellationToken);

        Assert.Null(await scope.Sessions.ValidateAsync(rawSession, cancellationToken));
        Assert.Null(await scope.Authentication.VerifyAsync(
            new AdminCredentials(email, IdentityTestScope.Password, null, oldRecovery),
            cancellationToken));
        Assert.NotNull(await scope.Authentication.VerifyAsync(
            new AdminCredentials(
                email,
                IdentityTestScope.Password,
                null,
                reset.RecoveryCodes[0].Reveal()),
            cancellationToken));
    }

    [Fact]
    public async Task Reset_preparation_clears_the_candidate_totp_when_recovery_batch_generation_fails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IdentityTestScope.CreateAsync(database.ConnectionString);
        var organizationId = Guid.CreateVersion7();
        var email = IdentityTestScope.UniqueEmail();
        using var pending = await scope.Provisioning.BeginOwnerAsync(
            organizationId,
            email,
            IdentityTestScope.Password,
            cancellationToken);
        var oldRecovery = pending.RecoveryCodes[0].Reveal();
        var oldSecret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
        var confirmation = Rfc6238Totp.Generate(oldSecret, scope.Time.GetUtcNow().ToUnixTimeSeconds());
        await scope.Provisioning.ConfirmOwnerTotpAsync(pending.UserId, confirmation, cancellationToken);
        await scope.Provisioning.CompleteOwnerAsync(pending.UserId, organizationId, cancellationToken);

        var keys = IdentityTestScope.CreateKeys();
        var secretGenerator = new Puntiro.Security.SystemSecretGenerator();
        var candidateTotp = new TrackingTotpService();
        using var faultingRecovery = new FaultingRecoveryService();
        var service = new Puntiro.Modules.Identity.Services.IdentityProvisioningService(
            scope.Context,
            new PasswordHasher(secretGenerator),
            candidateTotp,
            faultingRecovery,
            new Puntiro.Modules.Identity.Security.TotpSecretProtector(
                IdentityTestScope.DataProtectionProvider),
            scope.Time,
            keys,
            secretGenerator);

        await Assert.ThrowsAsync<ResetSecretAcquisitionException>(() =>
            service.PrepareOwnerTotpResetAsync(
                pending.UserId,
                IdentityTestScope.Password,
                oldRecovery,
                new IdentityAuditContext(pending.UserId, "trace-reset-cleanup-001"),
                cancellationToken));

        Assert.All(candidateTotp.Secret, value => Assert.Equal(0, value));
    }

    private sealed class TrackingTotpService : ITotpService
    {
        internal byte[] Secret { get; } = Enumerable.Repeat((byte)0x63, 20).ToArray();

        public byte[] GenerateSecret() => Secret;

        public bool TryAccept(
            ReadOnlySpan<byte> secret,
            string code,
            long? lastAcceptedCounter,
            out AcceptedTotp accepted) =>
            throw new NotSupportedException();

        public bool TryAccept(
            ReadOnlySpan<byte> secret,
            string code,
            DateTimeOffset utcNow,
            long? lastAcceptedCounter,
            out AcceptedTotp accepted) =>
            throw new NotSupportedException();
    }

    private sealed class FaultingRecoveryService : IRecoveryCodeService
    {
        public IReadOnlyList<GeneratedRecoveryCode> GenerateBatch() =>
            throw new ResetSecretAcquisitionException();

        public bool Verify(string presentedCode, ReadOnlySpan<byte> expectedVerifier) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    private sealed class ResetSecretAcquisitionException : Exception;
}
