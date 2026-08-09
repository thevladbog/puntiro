namespace Puntiro.Modules.Identity.Domain;

internal sealed class IdentitySecurityEvent
{
    private IdentitySecurityEvent()
    {
    }

    internal IdentitySecurityEvent(
        Guid id,
        Guid? userId,
        Guid? actorUserId,
        Guid? organizationId,
        Guid? sessionId,
        string traceId,
        string eventType,
        string result,
        string reasonCode,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        UserId = userId;
        ActorUserId = actorUserId;
        OrganizationId = organizationId;
        SessionId = sessionId;
        TraceId = traceId;
        EventType = eventType;
        Result = result;
        ReasonCode = reasonCode;
        OccurredAtUtc = AdminUser.EnsureUtc(occurredAtUtc);
    }

    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string TraceId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Result { get; private set; } = string.Empty;
    public string ReasonCode { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
