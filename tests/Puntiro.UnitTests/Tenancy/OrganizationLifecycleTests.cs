using Puntiro.Modules.Tenancy.Domain;
using Xunit;

namespace Puntiro.UnitTests.Tenancy;

public sealed class OrganizationLifecycleTests
{
    [Fact]
    public void Active_organization_requires_active_owner()
    {
        var organization = Organization.StartProvisioning(
            Guid.CreateVersion7(),
            "Puntiro",
            "puntiro");

        Assert.Throws<InvalidOperationException>(() => organization.Activate(hasActiveOwner: false));
        Assert.Equal(OrganizationStatus.Provisioning, organization.Status);
    }

    [Fact]
    public void Provisioning_organization_with_active_owner_can_be_activated()
    {
        var organization = Organization.StartProvisioning(
            Guid.CreateVersion7(),
            "Puntiro",
            "puntiro");

        organization.Activate(hasActiveOwner: true);

        Assert.Equal(OrganizationStatus.Active, organization.Status);
    }

    [Fact]
    public void Suspending_an_organization_changes_only_its_lifecycle_state()
    {
        var organization = Organization.StartProvisioning(
            Guid.CreateVersion7(),
            "Puntiro",
            "puntiro");
        organization.Activate(hasActiveOwner: true);

        organization.Suspend();

        Assert.Equal(OrganizationStatus.Suspended, organization.Status);
    }
}
