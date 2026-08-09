using System.Data;
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
    public async Task<Guid?> FindOrganizationIdForTrustedProvisioningAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var normalized = OrganizationSlug.Normalize(slug).Value;
        return await context.Organizations.AsNoTracking()
            .Where(item => item.Slug == normalized)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OrganizationSnapshot> GetOrCreateProvisioningAsync(
        string displayName,
        string slug,
        CancellationToken cancellationToken)
    {
        var normalizedDisplayName = Organization.NormalizeDisplayName(displayName);
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
            normalizedDisplayName,
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

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockOrganizationAsync(organizationId, cancellationToken);
        _ = await LoadOrganizationAfterLockAsync(organizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Organization was not found.");

        var existing = await context.Memberships
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.UserId == userId,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.Status != MembershipStatus.Active || existing.Role != MembershipRole.Owner)
            {
                throw new InvalidOperationException("The existing owner membership is not active.");
            }

            await transaction.CommitAsync(cancellationToken);
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
            await transaction.CommitAsync(cancellationToken);
            return Snapshot(membership);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException(
                "The owner membership could not be created after serialization.",
                exception);
        }
    }

    public async Task ActivateAsync(
        Guid organizationId,
        TenancyAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        EnsureNotEmpty(organizationId, nameof(organizationId));
        ArgumentNullException.ThrowIfNull(auditContext);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockOrganizationAsync(organizationId, cancellationToken);
        var organization = await LoadOrganizationAfterLockAsync(
                organizationId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Organization was not found.");

        var actorIsActiveOwner = await context.Memberships.AnyAsync(
            item => item.OrganizationId == organizationId
                && item.UserId == auditContext.ActorUserId
                && item.Role == MembershipRole.Owner
                && item.Status == MembershipStatus.Active,
            cancellationToken);
        if (!actorIsActiveOwner)
        {
            throw new InvalidOperationException("The activation actor must be an active owner.");
        }

        if (organization.Status == OrganizationStatus.Active)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        organization.Activate(hasActiveOwner: true);
        var now = timeProvider.GetUtcNow();
        organization.MarkUpdated(now);
        context.SecurityEvents.Add(TenancySecurityEvent.OrganizationActivated(
            Guid.CreateVersion7(),
            organizationId,
            auditContext.ActorUserId,
            auditContext.TraceId,
            now));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<MembershipSnapshot> RevokeOwnerMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        EnsureNotEmpty(organizationId, nameof(organizationId));
        EnsureNotEmpty(userId, nameof(userId));

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockOrganizationAsync(organizationId, cancellationToken);
        var organization = await LoadOrganizationAfterLockAsync(
                organizationId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Organization was not found.");
        var membership = await LoadMembershipAfterLockAsync(
                organizationId,
                userId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Owner membership was not found.");

        if (membership.Status == MembershipStatus.Revoked)
        {
            await transaction.CommitAsync(cancellationToken);
            return Snapshot(membership);
        }

        if (organization.Status == OrganizationStatus.Active)
        {
            var anotherActiveOwnerExists = await context.Memberships.AnyAsync(
                item => item.OrganizationId == organizationId
                    && item.Id != membership.Id
                    && item.Role == MembershipRole.Owner
                    && item.Status == MembershipStatus.Active,
                cancellationToken);
            if (!anotherActiveOwnerExists)
            {
                throw new InvalidOperationException(
                    "The last active owner of an active organization cannot be revoked.");
            }
        }

        membership.Revoke(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Snapshot(membership);
    }

    public async Task<ITrustedActiveOwnerMutationLease?> TryAcquireActiveOwnerMutationLeaseForTrustedProvisioningAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        EnsureNotEmpty(organizationId, nameof(organizationId));
        EnsureNotEmpty(userId, nameof(userId));

        var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var lease = new TrustedActiveOwnerMutationLease(
            new EfTrustedMutationTransaction(transaction));
        try
        {
            await LockOrganizationAsync(organizationId, cancellationToken);
            var organization = await LoadOrganizationAfterLockAsync(organizationId, cancellationToken);
            var membership = await LoadMembershipAfterLockAsync(
                organizationId,
                userId,
                cancellationToken);
            if (organization?.Status != OrganizationStatus.Active ||
                membership is null ||
                membership.Role != MembershipRole.Owner ||
                membership.Status != MembershipStatus.Active)
            {
                await lease.DisposeAsync();
                return null;
            }

            return lease;
        }
        catch (Exception primaryError)
        {
            try
            {
                await lease.DisposeAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "The trusted mutation lease could not be acquired or cleaned up.",
                    primaryError,
                    cleanupError);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(primaryError)
                .Throw();
            throw;
        }
    }

    public async Task SuspendAsync(
        Guid organizationId,
        TenancyAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        EnsureNotEmpty(organizationId, nameof(organizationId));
        ArgumentNullException.ThrowIfNull(auditContext);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockOrganizationAsync(organizationId, cancellationToken);
        var organization = await LoadOrganizationAfterLockAsync(organizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Organization was not found.");
        var actorIsActiveOwner = await context.Memberships.AnyAsync(
            item => item.OrganizationId == organizationId
                && item.UserId == auditContext.ActorUserId
                && item.Role == MembershipRole.Owner
                && item.Status == MembershipStatus.Active,
            cancellationToken);
        if (!actorIsActiveOwner)
        {
            throw new InvalidOperationException("The suspension actor must be an active owner.");
        }

        if (organization.Status == OrganizationStatus.Suspended)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        organization.Suspend();
        var now = timeProvider.GetUtcNow();
        organization.MarkUpdated(now);
        context.SecurityEvents.Add(TenancySecurityEvent.OrganizationSuspended(
            Guid.CreateVersion7(),
            organizationId,
            auditContext.ActorUserId,
            auditContext.TraceId,
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

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }

    private Task<int> LockOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM tenancy.organizations WHERE id = {organizationId} FOR UPDATE",
            cancellationToken);
    }

    private async Task<Organization?> LoadOrganizationAfterLockAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var tracked = context.ChangeTracker.Entries<Organization>()
            .SingleOrDefault(entry => entry.Entity.Id == organizationId);
        if (tracked is null)
        {
            return await context.Organizations.SingleOrDefaultAsync(
                item => item.Id == organizationId,
                cancellationToken);
        }

        await tracked.ReloadAsync(cancellationToken);
        return tracked.State == EntityState.Detached ? null : tracked.Entity;
    }

    private async Task<Membership?> LoadMembershipAfterLockAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tracked = context.ChangeTracker.Entries<Membership>()
            .SingleOrDefault(entry => entry.Entity.OrganizationId == organizationId
                && entry.Entity.UserId == userId);
        if (tracked is null)
        {
            return await context.Memberships.SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.UserId == userId,
                cancellationToken);
        }

        await tracked.ReloadAsync(cancellationToken);
        return tracked.State == EntityState.Detached ? null : tracked.Entity;
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
