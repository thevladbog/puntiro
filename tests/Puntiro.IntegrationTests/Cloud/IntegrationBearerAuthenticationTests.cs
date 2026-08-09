using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Puntiro.Modules.Tenancy.Contracts;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class IntegrationBearerAuthenticationTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Bearer_derives_tenant_and_enforces_the_exact_scope_without_cookie_fallback()
    {
        using var admin = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(admin, factory);
        var token = await CloudIntegrationTestClient.CreateTokenAsync(
            admin,
            csrf,
            "Reader",
            ["shipments.read"]);
        using var integration = factory.CreateSecureClient();
        integration.DefaultRequestHeaders.Remove("Origin");
        integration.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        integration.DefaultRequestHeaders.Add("X-Puntiro-Organization", Guid.CreateVersion7().ToString("D"));

        var read = await integration.GetFromJsonAsync<IntegrationProbeResponse>(
            "/api/v1/test/shipments/read?organizationId=" + Guid.CreateVersion7().ToString("D"),
            TestContext.Current.CancellationToken);
        var write = await integration.PostAsJsonAsync(
            "/api/v1/test/shipments/write",
            new { organizationId = Guid.CreateVersion7() },
            TestContext.Current.CancellationToken);
        using var cookieOnly = factory.CreateSecureClient();
        _ = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(cookieOnly, factory);
        var withoutBearer = await cookieOnly.GetAsync(
            "/api/v1/test/shipments/read",
            TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal(factory.OrganizationId, read.OrganizationId);
        Assert.Equal(token.Id, read.TokenId);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(
            "integration.scope_forbidden",
            (await write.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken))!.Code);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutBearer.StatusCode);
    }
}

public sealed class IntegrationBearerMalformedTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Missing_malformed_duplicate_and_unknown_credentials_are_generic_unauthorized()
    {
        using var client = factory.CreateClient(CloudWebApplicationFactory.SecureClientOptions());
        var requests = new[]
        {
            Request(),
            Request("Basic anything"),
            Request("Bearer  malformed"),
            Request("Bearer " + new string('x', 1024)),
            Request("Bearer " + UnknownToken('A'))
        };

        string? firstProblemCode = null;
        foreach (var request in requests)
        {
            using (request)
            {
                var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
                    TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                firstProblemCode ??= problem!.Code;
                Assert.Equal(firstProblemCode, problem!.Code);
            }
        }

        using var duplicate = Request();
        duplicate.Headers.TryAddWithoutValidation("Authorization", new[]
        {
            "Bearer " + UnknownToken('A'),
            "Bearer " + UnknownToken('B')
        });
        var duplicateResponse = await client.SendAsync(
            duplicate,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, duplicateResponse.StatusCode);
    }

    private static HttpRequestMessage Request(string? authorization = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/shipments/read");
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        return request;
    }

    private static string UnknownToken(char value) =>
        "pnt_" + "live_" + new string(value, 22) + "." + new string(value, 43);
}

public sealed class IntegrationBearerRevocationTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Revocation_and_organization_suspension_are_rechecked_immediately()
    {
        using var admin = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(admin, factory);
        var token = await CloudIntegrationTestClient.CreateTokenAsync(
            admin,
            csrf,
            "Writer",
            ["shipments.write"]);
        using var integration = factory.CreateClient(CloudWebApplicationFactory.SecureClientOptions());
        integration.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await integration.PostAsJsonAsync(
                "/api/v1/test/shipments/write",
                new { organizationId = Guid.CreateVersion7() },
                TestContext.Current.CancellationToken)).StatusCode);
        var metadata = await admin.GetFromJsonAsync<CloudIntegrationTokenMetadata[]>(
            "/api/admin/integration-tokens",
            TestContext.Current.CancellationToken);
        var revoked = await CloudIntegrationTestClient.RevokeAsync(
            admin,
            csrf,
            token.Id,
            Assert.Single(metadata!).Version);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await integration.PostAsJsonAsync(
                "/api/v1/test/shipments/write",
                new { },
                TestContext.Current.CancellationToken)).StatusCode);
    }

}

public sealed class IntegrationBearerInactiveOrganizationTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Inactive_organization_is_concealed_as_invalid_bearer()
    {
        using var admin = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(admin, factory);
        var token = await CloudIntegrationTestClient.CreateTokenAsync(
            admin,
            csrf,
            "Writer",
            ["shipments.write"]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITenancyProvisioningService>()
                .SuspendAsync(
                    factory.OrganizationId,
                    new TenancyAuditContext(factory.OwnerUserId, "test-bearer-suspend"),
                    TestContext.Current.CancellationToken);
        }

        using var integration = factory.CreateClient(CloudWebApplicationFactory.SecureClientOptions());
        integration.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        var response = await integration.PostAsJsonAsync(
            "/api/v1/test/shipments/write",
            new { },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "integration.invalid_credentials",
            (await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken))!.Code);
    }
}

public sealed class IntegrationBearerRateLimitTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Verified_token_and_direct_ip_partition_is_limited_to_120_per_minute()
    {
        using var admin = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(admin, factory);
        var token = await CloudIntegrationTestClient.CreateTokenAsync(
            admin,
            csrf,
            "Reader",
            ["shipments.read"]);
        using var integration = factory.CreateClient(CloudWebApplicationFactory.SecureClientOptions());
        integration.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        integration.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");

        for (var attempt = 0; attempt < 120; attempt++)
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await integration.GetAsync(
                    "/api/v1/test/shipments/read",
                    TestContext.Current.CancellationToken)).StatusCode);
        }

        integration.DefaultRequestHeaders.Remove("X-Forwarded-For");
        integration.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.11");
        var limited = await integration.GetAsync(
            "/api/v1/test/shipments/read",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("60", Assert.Single(limited.Headers.GetValues("Retry-After")));
        Assert.Equal(
            "integration.rate_limited",
            (await limited.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken))!.Code);
    }
}

public sealed class IntegrationBearerUnknownRateLimitTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Unknown_credentials_share_a_direct_ip_only_partition()
    {
        using var client = factory.CreateClient(CloudWebApplicationFactory.SecureClientOptions());
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Authorization",
            "Bearer " + IntegrationBearerTestToken.UnknownCanonical());
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");

        for (var attempt = 0; attempt < 120; attempt++)
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await client.GetAsync(
                    "/api/v1/test/shipments/read",
                    TestContext.Current.CancellationToken)).StatusCode);
        }

        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer malformed");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.11");
        var limited = await client.GetAsync(
            "/api/v1/test/shipments/read",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("60", Assert.Single(limited.Headers.GetValues("Retry-After")));
    }

}

public sealed class IntegrationBearerPreAuthenticationRateLimitTests(
    CloudWebApplicationFactory factory) : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Exhausted_direct_peer_gate_stops_database_auth_for_concurrent_burst()
    {
        var authenticationCalls = new IntegrationAuthenticationCallCounter();
        using var production = factory.CreateProductionFactory(
            includeIntegrationTestProbes: true,
            integrationAuthenticationCalls: authenticationCalls);
        using var client = production.CreateClient(
            CloudWebApplicationFactory.SecureClientOptions());
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Authorization",
            "Bearer " + IntegrationBearerTestToken.UnknownCanonical());

        var boundaryBurst = await Task.WhenAll(Enumerable.Range(0, 160).Select(_ => client.GetAsync(
            "/api/v1/test/shipments/read",
            TestContext.Current.CancellationToken)));
        Assert.Equal(120, boundaryBurst.Count(response =>
            response.StatusCode == HttpStatusCode.Unauthorized));
        Assert.Equal(40, boundaryBurst.Count(response =>
            response.StatusCode == HttpStatusCode.TooManyRequests));
        Assert.Equal(120, authenticationCalls.Count);

        var beforeExhaustedRequests = authenticationCalls.Count;
        using var firstLimited = await client.GetAsync(
            "/api/v1/test/shipments/read",
            TestContext.Current.CancellationToken);
        var burst = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => client.GetAsync(
            "/api/v1/test/shipments/read",
            TestContext.Current.CancellationToken)));

        Assert.Equal(HttpStatusCode.TooManyRequests, firstLimited.StatusCode);
        Assert.Equal("60", Assert.Single(firstLimited.Headers.GetValues("Retry-After")));
        Assert.All(burst, response => Assert.Equal(
            HttpStatusCode.TooManyRequests,
            response.StatusCode));
        Assert.Equal(beforeExhaustedRequests, authenticationCalls.Count);
        foreach (var response in boundaryBurst.Concat(burst))
        {
            response.Dispose();
        }
    }
}

internal static class IntegrationBearerTestToken
{
    internal static string UnknownCanonical() =>
        "pnt_" + "live_" + Base64Url(Enumerable.Repeat((byte)0x01, 16).ToArray()) + "." +
        Base64Url(Enumerable.Repeat((byte)0x02, 32).ToArray());

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

internal sealed record IntegrationProbeResponse(Guid TokenId, Guid OrganizationId);
