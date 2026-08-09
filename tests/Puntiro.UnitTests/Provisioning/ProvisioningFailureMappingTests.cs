using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Domain;
using Puntiro.Provisioning.Bootstrap;
using Puntiro.Provisioning.Cli;
using Puntiro.Provisioning.Recovery;
using Puntiro.Security;
using Xunit;

namespace Puntiro.UnitTests.Provisioning;

public sealed class ProvisioningFailureMappingTests
{
    [Fact]
    public async Task Reset_commit_followed_by_lease_disposal_failure_is_ambiguous_infrastructure()
    {
        var lease = new FaultingOwnerMutationLease();
        var tenancy = new StubTenancyProvisioningService { Lease = lease };
        var identity = new StubIdentityProvisioningService();
        var terminal = new StubProvisioningTerminal();
        var orchestrator = new ResetOwnerTotpOrchestrator(
            tenancy,
            new StubTenantAccessService(),
            identity,
            new StubReadinessService(),
            terminal);

        var exit = await orchestrator.RunAsync(
            "example",
            "owner@example.test",
            TestContext.Current.CancellationToken);

        Assert.Equal(ProvisioningExit.InfrastructureFailure, exit);
        Assert.Equal(1, identity.CompleteResetCalls);
        Assert.Equal(1, lease.DisposeCalls);
        using var output = new StringWriter();
        ProvisioningOutcome.Write(output, exit);
        Assert.Equal(
            "Provisioning failed because infrastructure is unavailable.\n",
            output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.DoesNotContain("unchanged", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bootstrap_throwing_readiness_is_infrastructure_before_prompt_or_mutation()
    {
        var tenancy = new StubTenancyProvisioningService();
        var terminal = new StubProvisioningTerminal();
        var orchestrator = new BootstrapOwnerOrchestrator(
            tenancy,
            new StubTenantAccessService(),
            new StubIdentityProvisioningService(),
            new StubReadinessService(new InvalidOperationException("injected")),
            terminal);

        var exit = await orchestrator.RunAsync(
            "Example",
            "example",
            "owner@example.test",
            TestContext.Current.CancellationToken);

        Assert.Equal(ProvisioningExit.InfrastructureFailure, exit);
        Assert.Equal(0, terminal.PasswordReads);
        Assert.Equal(0, tenancy.GetOrCreateCalls);
    }

    [Fact]
    public async Task Bootstrap_cancelled_readiness_is_infrastructure_before_prompt_or_mutation()
    {
        var tenancy = new StubTenancyProvisioningService();
        var terminal = new StubProvisioningTerminal();
        var orchestrator = new BootstrapOwnerOrchestrator(
            tenancy,
            new StubTenantAccessService(),
            new StubIdentityProvisioningService(),
            new StubReadinessService(new OperationCanceledException("injected")),
            terminal);

        var exit = await orchestrator.RunAsync(
            "Example",
            "example",
            "owner@example.test",
            TestContext.Current.CancellationToken);

        Assert.Equal(ProvisioningExit.InfrastructureFailure, exit);
        Assert.Equal(0, terminal.PasswordReads);
        Assert.Equal(0, tenancy.GetOrCreateCalls);
    }
}

internal sealed class StubReadinessService(
    Exception? exception = null) : IIdentityProvisioningReadinessService
{
    public Task<bool> IsReadyForTrustedProvisioningAsync(CancellationToken cancellationToken) =>
        exception is null
            ? Task.FromResult(true)
            : Task.FromException<bool>(exception);
}

internal sealed class StubTenantAccessService : ITenantAccessService
{
    public Task<TenantAccess?> FindSingleActiveMembershipAsync(
        Guid userId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> IsActiveOwnerAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<bool> IsOrganizationActiveAsync(
        Guid organizationId,
        CancellationToken cancellationToken) => Task.FromResult(true);
}

internal sealed class StubTenancyProvisioningService : ITenancyProvisioningService
{
    internal ITrustedActiveOwnerMutationLease? Lease { get; init; }
    internal int GetOrCreateCalls { get; private set; }

    public Task<Guid?> FindOrganizationIdForTrustedProvisioningAsync(
        string slug,
        CancellationToken cancellationToken) => Task.FromResult<Guid?>(Guid.Parse("0198a940-98f1-7000-8000-000000000001"));

    public Task<OrganizationSnapshot> GetOrCreateProvisioningAsync(
        string displayName,
        string slug,
        CancellationToken cancellationToken)
    {
        GetOrCreateCalls++;
        throw new InvalidOperationException("Must not be called when readiness fails.");
    }

    public Task<MembershipSnapshot> EnsureOwnerMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<MembershipSnapshot> RevokeOwnerMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ITrustedActiveOwnerMutationLease?> TryAcquireActiveOwnerMutationLeaseForTrustedProvisioningAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) => Task.FromResult(Lease);

    public Task ActivateAsync(
        Guid organizationId,
        TenancyAuditContext auditContext,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SuspendAsync(
        Guid organizationId,
        TenancyAuditContext auditContext,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class StubIdentityProvisioningService : IIdentityProvisioningService
{
    private static readonly Guid UserId = Guid.Parse("0198a940-98f1-7000-8000-000000000002");
    internal int CompleteResetCalls { get; private set; }

    public Task<Guid?> FindUserIdForTrustedProvisioningAsync(
        string email,
        CancellationToken cancellationToken) => Task.FromResult<Guid?>(UserId);

    public Task<PendingOwnerIdentity> BeginOwnerAsync(
        Guid provisioningOrganizationId,
        string email,
        string password,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ConfirmOwnerTotpAsync(
        Guid userId,
        string code,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task CompleteOwnerAsync(
        Guid userId,
        Guid organizationId,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<PendingOwnerTotpReset> PrepareOwnerTotpResetAsync(
        Guid userId,
        string password,
        string recoveryCode,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken) => Task.FromResult(new PendingOwnerTotpReset
        {
            UserId = UserId,
            TotpUri = new SensitiveValue("otpauth://totp/test?secret=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
            RecoveryCodes = [new SensitiveValue("AAAA-BBBB-CCCC-DDDD")]
        });

    public Task CompleteOwnerTotpResetAsync(
        PendingOwnerTotpReset pending,
        string firstTotpCode,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        CompleteResetCalls++;
        return Task.CompletedTask;
    }

    public Task SuspendAsync(
        Guid userId,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class StubProvisioningTerminal : IProvisioningTerminal
{
    internal int PasswordReads { get; private set; }

    public Task<SensitiveValue> ReadPasswordAsync(CancellationToken cancellationToken)
    {
        PasswordReads++;
        return Task.FromResult(new SensitiveValue("password-fixture"));
    }

    public Task<SensitiveValue> ReadRecoveryCodeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SensitiveValue("AAAA-BBBB-CCCC-DDDD"));

    public Task<SensitiveValue> ReadTotpAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SensitiveValue("123456"));

    public Task WriteEnrollmentAsync(
        SensitiveValue totpUri,
        IReadOnlyList<SensitiveValue> recoveryCodes,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FaultingOwnerMutationLease : ITrustedActiveOwnerMutationLease
{
    internal int DisposeCalls { get; private set; }

    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        throw new InvalidOperationException("injected lease cleanup failure");
    }
}
