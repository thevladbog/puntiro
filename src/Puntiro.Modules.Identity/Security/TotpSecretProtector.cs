using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Puntiro.Modules.Identity.Security;

internal sealed class TotpSecretProtector(IDataProtectionProvider provider)
{
    private const string Purpose = "Puntiro.Identity.Totp.v1";

    public byte[] Protect(Guid userId, ReadOnlySpan<byte> secret)
    {
        EnsureUserId(userId);
        if (secret.IsEmpty)
        {
            throw new ArgumentException("TOTP secret cannot be empty.", nameof(secret));
        }

        var plaintext = secret.ToArray();
        try
        {
            return CreateProtector(userId).Protect(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public byte[] Unprotect(Guid userId, ReadOnlySpan<byte> protectedSecret)
    {
        EnsureUserId(userId);
        if (protectedSecret.IsEmpty)
        {
            throw new ArgumentException("Protected TOTP secret cannot be empty.", nameof(protectedSecret));
        }

        var ciphertext = protectedSecret.ToArray();
        try
        {
            return CreateProtector(userId).Unprotect(ciphertext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    private IDataProtector CreateProtector(Guid userId) =>
        provider.CreateProtector(Purpose, userId.ToString("D"));

    private static void EnsureUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }
    }
}
