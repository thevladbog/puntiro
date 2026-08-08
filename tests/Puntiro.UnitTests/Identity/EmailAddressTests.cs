using System.Text;
using Puntiro.Modules.Identity.Security;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class EmailAddressTests
{
    [Fact]
    public void Normalize_preserves_trimmed_display_and_canonicalizes_identity()
    {
        var email = EmailAddress.Normalize("  Üser@BÜCHER.Example  ");

        Assert.Equal("Üser@BÜCHER.Example", email.Display);
        Assert.Equal("üser@xn--bcher-kva.example", email.Normalized);
    }

    [Fact]
    public void Normalize_uses_nfc_before_case_insensitive_comparison()
    {
        var decomposed = EmailAddress.Normalize("é@example.com");
        var composed = EmailAddress.Normalize("É@EXAMPLE.COM");

        Assert.Equal("é@example.com", decomposed.Normalized);
        Assert.Equal(decomposed.Normalized, composed.Normalized);
        Assert.NotEqual(decomposed.Display, composed.Display);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("missing-at.example")]
    [InlineData("@example.com")]
    [InlineData("owner@")]
    [InlineData("two@@example.com")]
    [InlineData("owner@bad_domain.example")]
    public void Normalize_rejects_malformed_addresses(string input)
    {
        Assert.Throws<ArgumentException>(() => EmailAddress.Normalize(input));
    }

    [Fact]
    public void Normalize_accepts_exactly_320_utf8_bytes()
    {
        var email = EmailAddress.Normalize($"{new string('a', 308)}@example.com");

        Assert.Equal(320, Encoding.UTF8.GetByteCount(email.Normalized));
    }

    [Fact]
    public void Normalize_rejects_more_than_320_utf8_bytes()
    {
        var input = $"{new string('a', 309)}@example.com";

        Assert.Throws<ArgumentException>(() => EmailAddress.Normalize(input));
    }

    [Fact]
    public void ToString_does_not_expose_the_email()
    {
        var email = EmailAddress.Normalize("owner@example.com");

        Assert.Equal(nameof(EmailAddress), email.ToString());
    }
}
