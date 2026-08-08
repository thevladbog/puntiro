namespace Puntiro.Modules.Tenancy.Domain;

internal sealed class TenancySecurityEvent
{
    private TenancySecurityEvent()
    {
    }

    private TenancySecurityEvent(
        Guid id,
        Guid organizationId,
        string eventType,
        string result,
        string reasonCode,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        EventType = eventType;
        Result = result;
        ReasonCode = reasonCode;
        OccurredAtUtc = occurredAtUtc.Offset == TimeSpan.Zero
            ? occurredAtUtc
            : occurredAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Result { get; private set; } = string.Empty;

    public string ReasonCode { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    internal static TenancySecurityEvent OrganizationActivated(
        Guid id,
        Guid organizationId,
        DateTimeOffset occurredAtUtc)
    {
        return new TenancySecurityEvent(
            id,
            organizationId,
            "organization.activated",
            "success",
            "provisioning_completed",
            occurredAtUtc);
    }
}
