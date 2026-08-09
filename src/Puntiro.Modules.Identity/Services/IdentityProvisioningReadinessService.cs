using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Domain;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Security;

namespace Puntiro.Modules.Identity.Services;

internal sealed class IdentityProvisioningReadinessService(
    IdentityDbContext context,
    IdentityKeyOptions keyOptions,
    TotpSecretProtector totpProtector,
    IDataProtectionKeyRingReadiness keyRingReadiness) : IIdentityProvisioningReadinessService
{
    private const int TotpSecretLength = 20;

    public async Task<bool> IsReadyForTrustedProvisioningAsync(
        CancellationToken cancellationToken)
    {
        var recoveryVersions = await context.RecoveryCodes.AsNoTracking()
            .Where(item => item.UsedAtUtc == null)
            .Select(item => item.KeyVersion)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (recoveryVersions.Any(version => !keyOptions.HasRecoveryKey(version)))
        {
            return false;
        }

        var sessionVersions = await context.Sessions.AsNoTracking()
            .Select(item => item.KeyVersion)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (sessionVersions.Any(version => !keyOptions.HasSessionKey(version)))
        {
            return false;
        }

        if (!CanUseCurrentHmacKeys() ||
            !keyRingReadiness.HasUsableCurrentKey() ||
            !CanRoundTripCurrentProtector())
        {
            return false;
        }

        var activeTotp = await context.TotpCredentials.AsNoTracking()
            .Join(
                context.AdminUsers.AsNoTracking()
                    .Where(item => item.Status == AdminUserStatus.Active),
                credential => credential.UserId,
                user => user.Id,
                (credential, _) => new { credential.UserId, credential.ProtectedSecret })
            .ToArrayAsync(cancellationToken);
        foreach (var credential in activeTotp)
        {
            byte[]? secret = null;
            try
            {
                secret = totpProtector.Unprotect(
                    credential.UserId,
                    credential.ProtectedSecret);
                if (secret.Length != TotpSecretLength)
                {
                    return false;
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
            finally
            {
                if (secret is not null)
                {
                    CryptographicOperations.ZeroMemory(secret);
                }
            }
        }

        return true;
    }

    private bool CanUseCurrentHmacKeys()
    {
        byte[]? sessionKey = null;
        byte[]? recoveryKey = null;
        Span<byte> probe = stackalloc byte[16];
        RandomNumberGenerator.Fill(probe);
        try
        {
            sessionKey = keyOptions.GetSessionKey(keyOptions.CurrentSessionKeyVersion);
            recoveryKey = keyOptions.GetRecoveryKey(keyOptions.CurrentRecoveryKeyVersion);
            Span<byte> sessionDigest = stackalloc byte[HMACSHA256.HashSizeInBytes];
            Span<byte> recoveryDigest = stackalloc byte[HMACSHA256.HashSizeInBytes];
            return HMACSHA256.TryHashData(sessionKey, probe, sessionDigest, out var sessionWritten) &&
                sessionWritten == sessionDigest.Length &&
                HMACSHA256.TryHashData(recoveryKey, probe, recoveryDigest, out var recoveryWritten) &&
                recoveryWritten == recoveryDigest.Length;
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(probe);
            if (sessionKey is not null)
            {
                CryptographicOperations.ZeroMemory(sessionKey);
            }

            if (recoveryKey is not null)
            {
                CryptographicOperations.ZeroMemory(recoveryKey);
            }
        }
    }

    private bool CanRoundTripCurrentProtector()
    {
        var userId = Guid.CreateVersion7();
        Span<byte> probe = stackalloc byte[TotpSecretLength];
        RandomNumberGenerator.Fill(probe);
        byte[]? protectedProbe = null;
        byte[]? unprotectedProbe = null;
        try
        {
            protectedProbe = totpProtector.Protect(userId, probe);
            unprotectedProbe = totpProtector.Unprotect(userId, protectedProbe);
            return CryptographicOperations.FixedTimeEquals(probe, unprotectedProbe);
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(probe);
            if (protectedProbe is not null)
            {
                CryptographicOperations.ZeroMemory(protectedProbe);
            }

            if (unprotectedProbe is not null)
            {
                CryptographicOperations.ZeroMemory(unprotectedProbe);
            }
        }
    }
}
