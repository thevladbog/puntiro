using Microsoft.EntityFrameworkCore;
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
}
