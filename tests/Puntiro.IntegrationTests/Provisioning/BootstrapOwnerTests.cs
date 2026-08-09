using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Puntiro.IntegrationTests.Identity;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Identity;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Domain;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Security;
using Puntiro.Modules.Tenancy;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Domain;
using Puntiro.Modules.Tenancy.Persistence;
using Puntiro.Provisioning.Bootstrap;
using Puntiro.Provisioning.Cli;
using Puntiro.Provisioning.Recovery;
using Puntiro.Security;
using Xunit;

namespace Puntiro.IntegrationTests.Provisioning;

[Collection(PostgresCollection.Name)]
public sealed class BootstrapOwnerTests(PostgresDatabase database)
{
    [Fact]
    public async Task Bootstrap_activates_only_after_first_totp_and_owner_membership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        ProvisioningState? beforeConfirmation = null;
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            EnrollmentCaptured = scope.RememberEnrollment,
            TotpFactory = async token =>
            {
                beforeConfirmation = await scope.ReadStateAsync("puntiro-bootstrap", token);
                return scope.GenerateCurrentTotp();
            }
        };
        var orchestrator = scope.CreateBootstrap(terminal);

        var exit = await orchestrator.RunAsync(
            "Puntiro Warehouse",
            "puntiro-bootstrap",
            "Owner.Bootstrap@Example.Test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.Success, exit);
        Assert.NotNull(beforeConfirmation);
        Assert.Equal(OrganizationStatus.Provisioning, beforeConfirmation.OrganizationStatus);
        Assert.Equal(AdminUserStatus.Provisioning, beforeConfirmation.UserStatus);
        Assert.Equal(0, beforeConfirmation.OwnerMemberships);
        var completed = await scope.ReadStateAsync("puntiro-bootstrap", cancellationToken);
        Assert.Equal(OrganizationStatus.Active, completed.OrganizationStatus);
        Assert.Equal(AdminUserStatus.Active, completed.UserStatus);
        Assert.Equal(1, completed.OwnerMemberships);
        Assert.Null(completed.ProvisioningOrganizationId);
    }

    [Fact]
    public async Task Interrupted_bootstrap_replaces_pending_secrets_without_duplicate_records()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        using var interruptedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var interrupted = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            EnrollmentCaptured = scope.RememberEnrollment,
            TotpFactory = _ =>
            {
                interruptedCancellation.Cancel();
                return Task.FromCanceled<string>(interruptedCancellation.Token);
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.CreateBootstrap(interrupted).RunAsync(
            "Puntiro Warehouse",
            "puntiro-resume",
            "owner.resume@example.test",
            interruptedCancellation.Token));

        var resumed = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            EnrollmentCaptured = scope.RememberEnrollment,
            TotpFactory = _ => Task.FromResult(scope.GenerateCurrentTotp())
        };
        var exit = await scope.CreateBootstrap(resumed).RunAsync(
            "Puntiro Warehouse",
            "PUNTIRO RESUME",
            " OWNER.RESUME@example.test ",
            cancellationToken);

        Assert.Equal(ProvisioningExit.Success, exit);
        Assert.False(
            string.Equals(interrupted.TotpUri, resumed.TotpUri, StringComparison.Ordinal),
            "The pending TOTP enrollment was not rotated.");
        Assert.False(
            string.Equals(interrupted.RecoveryCodes[0], resumed.RecoveryCodes[0], StringComparison.Ordinal),
            "The pending recovery batch was not rotated.");
        await using var identity = scope.Services.GetRequiredService<IdentityDbContext>();
        await using var tenancy = scope.Services.GetRequiredService<TenancyDbContext>();
        Assert.Equal(1, await tenancy.Organizations.CountAsync(item => item.Slug == "puntiro-resume", cancellationToken));
        var resumedUserId = await identity.AdminUsers
            .Where(item => item.NormalizedEmail == "owner.resume@example.test")
            .Select(item => item.Id)
            .SingleAsync(cancellationToken);
        var resumedOrganizationId = await tenancy.Organizations
            .Where(item => item.Slug == "puntiro-resume")
            .Select(item => item.Id)
            .SingleAsync(cancellationToken);
        Assert.Equal(1, await identity.AdminUsers.CountAsync(
            item => item.NormalizedEmail == "owner.resume@example.test", cancellationToken));
        Assert.Equal(1, await tenancy.Memberships.CountAsync(
            item => item.OrganizationId == resumedOrganizationId && item.UserId == resumedUserId,
            cancellationToken));
        Assert.Equal(1, await identity.RecoveryCodes
            .Where(item => item.UserId == resumedUserId)
            .Select(item => item.BatchId)
            .Distinct()
            .CountAsync(cancellationToken));
    }

    [Fact]
    public async Task Active_organization_retry_only_completes_safe_identity_correlation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        var tenancy = scope.Services.GetRequiredService<ITenancyProvisioningService>();
        var identity = scope.Services.GetRequiredService<IIdentityProvisioningService>();
        var organization = await tenancy.GetOrCreateProvisioningAsync(
            "Puntiro Warehouse", "puntiro-cleanup", cancellationToken);
        using var pending = await identity.BeginOwnerAsync(
            organization.Id,
            "owner.cleanup@example.test",
            IdentityTestScope.Password,
            new IdentityAuditContext(null, "cli-bootstrap-start"),
            cancellationToken);
        scope.RememberEnrollment(pending.TotpUri.Reveal(), pending.RecoveryCodes.Select(item => item.Reveal()).ToArray());
        await identity.ConfirmOwnerTotpAsync(
            pending.UserId,
            scope.GenerateCurrentTotp(),
            new IdentityAuditContext(pending.UserId, "cli-bootstrap-confirm"),
            cancellationToken);
        await tenancy.EnsureOwnerMembershipAsync(organization.Id, pending.UserId, cancellationToken);
        await tenancy.ActivateAsync(
            organization.Id,
            new TenancyAuditContext(pending.UserId, "cli-bootstrap-activate"),
            cancellationToken);

        await using var beforeContext = scope.NewIdentityContext();
        var beforeTotp = await beforeContext.TotpCredentials.AsNoTracking()
            .SingleAsync(item => item.UserId == pending.UserId, cancellationToken);
        var beforeRecovery = await beforeContext.RecoveryCodes.AsNoTracking()
            .Where(item => item.UserId == pending.UserId)
            .Select(item => item.Id)
            .OrderBy(item => item)
            .ToArrayAsync(cancellationToken);
        var terminal = new CapturingProvisioningTerminal("must-not-be-read");

        var exit = await scope.CreateBootstrap(terminal).RunAsync(
            "Puntiro Warehouse",
            "puntiro-cleanup",
            "OWNER.CLEANUP@example.test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.Success, exit);
        Assert.Equal(0, terminal.PasswordReads);
        Assert.Null(terminal.TotpUri);
        await using var afterContext = scope.NewIdentityContext();
        var afterTotp = await afterContext.TotpCredentials.AsNoTracking()
            .SingleAsync(item => item.UserId == pending.UserId, cancellationToken);
        var afterRecovery = await afterContext.RecoveryCodes.AsNoTracking()
            .Where(item => item.UserId == pending.UserId)
            .Select(item => item.Id)
            .OrderBy(item => item)
            .ToArrayAsync(cancellationToken);
        Assert.Equal(beforeTotp.ProtectedSecret, afterTotp.ProtectedSecret);
        Assert.Equal(beforeRecovery, afterRecovery);
        Assert.Equal(AdminUserStatus.Active, (await scope.ReadStateAsync("puntiro-cleanup", cancellationToken)).UserStatus);
    }

    [Fact]
    public async Task Provisioning_organization_rejects_a_different_owner_email_without_creating_an_account()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await ProvisioningTestScope.CreateAsync(database.ConnectionString);
        using var interruptedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var first = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            EnrollmentCaptured = scope.RememberEnrollment,
            TotpFactory = _ =>
            {
                interruptedCancellation.Cancel();
                return Task.FromCanceled<string>(interruptedCancellation.Token);
            }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.CreateBootstrap(first).RunAsync(
            "Puntiro Warehouse",
            "puntiro-email-conflict",
            "first.owner@example.test",
            interruptedCancellation.Token));
        var second = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            EnrollmentCaptured = scope.RememberEnrollment,
            TotpFactory = _ => Task.FromResult(scope.GenerateCurrentTotp())
        };

        var exit = await scope.CreateBootstrap(second).RunAsync(
            "Puntiro Warehouse",
            "puntiro-email-conflict",
            "different.owner@example.test",
            cancellationToken);

        Assert.Equal(ProvisioningExit.Conflict, exit);
        await using var identity = scope.NewIdentityContext();
        Assert.Equal(1, await identity.AdminUsers.CountAsync(
            item => item.NormalizedEmail == "first.owner@example.test" ||
                item.NormalizedEmail == "different.owner@example.test",
            cancellationToken));
    }
}

