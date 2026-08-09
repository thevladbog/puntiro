using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Puntiro.Security;

namespace Puntiro.Modules.Integrations.Security;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class IssuedIntegrationTokenMaterial : IDisposable
{
    public required string PublicId { get; init; }

    [JsonIgnore]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public required byte[] SecretVerifier { get; init; }

    public required string KeyVersion { get; init; }

    [JsonIgnore]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public required SensitiveValue RawToken { get; set; }

    public SensitiveValue TakeRawToken()
    {
        var value = RawToken;
        RawToken = new SensitiveValue(ReadOnlySpan<char>.Empty);
        return value;
    }

    public void Dispose()
    {
        RawToken.Dispose();
        CryptographicOperations.ZeroMemory(SecretVerifier);
    }

    public override string ToString() => nameof(IssuedIntegrationTokenMaterial);

    private string DebuggerDisplay => nameof(IssuedIntegrationTokenMaterial);
}

internal sealed class IntegrationTokenCodec(
    IntegrationKeyOptions keys,
    ISecretGenerator secretGenerator)
{
    private const string Prefix = "pnt_live_";
    private const int PrefixLength = 9;
    private const string Purpose = "Puntiro.Integrations.Token.v1";
    private const int PublicIdByteLength = 16;
    private const int PublicIdLength = 22;
    private const int SecretLength = 32;
    private const int EncodedSecretLength = 43;
    private const int TokenLength = PrefixLength + PublicIdLength + 1 + EncodedSecretLength;
    private static readonly byte[] PurposePrefix = Encoding.ASCII.GetBytes(Purpose + "\0");

    internal IssuedIntegrationTokenMaterial Issue()
    {
        Span<byte> publicIdBytes = stackalloc byte[PublicIdByteLength];
        Span<byte> secret = stackalloc byte[SecretLength];
        Span<char> token = stackalloc char[TokenLength];
        Span<char> publicBase64 = stackalloc char[24];
        Span<char> secretBase64 = stackalloc char[44];
        byte[]? verifier = null;
        try
        {
            secretGenerator.Fill(publicIdBytes);
            secretGenerator.Fill(secret);
            if (publicIdBytes.IndexOfAnyExcept((byte)0) < 0)
            {
                throw new InvalidOperationException("Secret generator produced an invalid public token ID.");
            }

            Prefix.AsSpan().CopyTo(token);
            if (!Convert.TryToBase64Chars(publicIdBytes, publicBase64, out var publicWritten) ||
                publicWritten != publicBase64.Length ||
                publicBase64[^2] != '=' ||
                publicBase64[^1] != '=')
            {
                throw new InvalidOperationException("Could not encode the public token ID.");
            }

            CopyBase64Url(
                publicBase64[..PublicIdLength],
                token.Slice(Prefix.Length, PublicIdLength));
            token[Prefix.Length + PublicIdLength] = '.';
            if (!Convert.TryToBase64Chars(secret, secretBase64, out var secretWritten) ||
                secretWritten != secretBase64.Length ||
                secretBase64[^1] != '=')
            {
                throw new InvalidOperationException("Could not encode the integration token secret.");
            }

            CopyBase64Url(
                secretBase64[..EncodedSecretLength],
                token[(Prefix.Length + PublicIdLength + 1)..]);
            var publicId = new string(token.Slice(Prefix.Length, PublicIdLength));
            var keyVersion = keys.CurrentIntegrationKeyVersion;
            verifier = ComputeVerifier(publicId, secret, keyVersion);
            return new IssuedIntegrationTokenMaterial
            {
                PublicId = publicId,
                SecretVerifier = verifier,
                KeyVersion = keyVersion,
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
            CryptographicOperations.ZeroMemory(publicIdBytes);
            CryptographicOperations.ZeroMemory(secret);
            publicBase64.Clear();
            secretBase64.Clear();
            token.Clear();
        }
    }

    internal bool TryRead(string? token, out string publicId, out byte[] secret)
    {
        publicId = string.Empty;
        secret = [];
        if (token is null || token.Length != TokenLength ||
            !token.AsSpan().StartsWith(Prefix, StringComparison.Ordinal) ||
            token[Prefix.Length + PublicIdLength] != '.')
        {
            return false;
        }

        Span<byte> decodedPublicId = stackalloc byte[PublicIdByteLength];
        var decodedSecret = ArrayPool<byte>.Shared.Rent(SecretLength);
        try
        {
            var publicIdSpan = token.AsSpan(Prefix.Length, PublicIdLength);
            if (!Base64Url.TryDecode(
                    publicIdSpan,
                    decodedPublicId,
                    out var publicBytesWritten) ||
                publicBytesWritten != PublicIdByteLength ||
                decodedPublicId.IndexOfAnyExcept((byte)0) < 0)
            {
                return false;
            }

            var canonicalPublicId = Base64Url.Encode(decodedPublicId);
            if (!publicIdSpan.SequenceEqual(canonicalPublicId))
            {
                return false;
            }

            if (!Base64Url.TryDecode(
                    token.AsSpan(Prefix.Length + PublicIdLength + 1),
                    decodedSecret.AsSpan(0, SecretLength),
                    out var secretBytesWritten) ||
                secretBytesWritten != SecretLength)
            {
                return false;
            }

            publicId = canonicalPublicId;
            secret = decodedSecret.AsSpan(0, SecretLength).ToArray();
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decodedPublicId);
            CryptographicOperations.ZeroMemory(decodedSecret);
            ArrayPool<byte>.Shared.Return(decodedSecret);
        }
    }

    internal bool Verify(
        string token,
        string expectedPublicId,
        string keyVersion,
        ReadOnlySpan<byte> verifier)
    {
        if (!TryRead(token, out var publicId, out var secret) ||
            !string.Equals(publicId, expectedPublicId, StringComparison.Ordinal))
        {
            CryptographicOperations.ZeroMemory(secret);
            return false;
        }

        try
        {
            if (verifier.Length != HMACSHA256.HashSizeInBytes || !keys.HasKey(keyVersion))
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

    private byte[] ComputeVerifier(
        string publicId,
        ReadOnlySpan<byte> secret,
        string keyVersion)
    {
        Span<byte> input = stackalloc byte[PurposePrefix.Length + PublicIdLength + SecretLength];
        PurposePrefix.CopyTo(input);
        var publicWritten = Encoding.ASCII.GetBytes(
            publicId.AsSpan(), input[PurposePrefix.Length..]);
        if (publicWritten != PublicIdLength)
        {
            throw new InvalidOperationException("Could not bind the public integration token ID.");
        }

        secret.CopyTo(input[(PurposePrefix.Length + PublicIdLength)..]);
        var key = keys.GetKey(keyVersion);
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

    private static void CopyBase64Url(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (source.Length != destination.Length)
        {
            throw new ArgumentException("Base64Url buffers have different lengths.", nameof(destination));
        }

        for (var index = 0; index < source.Length; index++)
        {
            destination[index] = source[index] switch
            {
                '+' => '-',
                '/' => '_',
                var value => value
            };
        }
    }
}
