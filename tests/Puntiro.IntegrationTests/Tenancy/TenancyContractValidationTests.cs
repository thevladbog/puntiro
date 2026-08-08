using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Services;
using Xunit;

namespace Puntiro.IntegrationTests.Tenancy;

[Collection(PostgresCollection.Name)]
public sealed class TenancyContractValidationTests(PostgresDatabase database)
{
    private static readonly Guid UserId = Guid.Parse("01989f73-0f9a-7aa2-9acf-8781f8a00002");
    private static readonly TenancyAuditContext AuditContext = new(UserId, "trace-validation-001");

    [Fact]
    public async Task Provisioning_validates_display_name_before_an_idempotent_return()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var slug = $"validation-{Guid.NewGuid():N}";
        await scope.Service.GetOrCreateProvisioningAsync("Valid", slug, cancellationToken);

        await Assert.ThrowsAsync<ArgumentException>(
            () => scope.Service.GetOrCreateProvisioningAsync(" ", slug, cancellationToken));
    }

    [Fact]
    public async Task Activation_rejects_an_empty_organization_id_before_database_lookup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);

        await Assert.ThrowsAsync<ArgumentException>(
            () => scope.Service.ActivateAsync(Guid.Empty, AuditContext, cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => scope.Service.ActivateAsync(Guid.CreateVersion7(), null!, cancellationToken));
    }

    [Fact]
    public async Task Owner_membership_rejects_empty_identifiers_before_database_lookup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);

        await Assert.ThrowsAsync<ArgumentException>(
            () => scope.Service.EnsureOwnerMembershipAsync(Guid.Empty, UserId, cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(
            () => scope.Service.EnsureOwnerMembershipAsync(
                Guid.CreateVersion7(),
                Guid.Empty,
                cancellationToken));
    }

    [Fact]
    public async Task Find_membership_rejects_an_empty_user_id()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var access = new TenantAccessService(scope.Context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => access.FindSingleActiveMembershipAsync(Guid.Empty, cancellationToken));
    }

    [Fact]
    public async Task Owner_check_rejects_empty_organization_and_user_ids()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await TenancyTestScope.CreateAsync(
            database.ConnectionString,
            cancellationToken);
        var access = new TenantAccessService(scope.Context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => access.IsActiveOwnerAsync(Guid.Empty, UserId, cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(
            () => access.IsActiveOwnerAsync(Guid.CreateVersion7(), Guid.Empty, cancellationToken));
    }
}
