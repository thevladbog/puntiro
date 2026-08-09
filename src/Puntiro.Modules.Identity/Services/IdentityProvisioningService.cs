using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Domain;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Security;
using Puntiro.Security;

namespace Puntiro.Modules.Identity.Services;

internal sealed class IdentityProvisioningService(
    IdentityDbContext context,
    IPasswordHasher passwordHasher,
    Rfc6238Totp totp,
    RecoveryCodeService recoveryCodeService,
    TotpSecretProtector totpProtector,
    TimeProvider timeProvider,
    string currentRecoveryKeyVersion,
    IdentityKeyOptions keyOptions,
    ISecretGenerator secretGenerator) : IIdentityProvisioningService
{
    public async Task<PendingOwnerIdentity> BeginOwnerAsync(
        Guid provisioningOrganizationId,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        AdminUser.EnsureId(provisioningOrganizationId, nameof(provisioningOrganizationId));
        var address = EmailAddress.Normalize(email);
        var passwordHash = await passwordHasher.HashAsync(password, cancellationToken);
        var rawTotpSecret = totp.GenerateSecret();
        var generatedRecovery = recoveryCodeService.GenerateBatch();
        var batchId = Guid.CreateVersion7();
        PendingOwnerIdentity? result = null;

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var user = await context.AdminUsers.SingleOrDefaultAsync(
                item => item.NormalizedEmail == address.Normalized,
                cancellationToken);
            var now = timeProvider.GetUtcNow();
            byte[] protectedSecret;
            if (user is null)
            {
                user = AdminUser.StartProvisioning(
                    Guid.CreateVersion7(),
                    address.Display,
                    address.Normalized,
                    provisioningOrganizationId,
                    now);
                context.AdminUsers.Add(user);
                context.PasswordCredentials.Add(new PasswordCredential(user.Id, passwordHash, now));
                protectedSecret = totpProtector.Protect(user.Id, rawTotpSecret);
                context.TotpCredentials.Add(new TotpCredential(user.Id, protectedSecret, now));
            }
            else
            {
                await LockUserAsync(user.Id, cancellationToken);
                await context.Entry(user).ReloadAsync(cancellationToken);
                if (user.Status != AdminUserStatus.Provisioning ||
                    user.ProvisioningOrganizationId != provisioningOrganizationId)
                {
                    throw new InvalidOperationException("The owner email cannot be used for this provisioning operation.");
                }

                user.ContinueProvisioning(address.Display, now);
                var passwordCredential = await context.PasswordCredentials.SingleAsync(
                    item => item.UserId == user.Id,
                    cancellationToken);
                passwordCredential.Replace(passwordHash, now);
                protectedSecret = totpProtector.Protect(user.Id, rawTotpSecret);
                var totpCredential = await context.TotpCredentials.SingleAsync(
                    item => item.UserId == user.Id,
                    cancellationToken);
                totpCredential.Replace(protectedSecret, now);
                var oldRecovery = await context.RecoveryCodes
                    .Where(item => item.UserId == user.Id)
                    .ToListAsync(cancellationToken);
                context.RecoveryCodes.RemoveRange(oldRecovery);
                await LockActiveSessionsForUserAsync(user.Id, cancellationToken);
                var oldSessions = await context.Sessions
                    .Where(item => item.UserId == user.Id && item.RevokedAtUtc == null)
                    .ToListAsync(cancellationToken);
                foreach (var session in oldSessions)
                {
                    session.Revoke(now, "provisioning_restarted");
                }
            }

            CryptographicOperations.ZeroMemory(protectedSecret);
            foreach (var generated in generatedRecovery)
            {
                context.RecoveryCodes.Add(new RecoveryCode(
                    Guid.CreateVersion7(),
                    user.Id,
                    batchId,
                    generated.Verifier,
                    currentRecoveryKeyVersion,
                    now));
            }

            context.SecurityEvents.Add(Event(
                user.Id,
                provisioningOrganizationId,
                null,
                "owner.provisioning_started",
                "success",
                "pending_factor_issued",
                now));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            result = new PendingOwnerIdentity(
                user.Id,
                user.DisplayEmail,
                CreateTotpUri(user.DisplayEmail, rawTotpSecret),
                generatedRecovery.Select(static item => item.Code).ToArray());
            return result;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                   { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InvalidOperationException("The owner email cannot be used for this provisioning operation.");
        }
        catch (PostgresException exception) when (exception.SqlState is
                   PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {
            throw new InvalidOperationException("The owner email cannot be used for this provisioning operation.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rawTotpSecret);
            ClearPasswordHash(passwordHash);
            if (result is null)
            {
                foreach (var item in generatedRecovery)
                {
                    item.Code.Dispose();
                }
            }

            foreach (var item in generatedRecovery)
            {
                CryptographicOperations.ZeroMemory(item.Verifier);
            }
        }
    }

    public async Task ConfirmOwnerTotpAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        AdminUser.EnsureId(userId, nameof(userId));
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockUserAsync(userId, cancellationToken);
        var user = await context.AdminUsers.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Owner account was not found.");
        if (user.Status != AdminUserStatus.Provisioning)
        {
            throw new InvalidOperationException("Only a provisioning owner can confirm TOTP.");
        }

        var credential = await context.TotpCredentials.SingleAsync(
            item => item.UserId == userId,
            cancellationToken);
        var secret = totpProtector.Unprotect(userId, credential.ProtectedSecret);
        try
        {
            if (!totp.TryAccept(secret, code, credential.LastAcceptedCounter, out var accepted))
            {
                throw new InvalidOperationException("The TOTP code is invalid.");
            }

            var now = timeProvider.GetUtcNow();
            credential.Accept(accepted.Counter, now, confirm: true);
            context.SecurityEvents.Add(Event(
                userId,
                user.ProvisioningOrganizationId,
                null,
                "owner.totp_confirmed",
                "success",
                "factor_confirmed",
                now));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public async Task CompleteOwnerAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        AdminUser.EnsureId(userId, nameof(userId));
        AdminUser.EnsureId(organizationId, nameof(organizationId));
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockUserAsync(userId, cancellationToken);
        var user = await context.AdminUsers.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Owner account was not found.");
        if (user.Status == AdminUserStatus.Active)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var totpCredential = await context.TotpCredentials.AsNoTracking().SingleAsync(
            item => item.UserId == userId,
            cancellationToken);
        if (totpCredential.ConfirmedAtUtc is null)
        {
            throw new InvalidOperationException("TOTP must be confirmed before owner activation.");
        }

        var now = timeProvider.GetUtcNow();
        user.Activate(organizationId, now);
        context.SecurityEvents.Add(Event(
            userId,
            organizationId,
            null,
            "owner.activated",
            "success",
            "provisioning_completed",
            now));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PendingOwnerTotpReset> PrepareOwnerTotpResetAsync(
        Guid userId,
        string password,
        string recoveryCode,
        CancellationToken cancellationToken)
    {
        AdminUser.EnsureId(userId, nameof(userId));
        var user = await context.AdminUsers.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == userId && item.Status == AdminUserStatus.Active,
            cancellationToken) ?? throw new InvalidOperationException("Owner credentials are invalid.");
        var passwordCredential = await context.PasswordCredentials.AsNoTracking().SingleAsync(
            item => item.UserId == userId,
            cancellationToken);
        var passwordSnapshot = passwordCredential.Snapshot();
        PasswordVerification passwordResult;
        try
        {
            passwordResult = await passwordHasher.VerifyAsync(
                password,
                passwordSnapshot,
                cancellationToken);
        }
        finally
        {
            ClearPasswordHash(passwordSnapshot);
        }
        if (passwordResult == PasswordVerification.Failed)
        {
            throw new InvalidOperationException("Owner credentials are invalid.");
        }

        var recoveryRows = await context.RecoveryCodes.AsNoTracking()
            .Where(item => item.UserId == userId && item.UsedAtUtc == null)
            .ToListAsync(cancellationToken);
        var verified = recoveryRows.SingleOrDefault(item =>
            VerifyRecovery(item.KeyVersion, recoveryCode, item.Verifier));
        if (verified is null)
        {
            throw new InvalidOperationException("Owner credentials are invalid.");
        }

        var secret = totp.GenerateSecret();
        var generated = recoveryCodeService.GenerateBatch();
        try
        {
            return new PendingOwnerTotpReset
            {
                UserId = userId,
                TotpUri = CreateTotpUri(user.DisplayEmail, secret),
                RecoveryCodes = generated.Select(static item => item.Code).ToArray(),
                CandidateSecret = secret,
                CandidateRecoveryVerifiers = generated.Select(static item => item.Verifier).ToArray(),
                CandidateBatchId = Guid.CreateVersion7(),
                VerifiedRecoveryCodeId = verified.Id,
                VerifiedRecoveryCodeVersion = verified.Version,
                RecoveryKeyVersion = currentRecoveryKeyVersion
            };
        }
        catch
        {
            CryptographicOperations.ZeroMemory(secret);
            foreach (var item in generated)
            {
                item.Code.Dispose();
                CryptographicOperations.ZeroMemory(item.Verifier);
            }

            throw;
        }
    }

    public async Task CompleteOwnerTotpResetAsync(
        PendingOwnerTotpReset pending,
        string firstTotpCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        pending.ThrowIfDisposed();
        if (!totp.TryAccept(pending.CandidateSecret, firstTotpCode, null, out var accepted))
        {
            throw new InvalidOperationException("The TOTP code is invalid.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await LockUserAsync(pending.UserId, cancellationToken);
        var oldRecovery = await context.RecoveryCodes.SingleOrDefaultAsync(
            item => item.Id == pending.VerifiedRecoveryCodeId &&
                item.UserId == pending.UserId &&
                item.UsedAtUtc == null &&
                item.Version == pending.VerifiedRecoveryCodeVersion,
            cancellationToken);
        if (oldRecovery is null)
        {
            throw new InvalidOperationException("The recovery authorization is no longer valid.");
        }

        var user = await context.AdminUsers.SingleAsync(item => item.Id == pending.UserId, cancellationToken);
        if (user.Status != AdminUserStatus.Active)
        {
            throw new InvalidOperationException("The recovery authorization is no longer valid.");
        }

        var now = timeProvider.GetUtcNow();
        var protectedSecret = totpProtector.Protect(pending.UserId, pending.CandidateSecret);
        try
        {
            var credential = await context.TotpCredentials.SingleAsync(
                item => item.UserId == pending.UserId,
                cancellationToken);
            credential.Replace(protectedSecret, now);
            credential.Accept(accepted.Counter, now, confirm: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedSecret);
        }

        var oldCodes = await context.RecoveryCodes
            .Where(item => item.UserId == pending.UserId)
            .ToListAsync(cancellationToken);
        context.RecoveryCodes.RemoveRange(oldCodes);
        foreach (var verifier in pending.CandidateRecoveryVerifiers)
        {
            context.RecoveryCodes.Add(new RecoveryCode(
                Guid.CreateVersion7(),
                pending.UserId,
                pending.CandidateBatchId,
                verifier,
                pending.RecoveryKeyVersion,
                now));
        }

        await LockActiveSessionsForUserAsync(pending.UserId, cancellationToken);
        var sessions = await context.Sessions
            .Where(item => item.UserId == pending.UserId && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now, "totp_reset");
        }

        context.SecurityEvents.Add(Event(
            pending.UserId,
            null,
            null,
            "owner.totp_reset",
            "success",
            "credentials_replaced",
            now));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<int> LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.admin_users WHERE id = {userId} FOR UPDATE",
            cancellationToken);

    private Task<int> LockActiveSessionsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.sessions WHERE user_id = {userId} AND revoked_at IS NULL FOR UPDATE",
            cancellationToken);

    private static IdentitySecurityEvent Event(
        Guid? userId,
        Guid? organizationId,
        Guid? sessionId,
        string eventType,
        string result,
        string reasonCode,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, organizationId, sessionId, eventType, result, reasonCode, now);

    private static SensitiveValue CreateTotpUri(string displayEmail, ReadOnlySpan<byte> secret)
    {
        const string prefix = "otpauth://totp/Puntiro:";
        const string queryPrefix = "?secret=";
        const string suffix = "&issuer=Puntiro&algorithm=SHA1&digits=6&period=30";
        var escapedEmail = Uri.EscapeDataString(displayEmail);
        Span<char> encodedSecret = stackalloc char[Base32.GetEncodedLength(secret.Length)];
        Base32.Encode(secret, encodedSecret);
        var buffer = new char[prefix.Length + escapedEmail.Length + queryPrefix.Length + encodedSecret.Length + suffix.Length];
        try
        {
            var destination = buffer.AsSpan();
            var offset = 0;
            prefix.AsSpan().CopyTo(destination[offset..]);
            offset += prefix.Length;
            escapedEmail.AsSpan().CopyTo(destination[offset..]);
            offset += escapedEmail.Length;
            queryPrefix.AsSpan().CopyTo(destination[offset..]);
            offset += queryPrefix.Length;
            encodedSecret.CopyTo(destination[offset..]);
            offset += encodedSecret.Length;
            suffix.AsSpan().CopyTo(destination[offset..]);
            return new SensitiveValue(destination);
        }
        finally
        {
            encodedSecret.Clear();
            Array.Clear(buffer);
        }
    }

    private static void ClearPasswordHash(PasswordHash hash)
    {
        CryptographicOperations.ZeroMemory(hash.Salt);
        CryptographicOperations.ZeroMemory(hash.Hash);
    }

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
