using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Puntiro.Security;

namespace Puntiro.Modules.Identity.Security;

internal readonly record struct AcceptedTotp(long Counter);

internal sealed class Rfc6238Totp
{
    private const int SecretLength = 20;
    private const int PeriodSeconds = 30;
    private const int AuthenticationDigits = 6;

    private readonly TimeProvider _timeProvider;
    private readonly ISecretGenerator _secretGenerator;

    public Rfc6238Totp(TimeProvider timeProvider, ISecretGenerator secretGenerator)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(secretGenerator);
        _timeProvider = timeProvider;
        _secretGenerator = secretGenerator;
    }

    public byte[] GenerateSecret()
    {
        var secret = new byte[SecretLength];
        try
        {
            _secretGenerator.Fill(secret);
            return secret;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(secret);
            throw;
        }
    }

    public bool TryAccept(
        ReadOnlySpan<byte> secret,
        string code,
        long? lastAcceptedCounter,
        out AcceptedTotp accepted)
    {
        return TryAccept(secret, code, _timeProvider.GetUtcNow(), lastAcceptedCounter, out accepted);
    }

    public bool TryAccept(
        ReadOnlySpan<byte> secret,
        string code,
        DateTimeOffset utcNow,
        long? lastAcceptedCounter,
        out AcceptedTotp accepted)
    {
        accepted = default;
        if (secret.IsEmpty || !TryReadCode(code, out var presentedCode))
        {
            return false;
        }

        Span<byte> expectedCode = stackalloc byte[AuthenticationDigits];
        try
        {
            var unixSeconds = utcNow.ToUnixTimeSeconds();
            if (unixSeconds < 0)
            {
                return false;
            }

            var currentCounter = unixSeconds / PeriodSeconds;
            Span<long> candidates = stackalloc long[3];
            candidates[0] = currentCounter;
            candidates[1] = currentCounter > 0 ? currentCounter - 1 : -1;
            candidates[2] = currentCounter + 1;

            foreach (var counter in candidates)
            {
                if (counter < 0)
                {
                    continue;
                }

                WriteCode(GenerateValue(secret, counter, AuthenticationDigits), expectedCode);
                if (!CryptographicOperations.FixedTimeEquals(presentedCode, expectedCode))
                {
                    continue;
                }

                if (lastAcceptedCounter.HasValue && counter <= lastAcceptedCounter.Value)
                {
                    return false;
                }

                accepted = new AcceptedTotp(counter);
                return true;
            }

            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presentedCode);
            CryptographicOperations.ZeroMemory(expectedCode);
        }
    }

    public static string Generate(ReadOnlySpan<byte> secret, long unixSeconds, int digits = AuthenticationDigits)
    {
        if (secret.IsEmpty)
        {
            throw new ArgumentException("TOTP secret cannot be empty.", nameof(secret));
        }

        if (unixSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unixSeconds));
        }

        if (digits is < 6 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(digits));
        }

        var value = GenerateValue(secret, unixSeconds / PeriodSeconds, digits);
        return value.ToString($"D{digits}", CultureInfo.InvariantCulture);
    }

    private static int GenerateValue(ReadOnlySpan<byte> secret, long counter, int digits)
    {
        Span<byte> counterBytes = stackalloc byte[sizeof(long)];
        Span<byte> digest = stackalloc byte[20];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        HMACSHA1.HashData(secret, counterBytes, digest);

        try
        {
            var offset = digest[^1] & 0x0f;
            var binaryCode =
                ((digest[offset] & 0x7f) << 24) |
                (digest[offset + 1] << 16) |
                (digest[offset + 2] << 8) |
                digest[offset + 3];
            var divisor = digits switch
            {
                6 => 1_000_000,
                7 => 10_000_000,
                8 => 100_000_000,
                _ => throw new ArgumentOutOfRangeException(nameof(digits)),
            };

            return binaryCode % divisor;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static bool TryReadCode(string code, out byte[] digits)
    {
        digits = Array.Empty<byte>();
        if (code is null || code.Length != AuthenticationDigits)
        {
            return false;
        }

        var result = new byte[AuthenticationDigits];
        for (var index = 0; index < code.Length; index++)
        {
            if (code[index] is < '0' or > '9')
            {
                CryptographicOperations.ZeroMemory(result);
                return false;
            }

            result[index] = (byte)code[index];
        }

        digits = result;
        return true;
    }

    private static void WriteCode(int value, Span<byte> destination)
    {
        for (var index = destination.Length - 1; index >= 0; index--)
        {
            destination[index] = (byte)('0' + (value % 10));
            value /= 10;
        }
    }
}
