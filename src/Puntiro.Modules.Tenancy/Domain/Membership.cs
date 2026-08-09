namespace Puntiro.Modules.Tenancy.Domain;

public enum MembershipRole
{
    Owner
}

public enum MembershipStatus
{
    Active,
    Revoked
}

internal sealed class Membership
{
    private Membership()
    {
    }

    private Membership(Guid id, Guid organizationId, Guid userId, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Membership ID cannot be empty.", nameof(id));
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        Id = id;
        OrganizationId = organizationId;
        UserId = userId;
        Role = MembershipRole.Owner;
        Status = MembershipStatus.Active;
        CreatedAtUtc = createdAtUtc.Offset == TimeSpan.Zero
            ? createdAtUtc
            : createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public MembershipRole Role { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public long Version { get; private set; }

    internal static Membership CreateOwner(
        Guid id,
        Guid organizationId,
        Guid userId,
        DateTimeOffset createdAtUtc)
    {
        return new Membership(id, organizationId, userId, createdAtUtc);
    }

    internal void Revoke(DateTimeOffset revokedAtUtc)
    {
        if (Status == MembershipStatus.Revoked)
        {
            return;
        }

        Status = MembershipStatus.Revoked;
        UpdatedAtUtc = revokedAtUtc.Offset == TimeSpan.Zero
            ? revokedAtUtc
            : revokedAtUtc.ToUniversalTime();
        Version++;
    }
}
