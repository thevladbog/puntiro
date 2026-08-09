using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Puntiro.Modules.Tenancy.Contracts;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class AdminAuthApiTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Login_requires_one_exact_configured_https_origin()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var client = factory.CreateSecureClient(includeOrigin: false);
        var payload = new
        {
            email = "missing@example.test",
            password = "not-the-password",
            totpCode = "000000"
        };

        var missing = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            payload,
            TestContext.Current.CancellationToken);
        using var wrongRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/login")
        {
            Content = JsonContent.Create(payload)
        };
        wrongRequest.Headers.Add("Origin", "https://wrong.example");
        var wrong = await client.SendAsync(wrongRequest, TestContext.Current.CancellationToken);
        using var multipleRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/login")
        {
            Content = JsonContent.Create(payload)
        };
        multipleRequest.Headers.TryAddWithoutValidation(
            "Origin",
            ["https://localhost", "https://wrong.example"]);
        var multiple = await client.SendAsync(multipleRequest, TestContext.Current.CancellationToken);

        foreach (var response in new[] { missing, wrong, multiple })
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("auth.csrf_invalid", problem!.Code);
        }
    }

    [Fact]
    public async Task Login_sets_only_the_approved_cookie_and_keeps_failures_generic()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var client = factory.CreateSecureClient();
        var success = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.CurrentTotp()
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);
        var cookie = Assert.Single(success.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-puntiro_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);

        var unknown = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = "missing@example.test",
            password = "not-the-password",
            totpCode = "000000"
        }, TestContext.Current.CancellationToken);
        var wrong = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = "not-the-password",
            totpCode = "000000"
        }, TestContext.Current.CancellationToken);
        var unknownProblem = await unknown.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);
        var wrongProblem = await wrong.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.Unauthorized, "auth.invalid_credentials"),
            (unknown.StatusCode, unknownProblem!.Code));
        Assert.Equal(
            (unknownProblem.Status, unknownProblem.Code, unknownProblem.Title),
            (wrongProblem!.Status, wrongProblem.Code, wrongProblem.Title));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("000000", "RECOVERY")]
    public async Task Login_rejects_any_request_without_exactly_one_factor(
        string? totpCode,
        string? recoveryCode)
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var client = factory.CreateSecureClient();
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode,
            recoveryCode
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recovery_login_has_no_fresh_totp_and_multiple_memberships_require_selection()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var recoveryClient = factory.CreateSecureClient();
        var recoveryLogin = await recoveryClient.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            recoveryCode = factory.RecoveryCode
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, recoveryLogin.StatusCode);
        var sessionJson = await recoveryClient.GetStringAsync(
            "/api/admin/auth/session",
            TestContext.Current.CancellationToken);
        Assert.Contains("\"secondFactorVerifiedAt\":null", sessionJson, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var tenancy = scope.ServiceProvider.GetRequiredService<ITenancyProvisioningService>();
        var second = await tenancy.GetOrCreateProvisioningAsync(
            "Second Organization",
            $"second-{Guid.NewGuid():N}",
            TestContext.Current.CancellationToken);
        await tenancy.EnsureOwnerMembershipAsync(
            second.Id,
            factory.OwnerUserId,
            TestContext.Current.CancellationToken);
        await tenancy.ActivateAsync(
            second.Id,
            new TenancyAuditContext(factory.OwnerUserId, "test-second-activate"),
            TestContext.Current.CancellationToken);

        using var ambiguousClient = factory.CreateSecureClient();
        var ambiguous = await ambiguousClient.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.NextTotp()
        }, TestContext.Current.CancellationToken);
        var problem = await ambiguous.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, ambiguous.StatusCode);
        Assert.Equal("auth.organization_selection_required", problem!.Code);
        await tenancy.SuspendAsync(
            second.Id,
            new TenancyAuditContext(factory.OwnerUserId, "test-second-suspend"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Login_is_bounded_by_ip_and_returns_deterministic_retry_after()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var client = factory.CreateSecureClient();
        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            response = await client.PostAsJsonAsync("/api/admin/auth/login", new
            {
                email = $"missing-{attempt}@example.test",
                password = "not-the-password",
                totpCode = "000000"
            }, TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
        Assert.Equal("60", Assert.Single(response.Headers.GetValues("Retry-After")));
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);
        Assert.Equal("auth.rate_limited", problem!.Code);
        factory.Time.Advance(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task Login_rejects_oversized_json_before_authentication_work()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var client = factory.CreateSecureClient();
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = new string('x', 20_000),
            totpCode = "000000"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_oversized_headers_before_authentication_work()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var client = factory.CreateSecureClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = factory.OwnerEmail,
                password = factory.OwnerPassword,
                totpCode = factory.CurrentTotp()
            })
        };
        request.Headers.Add("X-Oversized", new string('x', 40_000));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_accepts_only_the_exact_json_media_type()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var client = factory.CreateSecureClient();
        using var content = JsonContent.Create(new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.CurrentTotp()
        });
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/json-patch+json");

        var response = await client.PostAsync(
            "/api/admin/auth/login",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_explicit_null_required_fields_without_an_exception()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var client = factory.CreateSecureClient();
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = (string?)null,
            password = (string?)null,
            totpCode = "000000"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
