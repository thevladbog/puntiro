using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Puntiro.Modules.Identity.Security;
using Puntiro.Security;
using Puntiro.UnitTests.Security;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class RecoveryCodeServiceTests
{
    private static readonly byte[] RecoveryKey = Enumerable.Range(32, 32).Select(static value => (byte)value).ToArray();

    [Fact]
    public void GenerateBatch_returns_ten_grouped_codes_and_only_hmac_verifiers()
    {
        using var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));

        var batch = service.GenerateBatch();

        Assert.Equal(10, batch.Count);
        Assert.Equal(10, batch.Select(static item => item.Code.Reveal()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(batch, item =>
        {
            Assert.Matches(
                new Regex("^[A-Z2-7]{4}(?:-[A-Z2-7]{4}){5}-[A-Z2-7]{2}$", RegexOptions.CultureInvariant),
                item.Code.Reveal());
            Assert.Equal(32, item.Verifier.Length);
            Assert.True(service.Verify(item.Code.Reveal(), item.Verifier));
            Assert.True(service.Verify(item.Code.Reveal().Replace("-", string.Empty, StringComparison.Ordinal), item.Verifier));
        });
    }

    [Fact]
    public void Verify_rejects_wrong_malformed_or_noncanonical_codes()
    {
        using var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));
        var generated = service.GenerateBatch()[0];

        Assert.False(service.Verify("AAAA-AAAA-AAAA-AAAA-AAAA-AAAA-AA", generated.Verifier));
        Assert.False(service.Verify(generated.Code.Reveal().ToLowerInvariant(), generated.Verifier));
        Assert.False(service.Verify($" {generated.Code.Reveal()}", generated.Verifier));
        Assert.False(service.Verify(generated.Code.Reveal(), new byte[31]));
    }

    [Fact]
    public void Verifier_is_bound_to_the_recovery_key_and_purpose()
    {
        using var issuer = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));
        using var otherKey = new RecoveryCodeService(
            Enumerable.Repeat((byte)0xa5, 32).ToArray(),
            new TestSecretGenerator());
        var generated = issuer.GenerateBatch()[0];
        var normalized = generated.Code.Reveal().Replace("-", string.Empty, StringComparison.Ordinal);
        var normalizedBytes = Encoding.ASCII.GetBytes(normalized);
        var plainHmac = HMACSHA256.HashData(RecoveryKey, normalizedBytes);

        try
        {
            Assert.False(otherKey.Verify(generated.Code.Reveal(), generated.Verifier));
            Assert.False(CryptographicOperations.FixedTimeEquals(plainHmac, generated.Verifier));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(normalizedBytes);
            CryptographicOperations.ZeroMemory(plainHmac);
        }
    }

    [Fact]
    public void Sensitive_values_and_generated_records_are_redacted()
    {
        var secret = new SensitiveValue("do-not-log-this-value");
        using var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));
        var generated = service.GenerateBatch()[0];

        Assert.Equal("[REDACTED]", secret.ToString());
        Assert.Equal("do-not-log-this-value", secret.Reveal());
        Assert.DoesNotContain(generated.Code.Reveal(), generated.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Disposed_service_fails_closed()
    {
        var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));
        var generated = service.GenerateBatch()[0];

        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.Verify(generated.Code.Reveal(), generated.Verifier));
        Assert.Throws<ObjectDisposedException>(() => service.GenerateBatch());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Base32_matches_rfc4648_unpadded_vectors(string input, string expected)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(input);

        Assert.Equal(expected, Base32.Encode(bytes));

        Span<byte> decoded = stackalloc byte[bytes.Length];
        Assert.True(Base32.TryDecode(expected, decoded, out var bytesWritten));
        Assert.Equal(bytes, decoded[..bytesWritten].ToArray());
    }

    [Theory]
    [InlineData("M=")]
    [InlineData("mY")]
    [InlineData("M1")]
    [InlineData("MZ")]
    public void Base32_rejects_malformed_or_noncanonical_encodings(string input)
    {
        Span<byte> decoded = stackalloc byte[32];

        Assert.False(Base32.TryDecode(input, decoded, out _));
    }

    [Fact]
    public void Base64Url_round_trips_without_padding_and_rejects_noncanonical_input()
    {
        byte[] bytes = [0xfb, 0xef, 0xff];
        var encoded = Base64Url.Encode(bytes);

        Assert.Equal("--__", encoded);
        Span<byte> decoded = stackalloc byte[3];
        Assert.True(Base64Url.TryDecode(encoded, decoded, out var bytesWritten));
        Assert.Equal(bytes, decoded[..bytesWritten].ToArray());
        Assert.False(Base64Url.TryDecode("Zh", decoded, out _));
        Assert.False(Base64Url.TryDecode("Zg==", decoded, out _));
    }

    private static byte[][] CreateRecoveryValues()
    {
        return Enumerable.Range(0, 10)
            .Select(index => Enumerable.Range(index * 16, 16).Select(static value => (byte)value).ToArray())
            .ToArray();
    }
}
