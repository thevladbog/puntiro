namespace Puntiro.Modules.Tenancy.Domain;

internal sealed class TenancySecurityEvent
{
    private TenancySecurityEvent()
    {
    }

    private TenancySecurityEvent(
        Guid id,
        Guid organizationId,
        Guid actorUserId,
        string traceId,
        string eventType,
        string result,
        string reasonCode,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        TraceId = traceId;
        EventType = eventType;
        Result = result;
        ReasonCode = reasonCode;
        OccurredAtUtc = occurredAtUtc.Offset == TimeSpan.Zero
            ? occurredAtUtc
            : occurredAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string TraceId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string Result { get; private set; } = string.Empty;

    public string ReasonCode { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    internal static TenancySecurityEvent OrganizationActivated(
        Guid id,
        Guid organizationId,
        Guid actorUserId,
        string traceId,
        DateTimeOffset occurredAtUtc)
    {
        return new TenancySecurityEvent(
            id,
            organizationId,
            actorUserId,
            traceId,
            "organization.activated",
            "success",
            "provisioning_completed",
            occurredAtUtc);
    }
}
