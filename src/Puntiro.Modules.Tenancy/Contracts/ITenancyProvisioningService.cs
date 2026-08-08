using Puntiro.Modules.Tenancy.Domain;

namespace Puntiro.Modules.Tenancy.Contracts;

public interface ITenancyProvisioningService
{
    Task<OrganizationSnapshot> GetOrCreateProvisioningAsync(
        string displayName,
        string slug,
        CancellationToken cancellationToken);

    Task<MembershipSnapshot> EnsureOwnerMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task ActivateAsync(Guid organizationId, CancellationToken cancellationToken);
}

public sealed record OrganizationSnapshot(
    Guid Id,
    string DisplayName,
    string Slug,
    OrganizationStatus Status,
    long Version);

public sealed record MembershipSnapshot(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    MembershipRole Role,
    MembershipStatus Status,
    long Version);
