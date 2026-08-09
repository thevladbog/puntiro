namespace Puntiro.Modules.Identity.Domain;

internal sealed class IdentitySecurityEvent
{
    private IdentitySecurityEvent()
    {
    }

    internal IdentitySecurityEvent(
        Guid id,
        Guid? userId,
        Guid? organizationId,
        Guid? sessionId,
        string eventType,
        string result,
        string reasonCode,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        UserId = userId;
        OrganizationId = organizationId;
        SessionId = sessionId;
        EventType = eventType;
        Result = result;
        ReasonCode = reasonCode;
        OccurredAtUtc = AdminUser.EnsureUtc(occurredAtUtc);
    }

    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Result { get; private set; } = string.Empty;
    public string ReasonCode { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
