using System.Diagnostics;

namespace Puntiro.Modules.Integrations.Domain;

internal sealed class IntegrationSecurityEvent
{
    internal const int MaximumTraceIdLength = 128;

    private IntegrationSecurityEvent()
    {
    }

    private IntegrationSecurityEvent(
        Guid id,
        Guid organizationId,
        Guid actorUserId,
        Guid tokenId,
        string eventType,
        string reasonCode,
        DateTimeOffset occurredAtUtc)
    {
        EnsureId(id, nameof(id));
        EnsureId(organizationId, nameof(organizationId));
        EnsureId(actorUserId, nameof(actorUserId));
        EnsureId(tokenId, nameof(tokenId));
        Id = id;
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        TokenId = tokenId;
        TraceId = CreateTraceId(id);
        EventType = eventType;
        Result = "success";
        ReasonCode = reasonCode;
        OccurredAtUtc = occurredAtUtc.Offset == TimeSpan.Zero
            ? occurredAtUtc
            : occurredAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid TokenId { get; private set; }
    public string TraceId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Result { get; private set; } = string.Empty;
    public string ReasonCode { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }

    internal static IntegrationSecurityEvent Created(
        Guid organizationId,
        Guid actorUserId,
        Guid tokenId,
        DateTimeOffset occurredAtUtc) =>
        Created(Guid.CreateVersion7(), organizationId, actorUserId, tokenId, occurredAtUtc);

    internal static IntegrationSecurityEvent Created(
        Guid eventId,
        Guid organizationId,
        Guid actorUserId,
        Guid tokenId,
        DateTimeOffset occurredAtUtc) =>
        new(
            eventId,
            organizationId,
            actorUserId,
            tokenId,
            "integration_token.created",
            "token_created",
            occurredAtUtc);

    internal static IntegrationSecurityEvent Revoked(
        Guid organizationId,
        Guid actorUserId,
        Guid tokenId,
        DateTimeOffset occurredAtUtc) =>
        Revoked(Guid.CreateVersion7(), organizationId, actorUserId, tokenId, occurredAtUtc);

    internal static IntegrationSecurityEvent Revoked(
        Guid eventId,
        Guid organizationId,
        Guid actorUserId,
        Guid tokenId,
        DateTimeOffset occurredAtUtc) =>
        new(
            eventId,
            organizationId,
            actorUserId,
            tokenId,
            "integration_token.revoked",
            "manual_revoke",
            occurredAtUtc);

    private static void EnsureId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }

    private static string CreateTraceId(Guid eventId)
    {
        var activity = Activity.Current;
        var activityTraceId = activity is { IdFormat: ActivityIdFormat.W3C } &&
            activity.TraceId != default
            ? activity.TraceId.ToString()
            : null;

        return !string.IsNullOrEmpty(activityTraceId)
            ? activityTraceId
            : $"trace:integration:{eventId:N}";
    }
}
