using System.Security.Cryptography;
using Puntiro.Modules.Identity.Security;
using Puntiro.Security;
using Puntiro.UnitTests.Security;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class SessionTokenTests
{
    private static readonly byte[] SessionKey = Enumerable.Range(1, 32)
        .Select(static value => (byte)value)
        .ToArray();

    [Fact]
    public void Issue_uses_pns_format_and_stores_only_a_purpose_bound_hmac()
    {
        var publicIdBytes = Guid.Parse("0191f7a2-3b8c-7def-8123-456789abcdef").ToByteArray();
        var secret = Enumerable.Range(100, 32).Select(static value => (byte)value).ToArray();
        var codec = new SessionTokenCodec(
            IdentityKeyOptions.ForTesting("session-v1", SessionKey, "recovery-v1", new byte[32]),
            new TestSecretGenerator(publicIdBytes, secret));

        using var issued = codec.Issue();
        var raw = issued.RawToken.Reveal();

        Assert.StartsWith("pns_0191f7a23b8c7def8123456789abcdef.", raw, StringComparison.Ordinal);
        Assert.Equal("session-v1", issued.KeyVersion);
        Assert.Equal(SHA256.HashSizeInBytes, issued.Verifier.Length);
        Assert.DoesNotContain(Convert.ToBase64String(secret), Convert.ToBase64String(issued.Verifier), StringComparison.Ordinal);
        Assert.True(codec.Verify(raw, issued.PublicId, issued.KeyVersion, issued.Verifier));
    }

    [Fact]
    public void Verify_rejects_a_changed_secret_unknown_key_and_noncanonical_token()
    {
        var codec = new SessionTokenCodec(
            IdentityKeyOptions.ForTesting("session-v1", SessionKey, "recovery-v1", new byte[32]),
            new SystemSecretGenerator());
        using var issued = codec.Issue();
        var raw = issued.RawToken.Reveal();
        var changed = raw[..^1] + (raw[^1] == 'A' ? "B" : "A");

        Assert.False(codec.Verify(changed, issued.PublicId, issued.KeyVersion, issued.Verifier));
        Assert.False(codec.Verify(raw, issued.PublicId, "retired", issued.Verifier));
        Assert.False(codec.TryRead(" pns_invalid.value", out _, out _));
    }

    [Fact]
    public void Identity_keys_reject_cross_purpose_key_reuse()
    {
        var shared = Enumerable.Repeat((byte)0x5a, 32).ToArray();

        Assert.Throws<ArgumentException>(() => IdentityKeyOptions.ForTesting(
            "session-v1",
            shared,
            "recovery-v1",
            shared));
    }
}
