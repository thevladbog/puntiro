using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Domain;
using Puntiro.Modules.Tenancy.Persistence;

namespace Puntiro.Modules.Tenancy.Services;

internal sealed class TenantAccessService(TenancyDbContext context) : ITenantAccessService
{
    public async Task<TenantAccess?> FindSingleActiveMembershipAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        EnsureNotEmpty(userId, nameof(userId));

        var memberships = await (
                from membership in context.Memberships.AsNoTracking()
                join organization in context.Organizations.AsNoTracking()
                    on membership.OrganizationId equals organization.Id
                where membership.UserId == userId
                    && membership.Status == MembershipStatus.Active
                    && organization.Status == OrganizationStatus.Active
                select new TenantAccess(
                    membership.OrganizationId,
                    membership.UserId,
                    membership.Role))
            .Take(2)
            .ToListAsync(cancellationToken);

        return memberships.Count switch
        {
            0 => null,
            1 => memberships[0],
            _ => throw new OrganizationSelectionRequiredException()
        };
    }

    public async Task<bool> IsActiveOwnerAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        EnsureNotEmpty(organizationId, nameof(organizationId));
        EnsureNotEmpty(userId, nameof(userId));

        return await (
                from membership in context.Memberships.AsNoTracking()
                join organization in context.Organizations.AsNoTracking()
                    on membership.OrganizationId equals organization.Id
                where membership.OrganizationId == organizationId
                    && membership.UserId == userId
                    && membership.Role == MembershipRole.Owner
                    && membership.Status == MembershipStatus.Active
                    && organization.Status == OrganizationStatus.Active
                select membership.Id)
            .AnyAsync(cancellationToken);
    }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }
}
