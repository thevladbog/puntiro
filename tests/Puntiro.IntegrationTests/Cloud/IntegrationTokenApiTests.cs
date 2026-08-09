using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Domain;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class IntegrationTokenApiTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Create_returns_raw_token_once_and_list_returns_only_safe_metadata()
    {
        using var client = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(client, factory);

        var created = await CloudIntegrationTestClient.CreateTokenAsync(
            client,
            csrf,
            "ERP",
            ["shipments.write"]);
        var list = await client.GetStringAsync(
            "/api/admin/integration-tokens",
            TestContext.Current.CancellationToken);

        Assert.StartsWith("pnt_live_", created.Token, StringComparison.Ordinal);
        Assert.DoesNotContain(created.Token, list, StringComparison.Ordinal);
        Assert.DoesNotContain("secretVerifier", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token\"", list, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(created.PublicId, list, StringComparison.Ordinal);
        Assert.DoesNotContain(
            created.Token,
            string.Join('\n', factory.Logs),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_mutations_require_exact_origin_and_antiforgery()
    {
        using var client = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(client, factory);

        var missingCsrf = await client.PostAsJsonAsync(
            "/api/admin/integration-tokens",
            new { displayName = "ERP", scopes = new[] { "shipments.write" } },
            TestContext.Current.CancellationToken);
        using var wrongOrigin = CloudIntegrationTestClient.CreateTokenRequest(
            csrf,
            "https://wrong.example",
            "ERP",
            ["shipments.write"]);
        var wrongOriginResponse = await client.SendAsync(
            wrongOrigin,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, missingCsrf.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongOriginResponse.StatusCode);
        Assert.Equal(
            "auth.csrf_invalid",
            (await missingCsrf.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken))!.Code);
    }
}

public sealed class IntegrationTokenApiStepUpTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Create_requires_a_current_totp_step_up_within_five_minutes()
    {
        using var client = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginWithRecoveryAndGetCsrfAsync(client, factory);
        using var missingStepUp = CloudIntegrationTestClient.CreateTokenRequest(
            csrf,
            "https://localhost",
            "ERP",
            ["shipments.write"]);

        var missing = await client.SendAsync(missingStepUp, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(
            "auth.step_up_required",
            (await missing.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken))!.Code);

        using var stepUp = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/step-up")
        {
            Content = JsonContent.Create(new { totpCode = factory.CurrentTotp() })
        };
        stepUp.Headers.Add("X-Puntiro-CSRF", csrf);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.SendAsync(stepUp, TestContext.Current.CancellationToken)).StatusCode);
        csrf = (await CloudIntegrationTestClient.GetSessionAsync(client)).AntiforgeryToken;

        var issued = await CloudIntegrationTestClient.CreateTokenAsync(
            client,
            csrf,
            "ERP",
            ["shipments.write"]);
        Assert.StartsWith("pnt_live_", issued.Token, StringComparison.Ordinal);

        factory.Time.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromTicks(1)));
        using var stale = CloudIntegrationTestClient.CreateTokenRequest(
            csrf,
            "https://localhost",
            "Second",
            ["shipments.read"]);
        var staleResponse = await client.SendAsync(stale, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, staleResponse.StatusCode);
        Assert.Equal(
            "auth.step_up_required",
            (await staleResponse.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken))!.Code);
    }
}

public sealed class IntegrationTokenApiLimitTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Third_active_token_returns_the_stable_limit_problem()
    {
        using var client = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(client, factory);
        _ = await CloudIntegrationTestClient.CreateTokenAsync(client, csrf, "First", ["shipments.read"]);
        _ = await CloudIntegrationTestClient.CreateTokenAsync(client, csrf, "Second", ["shipments.write"]);
        using var third = CloudIntegrationTestClient.CreateTokenRequest(
            csrf,
            "https://localhost",
            "Third",
            ["shipments.read"]);

        var response = await client.SendAsync(third, TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("integration_token.active_limit", problem!.Code);
    }
}

public sealed class IntegrationTokenApiRateLimitTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Admin_token_routes_return_the_documented_rate_limit_problem()
    {
        using var client = factory.CreateSecureClient();
        _ = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(client, factory);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.40");

        for (var attempt = 0; attempt < 120; attempt++)
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync(
                    "/api/admin/integration-tokens",
                    TestContext.Current.CancellationToken)).StatusCode);
        }

        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.41");
        var limited = await client.GetAsync(
            "/api/admin/integration-tokens",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("60", Assert.Single(limited.Headers.GetValues("Retry-After")));
        Assert.Equal(
            "auth.rate_limited",
            (await limited.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken))!.Code);
    }
}

public sealed class IntegrationTokenApiRevokeTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Revoke_is_optimistic_idempotent_and_tenant_concealed()
    {
        using var client = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(client, factory);
        var issued = await CloudIntegrationTestClient.CreateTokenAsync(
            client,
            csrf,
            "ERP",
            ["shipments.write"]);
        var metadata = await client.GetFromJsonAsync<CloudIntegrationTokenMetadata[]>(
            "/api/admin/integration-tokens",
            TestContext.Current.CancellationToken);
        var version = Assert.Single(metadata!).Version;

        var conflict = await CloudIntegrationTestClient.RevokeAsync(
            client,
            csrf,
            issued.Id,
            version + 1);
        var missing = await CloudIntegrationTestClient.RevokeAsync(
            client,
            csrf,
            Guid.CreateVersion7(),
            1);
        var revoked = await CloudIntegrationTestClient.RevokeAsync(
            client,
            csrf,
            issued.Id,
            version);
        var repeated = await CloudIntegrationTestClient.RevokeAsync(
            client,
            csrf,
            issued.Id,
            version);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(
            "integration_token.version_conflict",
            (await conflict.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken))!.Code);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(
            "integration_token.not_found",
            (await missing.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken))!.Code);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
    }

    [Fact]
    public async Task Foreign_and_missing_token_ids_return_the_same_problem()
    {
        using var client = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(client, factory);
        var foreignOrganization = Guid.CreateVersion7();
        Guid foreignTokenId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            using var issued = await scope.ServiceProvider
                .GetRequiredService<IIntegrationTokenService>()
                .CreateAsync(
                    new CreateIntegrationToken(
                        foreignOrganization,
                        factory.OwnerUserId,
                        "Foreign",
                        new HashSet<IntegrationScope> { IntegrationScope.ShipmentsWrite }),
                    TestContext.Current.CancellationToken);
            foreignTokenId = issued.Metadata.Id;
        }

        var foreign = await CloudIntegrationTestClient.RevokeAsync(
            client,
            csrf,
            foreignTokenId,
            1);
        var missing = await CloudIntegrationTestClient.RevokeAsync(
            client,
            csrf,
            Guid.CreateVersion7(),
            1);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var foreignProblem = await foreign.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);
        var missingProblem = await missing.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);
        Assert.Equal(missingProblem!.Code, foreignProblem!.Code);
        Assert.Equal(missingProblem.Title, foreignProblem.Title);
    }
}

internal static class CloudIntegrationTestClient
{
    internal static async Task<string> LoginAndGetCsrfAsync(
        HttpClient client,
        CloudWebApplicationFactory factory)
    {
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.NextTotp()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return (await GetSessionAsync(client)).AntiforgeryToken;
    }

    internal static async Task<string> LoginWithRecoveryAndGetCsrfAsync(
        HttpClient client,
        CloudWebApplicationFactory factory)
    {
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            recoveryCode = factory.RecoveryCode
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return (await GetSessionAsync(client)).AntiforgeryToken;
    }

    internal static async Task<CloudAdminSessionResponse> GetSessionAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<CloudAdminSessionResponse>(
            "/api/admin/auth/session",
            TestContext.Current.CancellationToken))!;

    internal static async Task<CloudIssuedIntegrationToken> CreateTokenAsync(
        HttpClient client,
        string csrf,
        string displayName,
        string[] scopes)
    {
        using var request = CreateTokenRequest(
            csrf,
            "https://localhost",
            displayName,
            scopes);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CloudIssuedIntegrationToken>(
            TestContext.Current.CancellationToken))!;
    }

    internal static HttpRequestMessage CreateTokenRequest(
        string csrf,
        string origin,
        string displayName,
        string[] scopes)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/integration-tokens")
        {
            Content = JsonContent.Create(new { displayName, scopes })
        };
        request.Headers.Add("Origin", origin);
        request.Headers.Add("X-Puntiro-CSRF", csrf);
        return request;
    }

    internal static Task<HttpResponseMessage> RevokeAsync(
        HttpClient client,
        string csrf,
        Guid tokenId,
        long expectedVersion)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/integration-tokens/{tokenId:D}/revoke")
        {
            Content = JsonContent.Create(new { expectedVersion, reason = "manual rotation" })
        };
        request.Headers.Add("Origin", "https://localhost");
        request.Headers.Add("X-Puntiro-CSRF", csrf);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}

internal sealed record CloudAdminSessionResponse(string AntiforgeryToken);

internal sealed record CloudIssuedIntegrationToken(
    Guid Id,
    string PublicId,
    string DisplayName,
    string[] Scopes,
    DateTimeOffset CreatedAt,
    string Token)
{
    public override string ToString() => nameof(CloudIssuedIntegrationToken);
}

internal sealed record CloudIntegrationTokenMetadata(
    Guid Id,
    string PublicId,
    string DisplayName,
    string[] Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    long Version);
