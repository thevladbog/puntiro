using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Puntiro.Modules.Integrations.Security;
using Puntiro.Security;
using Puntiro.UnitTests.Security;
using Xunit;

namespace Puntiro.UnitTests.Integrations;

public sealed class IntegrationTokenCodecTests
{
    private static readonly byte[] PublicIdBytes = Enumerable.Range(1, 16)
        .Select(static value => (byte)value)
        .ToArray();

    private static readonly byte[] Secret = Enumerable.Range(101, 32)
        .Select(static value => (byte)value)
        .ToArray();

    private static readonly byte[] Key = Enumerable.Repeat((byte)0x4b, 32).ToArray();

    [Fact]
    public void Issue_uses_the_canonical_format_and_purpose_bound_hmac()
    {
        var codec = CreateCodec();

        using var issued = codec.Issue();
        var raw = issued.RawToken.Reveal();
        var expectedPublicId = Base64Url.Encode(PublicIdBytes);

        AssertSensitiveTextEqual(
            $"pnt_live_{expectedPublicId}.{Base64Url.Encode(Secret)}",
            raw);
        Assert.Equal(expectedPublicId, issued.PublicId);
        Assert.Equal("integration-v1", issued.KeyVersion);
        Assert.Equal(32, issued.SecretVerifier.Length);

        var purpose = Encoding.ASCII.GetBytes("Puntiro.Integrations.Token.v1\0");
        var publicId = Encoding.ASCII.GetBytes(expectedPublicId);
        var input = new byte[purpose.Length + publicId.Length + Secret.Length];
        purpose.CopyTo(input, 0);
        publicId.CopyTo(input, purpose.Length);
        Secret.CopyTo(input, purpose.Length + publicId.Length);
        var expectedVerifier = HMACSHA256.HashData(Key, input);

        AssertSensitiveBytesEqual(expectedVerifier, issued.SecretVerifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("pnt_test_AAECAwQFBgcICQoLDA0ODw.ZGVhZGJlZWY")]
    [InlineData("pnt_live_AAECAwQFBgcICQoLDA0ODw")]
    [InlineData("pnt_live_AAECAwQFBgcICQoLDA0ODw.ZGVhZGJlZWY=")]
    [InlineData("pnt_live_AAECAwQFBgcICQoLDA0OD!.ZGVhZGJlZWY")]
    [InlineData("pnt_live_AAECAwQFBgcICQoLDA0ODw.A")]
    public void TryRead_rejects_malformed_or_noncanonical_tokens(string token)
    {
        var codec = CreateCodec();

        Assert.False(codec.TryRead(token, out var publicId, out var parsedSecret));
        Assert.Equal(string.Empty, publicId);
        AssertNoParsedSecret(parsedSecret);
    }

    [Fact]
    public void Verify_fails_closed_for_wrong_secret_public_id_key_version_and_verifier_lengths()
    {
        var codec = CreateCodec();
        using var issued = codec.Issue();
        var raw = issued.RawToken.Reveal();
        var wrongSecret = raw[..^1] + (raw[^1] == 'A' ? "B" : "A");
        var wrongPublicId = Base64Url.Encode(Enumerable.Repeat((byte)0xcc, 16).ToArray());
        var wrongSameLengthVerifier = Enumerable.Repeat((byte)0x7f, 32).ToArray();

        Assert.True(codec.Verify(raw, issued.PublicId, issued.KeyVersion, issued.SecretVerifier));
        Assert.False(codec.Verify(wrongSecret, issued.PublicId, issued.KeyVersion, issued.SecretVerifier));
        Assert.False(codec.Verify(raw, wrongPublicId, issued.KeyVersion, issued.SecretVerifier));
        Assert.False(codec.Verify(raw, issued.PublicId, "retired-v0", issued.SecretVerifier));
        Assert.False(codec.Verify(raw, issued.PublicId, issued.KeyVersion, wrongSameLengthVerifier));
        Assert.False(codec.Verify(raw, issued.PublicId, issued.KeyVersion, issued.SecretVerifier[..31]));
        Assert.False(codec.Verify(raw, issued.PublicId, issued.KeyVersion, [.. issued.SecretVerifier, 0x00]));
    }

    [Fact]
    public void TryRead_returns_the_exact_bounded_secret_for_a_valid_token()
    {
        var codec = CreateCodec();
        using var issued = codec.Issue();

        Assert.True(codec.TryRead(issued.RawToken.Reveal(), out var publicId, out var parsedSecret));
        try
        {
            Assert.Equal(issued.PublicId, publicId);
            AssertSensitiveBytesEqual(Secret, parsedSecret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(parsedSecret);
        }
    }

    [Fact]
    public void TryRead_rejects_a_secret_with_noncanonical_padding_bits()
    {
        var codec = CreateCodec();
        using var issued = codec.Issue();
        var raw = issued.RawToken.Reveal();
        var noncanonicalLastCharacter = raw[^1] switch
        {
            'A' => 'B',
            'Q' => 'R',
            'g' => 'h',
            'w' => 'x',
            _ => throw new InvalidOperationException("The issued secret was not canonical Base64Url.")
        };
        var noncanonical = raw[..^1] + noncanonicalLastCharacter;

        Assert.False(codec.TryRead(noncanonical, out var publicId, out var parsedSecret));
        Assert.Equal(string.Empty, publicId);
        AssertNoParsedSecret(parsedSecret);
    }

    [Fact]
    public void Key_options_validate_versioned_32_byte_keys_without_serializing_key_material()
    {
        var options = new IntegrationKeyOptions(
            "integration-v1",
            new Dictionary<string, byte[]> { ["integration-v1"] = Key });
        var encodedKey = Convert.ToBase64String(Key);

        Assert.Equal(nameof(IntegrationKeyOptions), options.ToString());
        AssertSensitiveTextAbsent(encodedKey, JsonSerializer.Serialize(options));
        Assert.Throws<ArgumentException>(() => new IntegrationKeyOptions(
            "missing",
            new Dictionary<string, byte[]> { ["integration-v1"] = Key }));
        Assert.Throws<ArgumentException>(() => new IntegrationKeyOptions(
            "invalid version!",
            new Dictionary<string, byte[]> { ["invalid version!"] = Key }));
        Assert.Throws<ArgumentException>(() => new IntegrationKeyOptions(
            "integration-v1",
            new Dictionary<string, byte[]> { ["integration-v1"] = Key[..31] }));
    }

    private static IntegrationTokenCodec CreateCodec() =>
        new(
            IntegrationKeyOptions.ForTesting("integration-v1", Key),
            new TestSecretGenerator(PublicIdBytes, Secret));

    private static void AssertSensitiveTextEqual(string expected, string actual) =>
        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            "Sensitive text did not match the independently derived fixture.");

    private static void AssertSensitiveBytesEqual(
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual) =>
        Assert.True(
            CryptographicOperations.FixedTimeEquals(expected, actual),
            "Sensitive bytes did not match the independently derived fixture.");

    private static void AssertSensitiveTextAbsent(string sensitive, string candidate) =>
        Assert.False(
            candidate.Contains(sensitive, StringComparison.Ordinal),
            "A redacted representation exposed sensitive text.");

    private static void AssertNoParsedSecret(byte[] parsedSecret) =>
        Assert.True(
            parsedSecret.Length == 0,
            "A rejected credential returned parsed secret bytes.");
}
