using Puntiro.Modules.Tenancy.Domain;

namespace Puntiro.Modules.Tenancy.Contracts;

public interface ITenantAccessService
{
    Task<TenantAccess?> FindSingleActiveMembershipAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsActiveOwnerAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsOrganizationActiveAsync(
        Guid organizationId,
        CancellationToken cancellationToken);
}

public sealed record TenantAccess(Guid OrganizationId, Guid UserId, MembershipRole Role);
