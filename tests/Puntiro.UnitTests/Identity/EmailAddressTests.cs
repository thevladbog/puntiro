using System.Text;
using Puntiro.Modules.Identity.Security;
using Puntiro.Modules.Identity.Contracts;
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
    [InlineData("Σ", "ς", "σ@example.com")]
    [InlineData("S", "ſ", "s@example.com")]
    [InlineData("K", "K", "k@example.com")]
    public void Normalize_uses_stable_simple_case_fold_for_local_part(
        string firstLocalPart,
        string secondLocalPart,
        string expected)
    {
        var first = EmailAddress.Normalize($"{firstLocalPart}@example.com");
        var second = EmailAddress.Normalize($"{secondLocalPart}@example.com");

        Assert.Equal(expected, first.Normalized);
        Assert.Equal(expected, second.Normalized);
    }

    [Fact]
    public void Normalize_does_not_collapse_local_parts_outside_invariant_simple_fold()
    {
        var latinI = EmailAddress.Normalize("I@example.com");
        var dotlessI = EmailAddress.Normalize("ı@example.com");

        Assert.False(string.Equals("I", "ı", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(latinI.Normalized, dotlessI.Normalized);
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

    [Fact]
    public void Rate_limit_partition_uses_the_exact_identity_normalization_policy()
    {
        Assert.Equal(
            AdminEmailPartition.Normalize(" Üser@BÜCHER.Example "),
            AdminEmailPartition.Normalize("üSER@xn--bcher-kva.example"));
        Assert.Equal("<invalid>", AdminEmailPartition.Normalize("not-an-email"));
    }
}
