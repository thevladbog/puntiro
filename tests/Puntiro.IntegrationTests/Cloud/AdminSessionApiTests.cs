using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class AdminSessionApiTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Session_issues_antiforgery_and_logout_requires_it_and_same_origin()
    {
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var session = await client.GetFromJsonAsync<AdminSessionResponse>(
            "/api/admin/auth/session",
            TestContext.Current.CancellationToken);

        Assert.NotNull(session);
        Assert.Equal(factory.OwnerUserId, session.UserId);
        Assert.Equal(factory.OrganizationId, session.OrganizationId);
        Assert.False(string.IsNullOrWhiteSpace(session.AntiforgeryToken));

        var missing = await client.PostAsync(
            "/api/admin/auth/logout",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/logout");
        request.Headers.Add("Origin", "https://localhost");
        request.Headers.Add("X-Puntiro-CSRF", session.AntiforgeryToken);
        var logout = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(
                "/api/admin/auth/session",
                TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Totp_step_up_updates_the_current_server_session()
    {
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var before = await client.GetFromJsonAsync<AdminSessionResponse>(
            "/api/admin/auth/session",
            TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/step-up")
        {
            Content = JsonContent.Create(new { totpCode = factory.NextTotp() })
        };
        request.Headers.Add("Origin", "https://localhost");
        request.Headers.Add("X-Puntiro-CSRF", before!.AntiforgeryToken);

        var stepUp = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var after = await client.GetFromJsonAsync<AdminSessionResponse>(
            "/api/admin/auth/session",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, stepUp.StatusCode);
        Assert.NotNull(after!.SecondFactorVerifiedAt);
        Assert.True(after.SecondFactorVerifiedAt >= before.SecondFactorVerifiedAt);
    }

    [Fact]
    public async Task Step_up_is_rate_limited_per_session_and_rejects_wrong_origin()
    {
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var session = await client.GetFromJsonAsync<AdminSessionResponse>(
            "/api/admin/auth/session",
            TestContext.Current.CancellationToken);
        using (var wrongOrigin = StepUpRequest(session!.AntiforgeryToken, "https://evil.example"))
        {
            var rejected = await client.SendAsync(wrongOrigin, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        }

        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var request = StepUpRequest(session.AntiforgeryToken, "https://localhost");
            response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
        Assert.Equal("60", Assert.Single(response.Headers.GetValues("Retry-After")));
        factory.Time.Advance(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task Session_expires_at_the_idle_boundary_and_duplicate_cookie_is_rejected()
    {
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        factory.Time.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(
                "/api/admin/auth/session",
                TestContext.Current.CancellationToken)).StatusCode);

        using var duplicateClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        var duplicateLogin = await duplicateClient.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.NextTotp()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, duplicateLogin.StatusCode);
        var validCookie = Assert.Single(duplicateLogin.Headers.GetValues("Set-Cookie"))
            .Split(';', 2)[0];
        using var duplicate = new HttpRequestMessage(HttpMethod.Get, "/api/admin/auth/session");
        duplicate.Headers.Add("Cookie", $"{validCookie}; {validCookie}");
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await duplicateClient.SendAsync(duplicate, TestContext.Current.CancellationToken)).StatusCode);
    }

    private async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.NextTotp()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static HttpRequestMessage StepUpRequest(string antiforgeryToken, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/step-up")
        {
            Content = JsonContent.Create(new { totpCode = "000000" })
        };
        request.Headers.Add("Origin", origin);
        request.Headers.Add("X-Puntiro-CSRF", antiforgeryToken);
        return request;
    }

    private sealed record AdminSessionResponse(
        Guid UserId,
        string Email,
        Guid OrganizationId,
        string Role,
        DateTimeOffset IdleExpiresAt,
        DateTimeOffset AbsoluteExpiresAt,
        DateTimeOffset? SecondFactorVerifiedAt,
        string AntiforgeryToken);
}

public sealed class AdminSessionMembershipApiTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Active_owner_membership_is_rechecked_before_session_endpoint_runs()
    {
        using var client = factory.CreateSecureClient();
        var login = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.CurrentTotp()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var tenancy = scope.ServiceProvider.GetRequiredService<Puntiro.Modules.Tenancy.Contracts.ITenancyProvisioningService>();
        await tenancy.SuspendAsync(
            factory.OrganizationId,
            new Puntiro.Modules.Tenancy.Contracts.TenancyAuditContext(
                factory.OwnerUserId,
                "test-membership-recheck"),
            TestContext.Current.CancellationToken);

        var response = await client.GetAsync(
            "/api/admin/auth/session",
            TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.forbidden", problem!.Code);
    }
}

public sealed class AdminSessionAbsoluteExpiryApiTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Session_is_rejected_at_the_absolute_expiry_boundary()
    {
        using var client = factory.CreateSecureClient();
        var login = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.CurrentTotp()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        factory.Time.Advance(TimeSpan.FromHours(12));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(
                "/api/admin/auth/session",
                TestContext.Current.CancellationToken)).StatusCode);
    }
}
