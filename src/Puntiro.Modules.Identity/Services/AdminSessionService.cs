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
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(auditContext);
        AdminUser.EnsureId(identity.UserId, nameof(identity));
        AdminUser.EnsureId(organizationId, nameof(organizationId));
        using var token = tokenCodec.Issue();
        var sessionId = Guid.CreateVersion7();
        var operationEventId = Guid.CreateVersion7();
        var principal = await IdentityExecutionStrategy.ExecuteInTransactionAsync(
            context,
            retryToken => CreateAttemptAsync(
                identity,
                organizationId,
                auditContext,
                token,
                sessionId,
                operationEventId,
                retryToken),
            retryToken => context.SecurityEvents.AsNoTracking().AnyAsync(
                item => item.Id == operationEventId,
                retryToken),
            cancellationToken);
        return new IssuedAdminSession(principal, token.TakeRawToken());
    }

    private async Task<AdminSessionPrincipal> CreateAttemptAsync(
        VerifiedIdentity identity,
        Guid organizationId,
        IdentityAuditContext auditContext,
        IssuedSessionToken token,
        Guid sessionId,
        Guid operationEventId,
        CancellationToken cancellationToken)
    {
        await LockUserAsync(identity.UserId, cancellationToken);
        var user = await context.AdminUsers.SingleOrDefaultAsync(
            item => item.Id == identity.UserId,
            cancellationToken);
        if (user is not null)
        {
            await context.Entry(user).ReloadAsync(cancellationToken);
        }

        if (user is null ||
            user.Status != AdminUserStatus.Active ||
            user.AuthenticationEpoch != identity.AuthenticationEpoch)
        {
            throw new InvalidOperationException("An active admin user is required to create a session.");
        }

        var now = timeProvider.GetUtcNow();
        var absolute = now + SessionExpiryPolicy.AbsoluteLifetime;
        var session = new AdminSession(
            sessionId,
            token.PublicId,
            token.Verifier,
            token.KeyVersion,
            identity.UserId,
            identity.AuthenticationEpoch,
            organizationId,
            now,
            SessionExpiryPolicy.NextIdleExpiry(now, absolute),
            absolute,
            identity.Factor == VerifiedFactor.Totp ? identity.VerifiedAt : null);
        context.Sessions.Add(session);
        context.SecurityEvents.Add(Event(
            operationEventId,
            identity.UserId,
            auditContext.ActorUserId ?? identity.UserId,
            organizationId,
            session.Id,
            auditContext.TraceId,
            "session.created",
            "success",
            identity.Factor == VerifiedFactor.Totp ? "totp_login" : "recovery_login",
            now));
        await context.SaveChangesAsync(cancellationToken);
        return Principal(session, user.DisplayEmail);
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
        var userId = await context.Sessions.AsNoTracking()
            .Where(item => item.PublicId == publicId)
            .Select(item => (Guid?)item.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is null)
        {
            return null;
        }

        return await IdentityExecutionStrategy.ExecuteInTransactionAsync(
            context,
            token => ValidateAttemptAsync(presentedToken, publicId, userId.Value, token),
            verifySucceeded: null,
            cancellationToken);
    }

    private async Task<AdminSessionPrincipal?> ValidateAttemptAsync(
        string presentedToken,
        Guid publicId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await LockUserAsync(userId, cancellationToken);
        await LockSessionAsync(publicId, cancellationToken);
        var session = await context.Sessions.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        var user = await context.AdminUsers.SingleOrDefaultAsync(
            item => item.Id == userId,
            cancellationToken);
        if (session is not null)
        {
            await context.Entry(session).ReloadAsync(cancellationToken);
        }

        if (user is not null)
        {
            await context.Entry(user).ReloadAsync(cancellationToken);
        }

        if (session is null || user is null ||
            user.Status != AdminUserStatus.Active ||
            session.AuthenticationEpoch != user.AuthenticationEpoch ||
            !tokenCodec.Verify(presentedToken, session.PublicId, session.KeyVersion, session.Verifier))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (!SessionExpiryPolicy.IsValid(
                now,
                session.IdleExpiresAtUtc,
                session.AbsoluteExpiresAtUtc,
                session.RevokedAtUtc))
        {
            return null;
        }

        if (now - session.LastSeenAtUtc >= SessionExpiryPolicy.LastSeenWriteInterval)
        {
            session.Observe(now, SessionExpiryPolicy.NextIdleExpiry(now, session.AbsoluteExpiresAtUtc));
            await context.SaveChangesAsync(cancellationToken);
        }

        return Principal(session, user.DisplayEmail);
    }

    public async Task<AdminSessionPrincipal?> RecordStepUpAsync(
        Guid sessionId,
        DateTimeOffset verifiedAt,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditContext);
        if (sessionId == Guid.Empty)
        {
            return null;
        }

        var initialNow = timeProvider.GetUtcNow();
        var verifiedAtUtc = verifiedAt.Offset == TimeSpan.Zero
            ? verifiedAt
            : verifiedAt.ToUniversalTime();
        if (verifiedAtUtc > initialNow || verifiedAtUtc < initialNow.AddMinutes(-1))
        {
            return null;
        }

        var userId = await context.Sessions.AsNoTracking()
            .Where(item => item.Id == sessionId)
            .Select(item => (Guid?)item.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is null)
        {
            return null;
        }

        var operationEventId = Guid.CreateVersion7();
        return await IdentityExecutionStrategy.ExecuteInTransactionAsync(
            context,
            token => RecordStepUpAttemptAsync(
                sessionId,
                userId.Value,
                verifiedAtUtc,
                auditContext,
                operationEventId,
                token),
            token => context.SecurityEvents.AsNoTracking().AnyAsync(
                item => item.Id == operationEventId,
                token),
            cancellationToken);
    }

    private async Task<AdminSessionPrincipal?> RecordStepUpAttemptAsync(
        Guid sessionId,
        Guid userId,
        DateTimeOffset verifiedAtUtc,
        IdentityAuditContext auditContext,
        Guid operationEventId,
        CancellationToken cancellationToken)
    {
        await LockUserAsync(userId, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.sessions WHERE id = {sessionId} FOR UPDATE",
            cancellationToken);
        var user = await context.AdminUsers.SingleOrDefaultAsync(
            item => item.Id == userId,
            cancellationToken);
        var session = await context.Sessions.SingleOrDefaultAsync(
            item => item.Id == sessionId,
            cancellationToken);
        if (user is not null)
        {
            await context.Entry(user).ReloadAsync(cancellationToken);
        }

        if (session is not null)
        {
            await context.Entry(session).ReloadAsync(cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        if (user is null || session is null ||
            user.Status != AdminUserStatus.Active ||
            session.AuthenticationEpoch != user.AuthenticationEpoch ||
            auditContext.ActorUserId is not null && auditContext.ActorUserId != user.Id ||
            verifiedAtUtc > now || verifiedAtUtc < now.AddMinutes(-1) ||
            !SessionExpiryPolicy.IsValid(
                now,
                session.IdleExpiresAtUtc,
                session.AbsoluteExpiresAtUtc,
                session.RevokedAtUtc))
        {
            return null;
        }

        session.RecordStepUp(verifiedAtUtc);
        context.SecurityEvents.Add(Event(
            operationEventId,
            user.Id,
            user.Id,
            session.ActiveOrganizationId,
            session.Id,
            auditContext.TraceId,
            "session.step_up",
            "success",
            "totp_accepted",
            now));
        await context.SaveChangesAsync(cancellationToken);
        return Principal(session, user.DisplayEmail);
    }

    public async Task RevokeAsync(
        Guid sessionId,
        string reason,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        AdminUser.EnsureId(sessionId, nameof(sessionId));
        ArgumentNullException.ThrowIfNull(auditContext);
        var safeReason = ValidateReason(reason);
        var operationEventId = Guid.CreateVersion7();
        await IdentityExecutionStrategy.ExecuteInTransactionAsync(
            context,
            async token =>
            {
                await RevokeAttemptAsync(
                    sessionId,
                    safeReason,
                    auditContext,
                    operationEventId,
                    token);
                return true;
            },
            token => context.SecurityEvents.AsNoTracking().AnyAsync(
                item => item.Id == operationEventId,
                token),
            cancellationToken);
    }

    private async Task RevokeAttemptAsync(
        Guid sessionId,
        string safeReason,
        IdentityAuditContext auditContext,
        Guid operationEventId,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.sessions WHERE id = {sessionId} FOR UPDATE",
            cancellationToken);
        var session = await context.Sessions.SingleOrDefaultAsync(
            item => item.Id == sessionId,
            cancellationToken) ?? throw new KeyNotFoundException("Session was not found.");
        await context.Entry(session).ReloadAsync(cancellationToken);
        if (session.RevokedAtUtc is null)
        {
            var now = timeProvider.GetUtcNow();
            session.Revoke(now, safeReason);
            context.SecurityEvents.Add(Event(
                operationEventId,
                session.UserId,
                auditContext.ActorUserId ?? session.UserId,
                session.ActiveOrganizationId,
                session.Id,
                auditContext.TraceId,
                "session.revoked",
                "success",
                safeReason,
                now));
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        string reason,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        AdminUser.EnsureId(userId, nameof(userId));
        ArgumentNullException.ThrowIfNull(auditContext);
        var safeReason = ValidateReason(reason);
        var operationEventId = Guid.CreateVersion7();
        await IdentityExecutionStrategy.ExecuteInTransactionAsync(
            context,
            async token =>
            {
                await RevokeAllAttemptAsync(
                    userId,
                    safeReason,
                    auditContext,
                    operationEventId,
                    token);
                return true;
            },
            token => context.SecurityEvents.AsNoTracking().AnyAsync(
                item => item.Id == operationEventId,
                token),
            cancellationToken);
    }

    private async Task RevokeAllAttemptAsync(
        Guid userId,
        string safeReason,
        IdentityAuditContext auditContext,
        Guid operationEventId,
        CancellationToken cancellationToken)
    {
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
                operationEventId,
                userId,
                auditContext.ActorUserId ?? userId,
                null,
                null,
                auditContext.TraceId,
                "session.revoked_all",
                "success",
                safeReason,
                now));
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private Task<int> LockSessionAsync(Guid publicId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.sessions WHERE public_id = {publicId} FOR UPDATE",
            cancellationToken);

    private Task<int> LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.admin_users WHERE id = {userId} FOR UPDATE",
            cancellationToken);

    private static AdminSessionPrincipal Principal(AdminSession session, string email) =>
        new(
            session.Id,
            session.UserId,
            email,
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
        Guid id,
        Guid? userId,
        Guid? actorUserId,
        Guid? organizationId,
        Guid? sessionId,
        string traceId,
        string eventType,
        string result,
        string reasonCode,
        DateTimeOffset now) =>
        new(
            id,
            userId,
            actorUserId,
            organizationId,
            sessionId,
            traceId,
            eventType,
            result,
            reasonCode,
            now);
}
