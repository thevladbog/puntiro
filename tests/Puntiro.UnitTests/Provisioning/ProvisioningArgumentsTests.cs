using Puntiro.Provisioning.Cli;
using Xunit;

namespace Puntiro.UnitTests.Provisioning;

public sealed class ProvisioningArgumentsTests
{
    [Theory]
    [InlineData("--password")]
    [InlineData("--totp")]
    [InlineData("--recovery-code")]
    public void Secret_options_are_rejected(string option)
    {
        var result = ProvisioningArguments.Parse(["bootstrap-owner", option, "secret"]);

        Assert.Equal(ProvisioningExit.InvalidArguments, result.ExitCode);
        Assert.Null(result.Command);
        Assert.Empty(result.Arguments);
    }

    [Fact]
    public void Bootstrap_accepts_only_the_three_public_identifiers()
    {
        var result = ProvisioningArguments.Parse([
            "bootstrap-owner",
            "--organization-slug", " puntiro ",
            "--email", " Owner@Example.Test ",
            "--organization-name", " Puntiro Warehouse "
        ]);

        Assert.Equal(ProvisioningExit.Success, result.ExitCode);
        Assert.Equal("bootstrap-owner", result.Command);
        Assert.Equal(" puntiro ", result.Arguments["organization-slug"]);
        Assert.Equal(" Owner@Example.Test ", result.Arguments["email"]);
        Assert.Equal(" Puntiro Warehouse ", result.Arguments["organization-name"]);
    }

    [Fact]
    public void Recovery_accepts_only_slug_and_email()
    {
        var result = ProvisioningArguments.Parse([
            "reset-owner-totp",
            "--email", "owner@example.test",
            "--organization-slug", "puntiro"
        ]);

        Assert.Equal(ProvisioningExit.Success, result.ExitCode);
        Assert.Equal("reset-owner-totp", result.Command);
        Assert.Equal(2, result.Arguments.Count);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown")]
    [InlineData("bootstrap-owner", "--organization-name", "Puntiro")]
    [InlineData("reset-owner-totp", "--organization-slug", "puntiro")]
    [InlineData("reset-owner-totp", "--organization-slug", "puntiro", "--email")]
    [InlineData("reset-owner-totp", "--organization-slug", "puntiro", "--email", "owner@example.test", "--email", "other@example.test")]
    [InlineData("reset-owner-totp", "--organization-slug", "--email", "owner@example.test")]
    public void Missing_unknown_duplicate_or_option_shaped_values_are_rejected(params string[] arguments)
    {
        var result = ProvisioningArguments.Parse(arguments);

        Assert.Equal(ProvisioningExit.InvalidArguments, result.ExitCode);
        Assert.Null(result.Command);
        Assert.Empty(result.Arguments);
    }
}
