namespace Puntiro.Modules.Identity.Services;

internal static class SessionExpiryPolicy
{
    internal static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(12);
    internal static readonly TimeSpan LastSeenWriteInterval = TimeSpan.FromMinutes(5);

    internal static bool IsValid(
        DateTimeOffset utcNow,
        DateTimeOffset idleExpiresAtUtc,
        DateTimeOffset absoluteExpiresAtUtc,
        DateTimeOffset? revokedAtUtc) =>
        revokedAtUtc is null && utcNow < idleExpiresAtUtc && utcNow < absoluteExpiresAtUtc;

    internal static DateTimeOffset NextIdleExpiry(
        DateTimeOffset utcNow,
        DateTimeOffset absoluteExpiresAtUtc)
    {
        var candidate = utcNow + IdleLifetime;
        return candidate < absoluteExpiresAtUtc ? candidate : absoluteExpiresAtUtc;
    }
}
