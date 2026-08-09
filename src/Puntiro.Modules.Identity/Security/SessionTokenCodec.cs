using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Puntiro.Security;

namespace Puntiro.Modules.Identity.Security;

[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class IssuedSessionToken : IDisposable
{
    public required Guid PublicId { get; init; }
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    public required byte[] Verifier { get; init; }
    public required string KeyVersion { get; init; }
    public required SensitiveValue RawToken { get; set; }

    private string DebuggerDisplay => nameof(IssuedSessionToken);

    public SensitiveValue TakeRawToken()
    {
        var value = RawToken;
        RawToken = new SensitiveValue(ReadOnlySpan<char>.Empty);
        return value;
    }

    public void Dispose()
    {
        RawToken.Dispose();
        CryptographicOperations.ZeroMemory(Verifier);
    }
}

internal sealed class SessionTokenCodec(IdentityKeyOptions keys, ISecretGenerator secretGenerator)
{
    private const string Prefix = "pns_";
    private const string Purpose = "Puntiro.Identity.Session.v1";
    private const int SecretLength = 32;
    private const int PublicIdLength = 32;
    private const int EncodedSecretLength = 43;
    private const int TokenLength = 4 + PublicIdLength + 1 + EncodedSecretLength;
    private static readonly byte[] PurposePrefix = Encoding.ASCII.GetBytes(Purpose + "\0");

    public IssuedSessionToken Issue()
    {
        Span<byte> publicBytes = stackalloc byte[16];
        Span<byte> secret = stackalloc byte[SecretLength];
        Span<char> token = stackalloc char[TokenLength];
        Span<char> base64 = stackalloc char[44];
        byte[]? verifier = null;
        try
        {
            secretGenerator.Fill(publicBytes);
            secretGenerator.Fill(secret);
            var publicId = new Guid(publicBytes);
            if (publicId == Guid.Empty)
            {
                throw new InvalidOperationException("Secret generator produced an invalid public session ID.");
            }

            Prefix.AsSpan().CopyTo(token);
            if (!publicId.TryFormat(token[Prefix.Length..], out var idWritten, "N") ||
                idWritten != PublicIdLength)
            {
                throw new InvalidOperationException("Could not format the public session ID.");
            }

            token[Prefix.Length + PublicIdLength] = '.';
            if (!Convert.TryToBase64Chars(secret, base64, out var base64Written) ||
                base64Written != base64.Length || base64[^1] != '=')
            {
                throw new InvalidOperationException("Could not encode the session secret.");
            }

            var encodedSecret = token[(Prefix.Length + PublicIdLength + 1)..];
            for (var index = 0; index < encodedSecret.Length; index++)
            {
                encodedSecret[index] = base64[index] switch
                {
                    '+' => '-',
                    '/' => '_',
                    var value => value
                };
            }

            var version = keys.CurrentSessionKeyVersion;
            verifier = ComputeVerifier(publicId, secret, version);
            return new IssuedSessionToken
            {
                PublicId = publicId,
                Verifier = verifier,
                KeyVersion = version,
                RawToken = new SensitiveValue(token)
            };
        }
        catch
        {
            if (verifier is not null)
            {
                CryptographicOperations.ZeroMemory(verifier);
            }

            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(publicBytes);
            CryptographicOperations.ZeroMemory(secret);
            base64.Clear();
            token.Clear();
        }
    }

    public bool TryRead(string token, out Guid publicId, out byte[] secret)
    {
        publicId = Guid.Empty;
        secret = Array.Empty<byte>();
        if (token is null || token.Length != TokenLength ||
            !token.AsSpan().StartsWith(Prefix, StringComparison.Ordinal) ||
            token[Prefix.Length + PublicIdLength] != '.' ||
            !Guid.TryParseExact(token.AsSpan(Prefix.Length, PublicIdLength), "N", out publicId))
        {
            return false;
        }

        Span<char> canonicalPublicId = stackalloc char[PublicIdLength];
        if (!publicId.TryFormat(canonicalPublicId, out var publicIdWritten, "N") ||
            publicIdWritten != PublicIdLength ||
            !token.AsSpan(Prefix.Length, PublicIdLength).SequenceEqual(canonicalPublicId))
        {
            publicId = Guid.Empty;
            return false;
        }

        var decoded = ArrayPool<byte>.Shared.Rent(SecretLength);
        try
        {
            if (!Base64Url.TryDecode(
                    token.AsSpan(Prefix.Length + PublicIdLength + 1),
                    decoded.AsSpan(0, SecretLength),
                    out var written) || written != SecretLength)
            {
                publicId = Guid.Empty;
                return false;
            }

            secret = decoded.AsSpan(0, SecretLength).ToArray();
            return true;
        }
        finally
        {
            canonicalPublicId.Clear();
            CryptographicOperations.ZeroMemory(decoded);
            ArrayPool<byte>.Shared.Return(decoded);
        }
    }

    public bool Verify(string token, Guid expectedPublicId, string keyVersion, ReadOnlySpan<byte> verifier)
    {
        if (!TryRead(token, out var publicId, out var secret) || publicId != expectedPublicId)
        {
            return false;
        }

        try
        {
            if (!keys.HasSessionKey(keyVersion) || verifier.Length != HMACSHA256.HashSizeInBytes)
            {
                return false;
            }

            var candidate = ComputeVerifier(publicId, secret, keyVersion);
            try
            {
                return CryptographicOperations.FixedTimeEquals(candidate, verifier);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(candidate);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private byte[] ComputeVerifier(Guid publicId, ReadOnlySpan<byte> secret, string keyVersion)
    {
        Span<byte> input = stackalloc byte[PurposePrefix.Length + 16 + SecretLength];
        PurposePrefix.CopyTo(input);
        if (!publicId.TryWriteBytes(input[PurposePrefix.Length..], bigEndian: true, out var written) || written != 16)
        {
            throw new InvalidOperationException("Could not encode the public session ID.");
        }

        secret.CopyTo(input[(PurposePrefix.Length + 16)..]);
        var key = keys.GetSessionKey(keyVersion);
        try
        {
            return HMACSHA256.HashData(key, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(input);
        }
    }
}
