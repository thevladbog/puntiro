using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Puntiro.IntegrationTests.Identity;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Security;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Provisioning.Cli;
using Xunit;

namespace Puntiro.IntegrationTests.Provisioning;

[Collection(PostgresCollection.Name)]
public sealed class ResetOwnerTotpTests(PostgresDatabase database)
{
    [Fact]
    public async Task Recovery_replaces_factors_and_revokes_sessions_only_after_new_totp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await scope.BootstrapAsync(
            "puntiro-recovery",
            "owner.recovery@example.test",
            cancellationToken);
        scope.Time.SetUtcNow(scope.Time.GetUtcNow().AddSeconds(30));
        var authentication = scope.Services.GetRequiredService<IAdminAuthenticationService>();
        var sessions = scope.Services.GetRequiredService<IAdminSessionService>();
        var oldTotp = GenerateTotp(fixture.TotpUri, scope.Time);
        var verified = await authentication.VerifyAsync(
            new AdminCredentials(
                "owner.recovery@example.test",
                IdentityTestScope.Password,
                oldTotp,
                null),
            new IdentityAuditContext(null, "cli-recovery-login"),
            cancellationToken) ?? throw new InvalidOperationException("Fixture login failed.");
        using var session = await sessions.CreateAsync(
            verified,
            fixture.OrganizationId,
            new IdentityAuditContext(verified.UserId, "cli-recovery-session"),
            cancellationToken);
        var rawSession = session.RawToken.Reveal();
        await using var before = scope.NewIdentityContext();
        var oldRecoveryIds = await before.RecoveryCodes.AsNoTracking()
            .Where(item => item.UserId == fixture.UserId)
            .Select(item => item.Id)
            .OrderBy(item => item)
            .ToArrayAsync(cancellationToken);
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            RecoveryCode = fixture.RecoveryCodes[0],
            EnrollmentCaptured = scope.RememberEnrollment,
            TotpFactory = _ => Task.FromResult(scope.GenerateCurrentTotp())
        };

        var exit = await scope.CreateReset(terminal).RunAsync(
            "PUNTIRO RECOVERY",
            " OWNER.RECOVERY@example.test ",
            cancellationToken);

        Assert.Equal(ProvisioningExit.Success, exit);
        Assert.Null(await sessions.ValidateAsync(rawSession, cancellationToken));
        await using var after = scope.NewIdentityContext();
        var newRecoveryIds = await after.RecoveryCodes.AsNoTracking()
            .Where(item => item.UserId == fixture.UserId)
            .Select(item => item.Id)
            .OrderBy(item => item)
            .ToArrayAsync(cancellationToken);
        Assert.NotEqual(oldRecoveryIds, newRecoveryIds);
        Assert.All(await after.Sessions.Where(item => item.UserId == fixture.UserId).ToArrayAsync(cancellationToken),
            item => Assert.NotNull(item.RevokedAtUtc));
        var resetEvent = await after.SecurityEvents.AsNoTracking().SingleAsync(
            item => item.UserId == fixture.UserId && item.EventType == "owner.totp_reset",
            cancellationToken);
        var eventText = $"{resetEvent.TraceId}:{resetEvent.EventType}:{resetEvent.Result}:{resetEvent.ReasonCode}";
        Assert.False(
            eventText.Contains(IdentityTestScope.Password, StringComparison.Ordinal),
            "The redacted event contained the password fixture.");
        Assert.False(
            eventText.Contains(fixture.RecoveryCodes[0], StringComparison.Ordinal),
            "The redacted event contained the recovery-code fixture.");
        Assert.False(
            eventText.Contains(terminal.TotpUri!, StringComparison.Ordinal),
            "The redacted event contained the enrollment URI fixture.");
    }

    [Fact]
    public async Task Invalid_new_totp_leaves_old_factors_and_session_unchanged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await scope.BootstrapAsync(
            "puntiro-recovery-fail",
            "owner.recovery-fail@example.test",
            cancellationToken);
        scope.Time.SetUtcNow(scope.Time.GetUtcNow().AddSeconds(30));
        var authentication = scope.Services.GetRequiredService<IAdminAuthenticationService>();
        var sessions = scope.Services.GetRequiredService<IAdminSessionService>();
        var verified = await authentication.VerifyAsync(
            new AdminCredentials(
                "owner.recovery-fail@example.test",
                IdentityTestScope.Password,
                GenerateTotp(fixture.TotpUri, scope.Time),
                null),
            new IdentityAuditContext(null, "cli-recovery-fail-login"),
            cancellationToken) ?? throw new InvalidOperationException("Fixture login failed.");
        using var session = await sessions.CreateAsync(
            verified,
            fixture.OrganizationId,
            new IdentityAuditContext(verified.UserId, "cli-recovery-fail-session"),
            cancellationToken);
        var rawSession = session.RawToken.Reveal();
        await using var before = scope.NewIdentityContext();
        var oldTotp = await before.TotpCredentials.AsNoTracking()
            .SingleAsync(item => item.UserId == fixture.UserId, cancellationToken);
        var oldRecoveryIds = await before.RecoveryCodes.AsNoTracking()
            .Where(item => item.UserId == fixture.UserId)
            .Select(item => item.Id)
            .OrderBy(item => item)
            .ToArrayAsync(cancellationToken);
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            RecoveryCode = fixture.RecoveryCodes[0],
            EnrollmentCaptured = scope.RememberEnrollment,
            TotpFactory = _ => Task.FromResult("000000")
        };

        var exit = await scope.CreateReset(terminal).RunAsync(
            "puntiro-recovery-fail",
            "owner.recovery-fail@example.test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.InvalidCredentials, exit);
        Assert.NotNull(await sessions.ValidateAsync(rawSession, cancellationToken));
        await using var after = scope.NewIdentityContext();
        var currentTotp = await after.TotpCredentials.AsNoTracking()
            .SingleAsync(item => item.UserId == fixture.UserId, cancellationToken);
        var currentRecoveryIds = await after.RecoveryCodes.AsNoTracking()
            .Where(item => item.UserId == fixture.UserId)
            .Select(item => item.Id)
            .OrderBy(item => item)
            .ToArrayAsync(cancellationToken);
        Assert.Equal(oldTotp.ProtectedSecret, currentTotp.ProtectedSecret);
        Assert.Equal(oldRecoveryIds, currentRecoveryIds);
    }

    [Fact]
    public async Task Trusted_lookups_are_normalized_read_only_and_do_not_consume_authorization_state()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var fixture = await scope.BootstrapAsync(
            "puntiro-lookup",
            "owner.lookup@example.test",
            cancellationToken);
        var identity = scope.Services.GetRequiredService<IIdentityProvisioningService>();
        var tenancy = scope.Services.GetRequiredService<ITenancyProvisioningService>();
        await using var before = scope.NewIdentityContext();
        var recoveryRows = await before.RecoveryCodes.AsNoTracking()
            .Where(item => item.UserId == fixture.UserId)
            .Select(item => new { item.Id, item.UsedAtUtc, item.Version })
            .OrderBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var sessionRows = await before.Sessions.AsNoTracking().CountAsync(cancellationToken);
        var eventRows = await before.SecurityEvents.AsNoTracking().CountAsync(cancellationToken);

        var userId = await identity.FindUserIdForTrustedProvisioningAsync(
            " OWNER.LOOKUP@example.test ", cancellationToken);
        var organizationId = await tenancy.FindOrganizationIdForTrustedProvisioningAsync(
            "PUNTIRO LOOKUP", cancellationToken);

        Assert.Equal(fixture.UserId, userId);
        Assert.Equal(fixture.OrganizationId, organizationId);
        await using var after = scope.NewIdentityContext();
        Assert.Equal(recoveryRows, await after.RecoveryCodes.AsNoTracking()
            .Where(item => item.UserId == fixture.UserId)
            .Select(item => new { item.Id, item.UsedAtUtc, item.Version })
            .OrderBy(item => item.Id)
            .ToArrayAsync(cancellationToken));
        Assert.Equal(sessionRows, await after.Sessions.AsNoTracking().CountAsync(cancellationToken));
        Assert.Equal(eventRows, await after.SecurityEvents.AsNoTracking().CountAsync(cancellationToken));
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
}
