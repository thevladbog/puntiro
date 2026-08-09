using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class TrustedProxyBoundaryTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Trusted_proxy_supplies_the_client_partition_before_login_rate_limiting()
    {
        using var production = factory.CreateProductionFactory(
            proxy: new ProxyTestSettings(
                IPAddress.Parse("10.30.0.10"),
                ["10.30.0.10"],
                []));
        using var client = production.CreateSecureClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await InvalidLoginAsync(client, $"trusted-a-{attempt}@example.test");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.11");
        using var nextClient = await InvalidLoginAsync(client, "trusted-b@example.test");

        Assert.Equal(HttpStatusCode.Unauthorized, nextClient.StatusCode);
    }

    [Fact]
    public async Task Forwarded_client_from_an_unknown_peer_is_ignored()
    {
        using var production = factory.CreateProductionFactory(
            proxy: new ProxyTestSettings(
                IPAddress.Parse("10.30.0.11"),
                ["10.30.0.10"],
                []));
        using var client = production.CreateSecureClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await InvalidLoginAsync(client, $"unknown-a-{attempt}@example.test");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.11");
        using var spoofed = await InvalidLoginAsync(client, "unknown-b@example.test");

        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
        Assert.Equal("60", Assert.Single(spoofed.Headers.GetValues("Retry-After")));
    }

    private static Task<HttpResponseMessage> InvalidLoginAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email,
            password = "not-the-password",
            totpCode = "000000"
        }, TestContext.Current.CancellationToken);
}

public sealed class TrustedProxyConfigurationTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public void Enabled_proxy_without_an_allowlist_fails_startup()
    {
        using var production = factory.CreateProductionFactory(
            proxy: new ProxyTestSettings(IPAddress.Loopback, [], []));

        var error = Assert.Throws<InvalidOperationException>(() => production.CreateSecureClient());

        Assert.Equal("Puntiro Cloud security configuration is invalid.", error.Message);
    }

    [Fact]
    public void Unrestricted_platform_forwarding_shortcut_fails_startup()
    {
        using var production = factory.CreateProductionFactory(
            proxy: new ProxyTestSettings(
                IPAddress.Loopback,
                [IPAddress.Loopback.ToString()],
                [],
                EnablePlatformShortcut: true));

        var error = Assert.Throws<InvalidOperationException>(() => production.CreateSecureClient());

        Assert.Equal("Puntiro Cloud security configuration is invalid.", error.Message);
    }

    [Fact]
    public void Network_only_allowlist_starts_with_the_explicit_trusted_boundary()
    {
        using var production = factory.CreateProductionFactory(
            proxy: new ProxyTestSettings(
                IPAddress.Parse("10.30.0.11"),
                [],
                ["10.30.0.0/24"]));

        using var client = production.CreateSecureClient();

        Assert.NotNull(client);
    }

    [Fact]
    public void Combined_proxy_and_network_allowlist_starts_with_only_explicit_values()
    {
        using var production = factory.CreateProductionFactory(
            proxy: new ProxyTestSettings(
                IPAddress.Parse("10.30.0.11"),
                ["10.30.0.10"],
                ["10.30.0.0/24"]));

        using var client = production.CreateSecureClient();

        Assert.NotNull(client);
    }
}
