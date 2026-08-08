namespace Puntiro.Modules.Tenancy.Domain;

public enum OrganizationStatus
{
    Provisioning,
    Active,
    Suspended
}

internal sealed class Organization
{
    private Organization()
    {
    }

    private Organization(Guid id, string displayName, OrganizationSlug slug)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Organization ID cannot be empty.", nameof(id));
        }

        Id = id;
        DisplayName = NormalizeDisplayName(displayName);
        Slug = slug.Value;
        Status = OrganizationStatus.Provisioning;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public OrganizationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public long Version { get; private set; }

    public static Organization StartProvisioning(Guid id, string displayName, string slug)
    {
        return new Organization(id, displayName, OrganizationSlug.Normalize(slug));
    }

    internal void SetCreatedAt(DateTimeOffset createdAtUtc)
    {
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Activate(bool hasActiveOwner)
    {
        if (Status != OrganizationStatus.Provisioning)
        {
            throw new InvalidOperationException("Only a provisioning organization can be activated.");
        }

        if (!hasActiveOwner)
        {
            throw new InvalidOperationException("An active owner is required before organization activation.");
        }

        Status = OrganizationStatus.Active;
        Version++;
    }

    public void Suspend()
    {
        if (Status != OrganizationStatus.Active)
        {
            throw new InvalidOperationException("Only an active organization can be suspended.");
        }

        Status = OrganizationStatus.Suspended;
        Version++;
    }

    internal void MarkUpdated(DateTimeOffset updatedAtUtc)
    {
        UpdatedAtUtc = EnsureUtc(updatedAtUtc);
    }

    private static string NormalizeDisplayName(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        var normalized = displayName.Trim();
        if (normalized.Length is 0 or > 200)
        {
            throw new ArgumentException(
                "Organization display name must contain between 1 and 200 characters.",
                nameof(displayName));
        }

        return normalized;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero
            ? value
            : value.ToUniversalTime();
    }
}
