using Microsoft.EntityFrameworkCore;
using Npgsql;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Domain;
using Puntiro.Modules.Tenancy.Persistence;

namespace Puntiro.Modules.Tenancy.Services;

internal sealed class TenancyProvisioningService(
    TenancyDbContext context,
    TimeProvider timeProvider) : ITenancyProvisioningService
{
    public async Task<OrganizationSnapshot> GetOrCreateProvisioningAsync(
        string displayName,
        string slug,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = OrganizationSlug.Normalize(slug).Value;
        var existing = await context.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);
        if (existing is not null)
        {
            return Snapshot(existing);
        }

        var now = timeProvider.GetUtcNow();
        var organization = Organization.StartProvisioning(
            Guid.CreateVersion7(),
            displayName,
            normalizedSlug);
        organization.SetCreatedAt(now);
        context.Organizations.Add(organization);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Snapshot(organization);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.ChangeTracker.Clear();
            var concurrent = await context.Organizations
                .AsNoTracking()
                .SingleAsync(item => item.Slug == normalizedSlug, cancellationToken);
            return Snapshot(concurrent);
        }
    }

    public async Task<MembershipSnapshot> EnsureOwnerMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        var organizationExists = await context.Organizations
            .AsNoTracking()
            .AnyAsync(item => item.Id == organizationId, cancellationToken);
        if (!organizationExists)
        {
            throw new KeyNotFoundException("Organization was not found.");
        }

        var existing = await context.Memberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.UserId == userId,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.Status != MembershipStatus.Active || existing.Role != MembershipRole.Owner)
            {
                throw new InvalidOperationException("The existing owner membership is not active.");
            }

            return Snapshot(existing);
        }

        var membership = Membership.CreateOwner(
            Guid.CreateVersion7(),
            organizationId,
            userId,
            timeProvider.GetUtcNow());
        context.Memberships.Add(membership);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Snapshot(membership);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.ChangeTracker.Clear();
            var concurrent = await context.Memberships
                .AsNoTracking()
                .SingleAsync(
                    item => item.OrganizationId == organizationId && item.UserId == userId,
                    cancellationToken);
            if (concurrent.Status != MembershipStatus.Active || concurrent.Role != MembershipRole.Owner)
            {
                throw new InvalidOperationException("The existing owner membership is not active.");
            }

            return Snapshot(concurrent);
        }
    }

    public async Task ActivateAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var organization = await context.Organizations
            .SingleOrDefaultAsync(item => item.Id == organizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Organization was not found.");

        if (organization.Status == OrganizationStatus.Active)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var hasActiveOwner = await context.Memberships.AnyAsync(
            item => item.OrganizationId == organizationId
                && item.Role == MembershipRole.Owner
                && item.Status == MembershipStatus.Active,
            cancellationToken);

        organization.Activate(hasActiveOwner);
        var now = timeProvider.GetUtcNow();
        organization.MarkUpdated(now);
        context.SecurityEvents.Add(TenancySecurityEvent.OrganizationActivated(
            Guid.CreateVersion7(),
            organizationId,
            now));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }

    private static OrganizationSnapshot Snapshot(Organization organization)
    {
        return new OrganizationSnapshot(
            organization.Id,
            organization.DisplayName,
            organization.Slug,
            organization.Status,
            organization.Version);
    }

    private static MembershipSnapshot Snapshot(Membership membership)
    {
        return new MembershipSnapshot(
            membership.Id,
            membership.OrganizationId,
            membership.UserId,
            membership.Role,
            membership.Status,
            membership.Version);
    }
}
