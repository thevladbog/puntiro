using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Domain;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Security;

namespace Puntiro.Modules.Identity.Services;

internal sealed class AdminAuthenticationService(
    IdentityDbContext context,
    IPasswordHasher passwordHasher,
    ITotpService totp,
    TotpSecretProtector totpProtector,
    TimeProvider timeProvider,
    IdentityKeyOptions keyOptions,
    Puntiro.Security.ISecretGenerator secretGenerator) : IAdminAuthenticationService
{
    private static readonly PasswordHash DummyCredential = new(
        Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray(),
        new byte[32],
        19456,
        2,
        1,
        "argon2id");

    public async Task<VerifiedIdentity?> VerifyAsync(
        AdminCredentials credentials,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(auditContext);
        var hasTotp = !string.IsNullOrEmpty(credentials.TotpCode);
        var hasRecovery = !string.IsNullOrEmpty(credentials.RecoveryCode);
        var exactlyOneFactor = hasTotp ^ hasRecovery;

        string? normalizedEmail;
        try
        {
            normalizedEmail = EmailAddress.Normalize(credentials.Email).Normalized;
        }
        catch (ArgumentException)
        {
            normalizedEmail = null;
        }

        var operationEventId = Guid.CreateVersion7();
        return await IdentityExecutionStrategy.ExecuteInTransactionAsync(
            context,
            token => VerifyAttemptAsync(
                credentials,
                auditContext,
                normalizedEmail,
                hasTotp,
                hasRecovery,
                exactlyOneFactor,
                operationEventId,
                token),
            token => context.SecurityEvents.AsNoTracking().AnyAsync(
                item => item.Id == operationEventId,
                token),
            cancellationToken);
    }

    private async Task<VerifiedIdentity?> VerifyAttemptAsync(
        AdminCredentials credentials,
        IdentityAuditContext auditContext,
        string? normalizedEmail,
        bool hasTotp,
        bool hasRecovery,
        bool exactlyOneFactor,
        Guid operationEventId,
        CancellationToken cancellationToken)
    {
        var user = normalizedEmail is null
            ? null
            : await context.AdminUsers.SingleOrDefaultAsync(
                item => item.NormalizedEmail == normalizedEmail,
                cancellationToken);
        if (user is null || user.Status != AdminUserStatus.Active)
        {
            await passwordHasher.VerifyAsync(
                credentials.Password ?? string.Empty,
                DummyCredential,
                cancellationToken);
            await AppendFailureAsync(
                operationEventId,
                user?.Id,
                user is null ? "account_unavailable" : "account_inactive",
                auditContext,
                cancellationToken);
            return null;
        }

        await LockUserAsync(user.Id, cancellationToken);
        await context.Entry(user).ReloadAsync(cancellationToken);
        if (user.Status != AdminUserStatus.Active)
        {
            await passwordHasher.VerifyAsync(
                credentials.Password ?? string.Empty,
                DummyCredential,
                cancellationToken);
            await AppendFailureAsync(
                operationEventId,
                user.Id,
                "account_inactive",
                auditContext,
                cancellationToken);
            return null;
        }

        var credential = await context.PasswordCredentials.SingleAsync(
            item => item.UserId == user.Id,
            cancellationToken);
        await context.Entry(credential).ReloadAsync(cancellationToken);
        var snapshot = credential.Snapshot();
        PasswordVerification passwordResult;
        try
        {
            passwordResult = await passwordHasher.VerifyAsync(
                credentials.Password ?? string.Empty,
                snapshot,
                cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(snapshot.Salt);
            CryptographicOperations.ZeroMemory(snapshot.Hash);
        }

        if (passwordResult == PasswordVerification.Failed || !exactlyOneFactor)
        {
            await AppendFailureAsync(
                operationEventId,
                user.Id,
                passwordResult == PasswordVerification.Failed ? "password_invalid" : "factor_shape_invalid",
                auditContext,
                cancellationToken);
            return null;
        }

        var now = timeProvider.GetUtcNow();
        VerifiedFactor? factor = hasTotp
            ? await TryAcceptTotpAsync(user.Id, credentials.TotpCode!, now, cancellationToken)
            : await TryUseRecoveryAsync(
                user.Id,
                credentials.RecoveryCode!,
                now,
                auditContext.TraceId,
                cancellationToken);
        if (factor is null)
        {
            await AppendFailureAsync(
                operationEventId,
                user.Id,
                "factor_invalid",
                auditContext,
                cancellationToken);
            return null;
        }

        if (passwordResult == PasswordVerification.ValidNeedsRehash)
        {
            var rehash = await passwordHasher.HashAsync(credentials.Password, cancellationToken);
            try
            {
                credential.Replace(rehash, now);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(rehash.Salt);
                CryptographicOperations.ZeroMemory(rehash.Hash);
            }
        }

        context.SecurityEvents.Add(Event(
            operationEventId,
            user.Id,
            user.Id,
            auditContext.TraceId,
            "authentication.login",
            "success",
            factor == VerifiedFactor.Totp ? "totp_accepted" : "recovery_accepted",
            now));
        await context.SaveChangesAsync(cancellationToken);
        return new VerifiedIdentity(user.Id, user.AuthenticationEpoch, factor.Value, now);
    }

    public async Task<DateTimeOffset?> StepUpTotpAsync(
        Guid userId,
        string code,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        AdminUser.EnsureId(userId, nameof(userId));
        ArgumentNullException.ThrowIfNull(auditContext);
        var operationEventId = Guid.CreateVersion7();
        return await IdentityExecutionStrategy.ExecuteInTransactionAsync(
            context,
            token => StepUpTotpAttemptAsync(
                userId,
                code,
                auditContext,
                operationEventId,
                token),
            token => context.SecurityEvents.AsNoTracking().AnyAsync(
                item => item.Id == operationEventId,
                token),
            cancellationToken);
    }

    private async Task<DateTimeOffset?> StepUpTotpAttemptAsync(
        Guid userId,
        string code,
        IdentityAuditContext auditContext,
        Guid operationEventId,
        CancellationToken cancellationToken)
    {
        await LockUserAsync(userId, cancellationToken);
        var active = await context.AdminUsers.AnyAsync(
            item => item.Id == userId && item.Status == AdminUserStatus.Active,
            cancellationToken);
        if (!active)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var factor = await TryAcceptTotpAsync(userId, code, now, cancellationToken);
        context.SecurityEvents.Add(Event(
            operationEventId,
            userId,
            auditContext.ActorUserId ?? userId,
            auditContext.TraceId,
            "authentication.step_up",
            factor is null ? "failure" : "success",
            factor is null ? "factor_invalid" : "totp_accepted",
            now));
        await context.SaveChangesAsync(cancellationToken);
        return factor is null ? null : now;
    }

    private async Task<VerifiedFactor?> TryAcceptTotpAsync(
        Guid userId,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var credential = await context.TotpCredentials.SingleAsync(
            item => item.UserId == userId,
            cancellationToken);
        await context.Entry(credential).ReloadAsync(cancellationToken);
        if (credential.ConfirmedAtUtc is null)
        {
            return null;
        }

        var secret = totpProtector.Unprotect(userId, credential.ProtectedSecret);
        try
        {
            if (!totp.TryAccept(secret, code, now, credential.LastAcceptedCounter, out var accepted))
            {
                return null;
            }

            credential.Accept(accepted.Counter, now, confirm: false);
            return VerifiedFactor.Totp;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private async Task<VerifiedFactor?> TryUseRecoveryAsync(
        Guid userId,
        string code,
        DateTimeOffset now,
        string traceId,
        CancellationToken cancellationToken)
    {
        var available = await context.RecoveryCodes
            .Where(item => item.UserId == userId && item.UsedAtUtc == null)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        RecoveryCode? match = null;
        foreach (var item in available)
        {
            var verified = VerifyRecovery(item.KeyVersion, code, item.Verifier);
            match ??= verified ? item : null;
        }

        if (match is null)
        {
            return null;
        }

        match.Use(now);
        context.SecurityEvents.Add(Event(
            Guid.CreateVersion7(),
            userId,
            userId,
            traceId,
            "recovery_code.used",
            "success",
            "login_factor",
            now));
        return VerifiedFactor.RecoveryCode;
    }

    private async Task AppendFailureAsync(
        Guid eventId,
        Guid? userId,
        string reasonCode,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        context.SecurityEvents.Add(Event(
            eventId,
            userId,
            null,
            auditContext.TraceId,
            "authentication.login",
            "failure",
            reasonCode,
            timeProvider.GetUtcNow()));
        await context.SaveChangesAsync(cancellationToken);
    }

    private Task<int> LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.admin_users WHERE id = {userId} FOR UPDATE",
            cancellationToken);

    private static IdentitySecurityEvent Event(
        Guid id,
        Guid? userId,
        Guid? actorUserId,
        string? auditContext,
        string eventType,
        string result,
        string reasonCode,
        DateTimeOffset now) =>
        new(
            id,
            userId,
            actorUserId,
            null,
            null,
            auditContext ?? throw new InvalidOperationException("Audit trace was not supplied."),
            eventType,
            result,
            reasonCode,
            now);

    private bool VerifyRecovery(string keyVersion, string code, ReadOnlySpan<byte> verifier)
    {
        if (!keyOptions.HasRecoveryKey(keyVersion))
        {
            return false;
        }

        var key = keyOptions.GetRecoveryKey(keyVersion);
        try
        {
            using var verifierService = new RecoveryCodeService(key, secretGenerator);
            return verifierService.Verify(code, verifier);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
