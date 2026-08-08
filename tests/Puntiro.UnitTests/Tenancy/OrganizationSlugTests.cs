using Puntiro.Modules.Tenancy.Domain;
using Xunit;

namespace Puntiro.UnitTests.Tenancy;

public sealed class OrganizationSlugTests
{
    [Theory]
    [InlineData("  Moscow--Warehouse  ", "moscow-warehouse")]
    [InlineData("A  B", "a-b")]
    [InlineData("already-canonical", "already-canonical")]
    public void Normalize_returns_canonical_slug(string input, string expected)
    {
        Assert.Equal(expected, OrganizationSlug.Normalize(input).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("---")]
    [InlineData("Moscow_")]
    [InlineData("склад")]
    public void Normalize_rejects_values_without_a_valid_ascii_slug(string input)
    {
        Assert.Throws<ArgumentException>(() => OrganizationSlug.Normalize(input));
    }
}
