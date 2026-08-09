extern alias cloud;

using Microsoft.Extensions.Configuration;
using PuntiroCloudOptions = cloud::Puntiro.Cloud.Configuration.PuntiroCloudOptions;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class CloudEnvironmentBindingTests
{
    [Fact]
    public void Disabled_proxy_binds_no_allowlist_values_when_indexed_variables_are_absent()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Puntiro__Proxy__Enabled"] = "false"
        });

        Assert.False(options.Proxy.Enabled);
        Assert.Empty(options.Proxy.KnownProxies);
        Assert.Empty(options.Proxy.KnownNetworks);
    }

    [Theory]
    [MemberData(nameof(ProxyAllowlistCases))]
    public void Proxy_allowlist_binds_only_explicit_values(
        IReadOnlyDictionary<string, string?> values,
        string[] expectedProxies,
        string[] expectedNetworks)
    {
        var options = Bind(values);

        Assert.True(options.Proxy.Enabled);
        Assert.Equal(expectedProxies, options.Proxy.KnownProxies);
        Assert.Equal(expectedNetworks, options.Proxy.KnownNetworks);
        Assert.DoesNotContain(options.Proxy.KnownProxies, string.IsNullOrWhiteSpace);
        Assert.DoesNotContain(options.Proxy.KnownNetworks, string.IsNullOrWhiteSpace);
    }

    [Fact]
    public void Every_retained_HMAC_version_binds_without_an_enumerated_version_contract()
    {
        var values = new Dictionary<string, string?>();
        foreach (var purpose in new[] { "Session", "Recovery", "Integration" })
        {
            values[$"Puntiro__Security__{purpose}Hmac__CurrentVersion"] = "v2";
            values[$"Puntiro__Security__{purpose}Hmac__Keys__v0"] = $"{purpose}-old";
            values[$"Puntiro__Security__{purpose}Hmac__Keys__v1"] = $"{purpose}-middle";
            values[$"Puntiro__Security__{purpose}Hmac__Keys__v2"] = $"{purpose}-current";
        }

        var options = Bind(values);

        AssertRetained(options.Security.SessionHmac.Keys, "Session");
        AssertRetained(options.Security.RecoveryHmac.Keys, "Recovery");
        AssertRetained(options.Security.IntegrationHmac.Keys, "Integration");
    }

    public static TheoryData<IReadOnlyDictionary<string, string?>, string[], string[]> ProxyAllowlistCases =>
        new()
        {
            {
                new Dictionary<string, string?>
                {
                    ["Puntiro__Proxy__Enabled"] = "true",
                    ["Puntiro__Proxy__KnownProxies__0"] = "10.30.0.10"
                },
                ["10.30.0.10"],
                []
            },
            {
                new Dictionary<string, string?>
                {
                    ["Puntiro__Proxy__Enabled"] = "true",
                    ["Puntiro__Proxy__KnownNetworks__0"] = "10.30.0.0/24"
                },
                [],
                ["10.30.0.0/24"]
            },
            {
                new Dictionary<string, string?>
                {
                    ["Puntiro__Proxy__Enabled"] = "true",
                    ["Puntiro__Proxy__KnownProxies__0"] = "10.30.0.10",
                    ["Puntiro__Proxy__KnownNetworks__0"] = "10.31.0.0/24"
                },
                ["10.30.0.10"],
                ["10.31.0.0/24"]
            }
        };

    private static PuntiroCloudOptions Bind(IReadOnlyDictionary<string, string?> values)
    {
        var prefix = $"PUNTIRO_BIND_{Guid.NewGuid():N}__";
        try
        {
            foreach (var (name, value) in values)
            {
                Environment.SetEnvironmentVariable($"{prefix}{name}", value);
            }

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix)
                .Build();
            return configuration.GetSection(PuntiroCloudOptions.SectionName)
                .Get<PuntiroCloudOptions>() ?? new PuntiroCloudOptions();
        }
        finally
        {
            foreach (var name in values.Keys)
            {
                Environment.SetEnvironmentVariable($"{prefix}{name}", null);
            }
        }
    }

    private static void AssertRetained(IReadOnlyDictionary<string, string> keys, string purpose)
    {
        Assert.Equal(3, keys.Count);
        Assert.Equal($"{purpose}-old", keys["v0"]);
        Assert.Equal($"{purpose}-middle", keys["v1"]);
        Assert.Equal($"{purpose}-current", keys["v2"]);
    }
}
