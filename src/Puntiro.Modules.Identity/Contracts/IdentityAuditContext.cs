namespace Puntiro.Modules.Identity.Contracts;

public sealed record IdentityAuditContext
{
    public const int MaximumTraceIdLength = 128;

    public IdentityAuditContext(Guid? actorUserId, string traceId)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("Actor user ID cannot be empty when supplied.", nameof(actorUserId));
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

    public Guid? ActorUserId { get; }

    public string TraceId { get; }

    private static bool IsSafeTraceCharacter(char value) =>
        value is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-' or '_' or '.' or ':';
}
