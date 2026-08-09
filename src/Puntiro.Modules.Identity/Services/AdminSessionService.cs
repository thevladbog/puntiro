using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Domain;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Security;

namespace Puntiro.Modules.Identity.Services;

internal sealed class AdminSessionService(
    IdentityDbContext context,
    SessionTokenCodec tokenCodec,
    TimeProvider timeProvider) : IAdminSessionService
{
    public async Task<IssuedAdminSession> CreateAsync(
        VerifiedIdentity identity,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        AdminUser.EnsureId(identity.UserId, nameof(identity));
        AdminUser.EnsureId(organizationId, nameof(organizationId));
        var activeUser = await context.AdminUsers.AsNoTracking().AnyAsync(
            item => item.Id == identity.UserId && item.Status == AdminUserStatus.Active,
            cancellationToken);
        if (!activeUser)
        {
            throw new InvalidOperationException("An active admin user is required to create a session.");
        }

        using var token = tokenCodec.Issue();
        var now = timeProvider.GetUtcNow();
        var absolute = now + SessionExpiryPolicy.AbsoluteLifetime;
        var session = new AdminSession(
            Guid.CreateVersion7(),
            token.PublicId,
            token.Verifier,
            token.KeyVersion,
            identity.UserId,
            organizationId,
            now,
            SessionExpiryPolicy.NextIdleExpiry(now, absolute),
            absolute,
            identity.Factor == VerifiedFactor.Totp ? identity.VerifiedAt : null);
        context.Sessions.Add(session);
        context.SecurityEvents.Add(Event(
            identity.UserId,
            organizationId,
            session.Id,
            "session.created",
            "success",
            identity.Factor == VerifiedFactor.Totp ? "totp_login" : "recovery_login",
            now));
        await context.SaveChangesAsync(cancellationToken);
        return new IssuedAdminSession(Principal(session), token.TakeRawToken());
    }

    public async Task<AdminSessionPrincipal?> ValidateAsync(
        string presentedToken,
        CancellationToken cancellationToken)
    {
        if (!tokenCodec.TryRead(presentedToken, out var publicId, out var parsedSecret))
        {
            return null;
        }

        CryptographicOperations.ZeroMemory(parsedSecret);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockSessionAsync(publicId, cancellationToken);
        var session = await context.Sessions.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (session is null ||
            !tokenCodec.Verify(presentedToken, session.PublicId, session.KeyVersion, session.Verifier))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (!SessionExpiryPolicy.IsValid(
                now,
                session.IdleExpiresAtUtc,
                session.AbsoluteExpiresAtUtc,
                session.RevokedAtUtc))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (now - session.LastSeenAtUtc >= SessionExpiryPolicy.LastSeenWriteInterval)
        {
            session.Observe(now, SessionExpiryPolicy.NextIdleExpiry(now, session.AbsoluteExpiresAtUtc));
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Principal(session);
    }

    public async Task RevokeAsync(
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken)
    {
        AdminUser.EnsureId(sessionId, nameof(sessionId));
        var safeReason = ValidateReason(reason);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.sessions WHERE id = {sessionId} FOR UPDATE",
            cancellationToken);
        var session = await context.Sessions.SingleOrDefaultAsync(
            item => item.Id == sessionId,
            cancellationToken) ?? throw new KeyNotFoundException("Session was not found.");
        if (session.RevokedAtUtc is null)
        {
            var now = timeProvider.GetUtcNow();
            session.Revoke(now, safeReason);
            context.SecurityEvents.Add(Event(
                session.UserId,
                session.ActiveOrganizationId,
                session.Id,
                "session.revoked",
                "success",
                safeReason,
                now));
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken)
    {
        AdminUser.EnsureId(userId, nameof(userId));
        var safeReason = ValidateReason(reason);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.admin_users WHERE id = {userId} FOR UPDATE",
            cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.sessions WHERE user_id = {userId} AND revoked_at IS NULL FOR UPDATE",
            cancellationToken);
        var sessions = await context.Sessions
            .Where(item => item.UserId == userId && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        if (sessions.Count > 0)
        {
            var now = timeProvider.GetUtcNow();
            foreach (var session in sessions)
            {
                session.Revoke(now, safeReason);
            }

            context.SecurityEvents.Add(Event(
                userId,
                null,
                null,
                "session.revoked_all",
                "success",
                safeReason,
                now));
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private Task<int> LockSessionAsync(Guid publicId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.sessions WHERE public_id = {publicId} FOR UPDATE",
            cancellationToken);

    private static AdminSessionPrincipal Principal(AdminSession session) =>
        new(
            session.Id,
            session.UserId,
            session.ActiveOrganizationId,
            session.IdleExpiresAtUtc,
            session.AbsoluteExpiresAtUtc,
            session.SecondFactorVerifiedAtUtc);

    private static string ValidateReason(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        if (reason.Length is 0 or > 80 || reason.Any(static character => character is not (
                >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')))
        {
            throw new ArgumentException("Revoke reason must be a bounded safe reason code.", nameof(reason));
        }

        return reason;
    }

    private static IdentitySecurityEvent Event(
        Guid? userId,
        Guid? organizationId,
        Guid? sessionId,
        string eventType,
        string result,
        string reasonCode,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, organizationId, sessionId, eventType, result, reasonCode, now);
}
