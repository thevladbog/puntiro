using Puntiro.Modules.Tenancy.Domain;

namespace Puntiro.Modules.Tenancy.Contracts;

public interface ITenancyProvisioningService
{
    Task<Guid?> FindOrganizationIdForTrustedProvisioningAsync(
        string slug,
        CancellationToken cancellationToken);

    Task<OrganizationSnapshot> GetOrCreateProvisioningAsync(
        string displayName,
        string slug,
        CancellationToken cancellationToken);

    Task<MembershipSnapshot> EnsureOwnerMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<MembershipSnapshot> RevokeOwnerMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<ITrustedActiveOwnerMutationLease?> TryAcquireActiveOwnerMutationLeaseForTrustedProvisioningAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task ActivateAsync(
        Guid organizationId,
        TenancyAuditContext auditContext,
        CancellationToken cancellationToken);

    Task SuspendAsync(
        Guid organizationId,
        TenancyAuditContext auditContext,
        CancellationToken cancellationToken);
}

/// <summary>
/// Holds the serialized Tenancy authorization boundary for a trusted, non-HTTP
/// provisioning or recovery composition. The lease carries no authorization
/// data and must remain held across the corresponding Identity mutation.
/// </summary>
public interface ITrustedActiveOwnerMutationLease : IAsyncDisposable;

public sealed record TenancyAuditContext
{
    public const int MaximumTraceIdLength = 128;

    public TenancyAuditContext(Guid actorUserId, string traceId)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("Actor user ID cannot be empty.", nameof(actorUserId));
        }

        ArgumentNullException.ThrowIfNull(traceId);
        if (traceId.Length is 0 or > MaximumTraceIdLength || !traceId.All(IsSafeTraceCharacter))
        {
            throw new ArgumentException(
                $"Trace ID must contain 1 to {MaximumTraceIdLength} safe ASCII characters.",
                nameof(traceId));
        }

        ActorUserId = actorUserId;
        TraceId = traceId;
    }

    public Guid ActorUserId { get; }

    public string TraceId { get; }

    private static bool IsSafeTraceCharacter(char value)
    {
        return value is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-' or '_' or '.' or ':';
    }
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