internal sealed class ProvisioningTestScope : IAsyncDisposable
{
    private ProvisioningTestScope(ServiceProvider root, IServiceScope serviceScope, ManualTimeProvider time)
    {
        _root = root;
        _serviceScope = serviceScope;
        Time = time;
    }

    private readonly ServiceProvider _root;
    private readonly IServiceScope _serviceScope;
    private string? _lastTotpUri;

    internal IServiceProvider Services => _serviceScope.ServiceProvider;
    internal ManualTimeProvider Time { get; }

    internal static async Task<ProvisioningTestScope> CreateAsync(
        string connectionString,
        IdentityKeyOptions? keyOptions = null,
        IDataProtectionProvider? dataProtectionProvider = null,
        bool dataProtectionKeyRingReady = true,
        bool migrate = true,
        DateTimeOffset? now = null)
    {
        var time = new ManualTimeProvider(
            now ?? new DateTimeOffset(2026, 8, 8, 10, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddIdentityModule(connectionString, keyOptions ?? IdentityTestScope.CreateKeys(), time);
        services.AddTenancyModule(connectionString, time);
        services.AddSingleton(
            dataProtectionProvider ?? IdentityTestScope.DataProtectionProvider);
        services.AddSingleton<IDataProtectionKeyRingReadiness>(
            new ConfiguredTestDataProtectionKeyRing(dataProtectionKeyRingReady));
        var root = services.BuildServiceProvider(validateScopes: true);
        var serviceScope = root.CreateScope();
        var scope = new ProvisioningTestScope(root, serviceScope, time);
        if (migrate)
        {
            await scope.Services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(
                TestContext.Current.CancellationToken);
            await scope.Services.GetRequiredService<TenancyDbContext>().Database.MigrateAsync(
                TestContext.Current.CancellationToken);
        }

        return scope;
    }

    internal BootstrapOwnerOrchestrator CreateBootstrap(IProvisioningTerminal terminal) => new(
        Services.GetRequiredService<ITenancyProvisioningService>(),
        Services.GetRequiredService<ITenantAccessService>(),
        Services.GetRequiredService<IIdentityProvisioningService>(),
        Services.GetRequiredService<IIdentityProvisioningReadinessService>(),
        terminal);

    internal ResetOwnerTotpOrchestrator CreateReset(IProvisioningTerminal terminal) => new(
        Services.GetRequiredService<ITenancyProvisioningService>(),
        Services.GetRequiredService<ITenantAccessService>(),
        Services.GetRequiredService<IIdentityProvisioningService>(),
        Services.GetRequiredService<IIdentityProvisioningReadinessService>(),
        terminal);

    internal void RememberEnrollment(string totpUri, IReadOnlyList<string> recoveryCodes)
    {
        _lastTotpUri = totpUri;
    }

    internal async Task<BootstrapFixture> BootstrapAsync(
        string slug,
        string email,
        CancellationToken cancellationToken)
    {
        var terminal = new CapturingProvisioningTerminal(IdentityTestScope.Password)
        {
            EnrollmentCaptured = RememberEnrollment,
            TotpFactory = _ => Task.FromResult(GenerateCurrentTotp())
        };
        var exit = await CreateBootstrap(terminal).RunAsync(
            "Puntiro Warehouse",
            slug,
            email,
            cancellationToken);
        Assert.Equal(ProvisioningExit.Success, exit);
        await using var tenancy = NewTenancyContext();
        var organizationId = await tenancy.Organizations
            .Where(item => item.Slug == slug)
            .Select(item => item.Id)
            .SingleAsync(cancellationToken);
        var userId = await tenancy.Memberships
            .Where(item => item.OrganizationId == organizationId)
            .Select(item => item.UserId)
            .SingleAsync(cancellationToken);
        return new BootstrapFixture(
            userId,
            organizationId,
            terminal.TotpUri!,
            terminal.RecoveryCodes);
    }

    internal string GenerateCurrentTotp()
    {
        using var uri = new SensitiveValue(_lastTotpUri ?? throw new InvalidOperationException("Enrollment was not captured."));
        var secret = IdentityTestScope.ReadTotpSecret(uri);
        try
        {
            return Rfc6238Totp.Generate(secret, Time.GetUtcNow().ToUnixTimeSeconds());
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(secret);
        }
    }

    internal async Task<ProvisioningState> ReadStateAsync(string slug, CancellationToken cancellationToken)
    {
        await using var identity = NewIdentityContext();
        await using var tenancy = NewTenancyContext();
        var organization = await tenancy.Organizations.AsNoTracking()
            .SingleAsync(item => item.Slug == slug, cancellationToken);
        var membershipUserId = await tenancy.Memberships.AsNoTracking()
            .Where(item => item.OrganizationId == organization.Id)
            .Select(item => (Guid?)item.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        var user = membershipUserId is null
            ? await identity.AdminUsers.AsNoTracking().SingleAsync(
                item => item.ProvisioningOrganizationId == organization.Id,
                cancellationToken)
            : await identity.AdminUsers.AsNoTracking().SingleAsync(
                item => item.Id == membershipUserId.Value,
                cancellationToken);
        var memberships = await tenancy.Memberships.AsNoTracking().CountAsync(
            item => item.OrganizationId == organization.Id &&
                item.UserId == user.Id &&
                item.Role == MembershipRole.Owner &&
                item.Status == MembershipStatus.Active,
            cancellationToken);
        return new ProvisioningState(
            organization.Status,
            user.Status,
            memberships,
            user.ProvisioningOrganizationId);
    }

    internal IdentityDbContext NewIdentityContext()
    {
        var configured = Services.GetRequiredService<IdentityDbContext>();
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(configured.Database.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options);
    }

    internal TenancyDbContext NewTenancyContext()
    {
        var configured = Services.GetRequiredService<TenancyDbContext>();
        return new TenancyDbContext(new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(configured.Database.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy"))
            .Options);
    }

    public async ValueTask DisposeAsync()
    {
        _serviceScope.Dispose();
        await _root.DisposeAsync();
    }
}

internal sealed class ConfiguredTestDataProtectionKeyRing(
    bool ready) : IDataProtectionKeyRingReadiness
{
    public bool HasUsableCurrentKey() => ready;
}

internal sealed record ProvisioningState(
    OrganizationStatus OrganizationStatus,
    AdminUserStatus UserStatus,
    int OwnerMemberships,
    Guid? ProvisioningOrganizationId);

internal sealed record BootstrapFixture(
    Guid UserId,
    Guid OrganizationId,
    string TotpUri,
    IReadOnlyList<string> RecoveryCodes);

internal sealed class CapturingProvisioningTerminal(string password) : IProvisioningTerminal
{
    internal Func<CancellationToken, Task<string>>? TotpFactory { get; init; }
    internal string? RecoveryCode { get; init; }
    internal string? TotpUri { get; private set; }
    internal IReadOnlyList<string> RecoveryCodes { get; private set; } = [];
    internal int PasswordReads { get; private set; }
    internal Action<string, IReadOnlyList<string>>? EnrollmentCaptured { get; init; }

    public Task<SensitiveValue> ReadPasswordAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PasswordReads++;
        return Task.FromResult(new SensitiveValue(password));
    }

    public Task<SensitiveValue> ReadRecoveryCodeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SensitiveValue(
            RecoveryCode ?? throw new InvalidOperationException("No recovery fixture was supplied.")));
    }

    public async Task<SensitiveValue> ReadTotpAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await (TotpFactory ?? throw new InvalidOperationException("No TOTP fixture was supplied."))(
            cancellationToken);
        return new SensitiveValue(value);
    }

    public Task WriteEnrollmentAsync(
        SensitiveValue totpUri,
        IReadOnlyList<SensitiveValue> recoveryCodes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TotpUri = totpUri.Reveal();
        RecoveryCodes = recoveryCodes.Select(item => item.Reveal()).ToArray();
        EnrollmentCaptured?.Invoke(TotpUri, RecoveryCodes);
        return Task.CompletedTask;
    }
}
