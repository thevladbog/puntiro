using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class AdminRoutingApiTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Unknown_admin_route_returns_bounded_problem_json()
    {
        using var client = factory.CreateSecureClient();

        var response = await client.GetAsync(
            "/api/admin/not-a-route",
            TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("request.not_found", problem!.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task Wrong_admin_method_returns_problem_json_and_preserves_allow()
    {
        using var client = factory.CreateSecureClient();

        var response = await client.PostAsync(
            "/api/admin/auth/session",
            content: null,
            TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("request.method_not_allowed", problem!.Code);
        Assert.Contains("GET", response.Content.Headers.Allow);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }
}
