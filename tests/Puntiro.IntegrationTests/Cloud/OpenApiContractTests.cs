using System.Net;
using System.Text.Json;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class OpenApiContractTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task V1_documents_cookie_auth_problem_responses_and_no_secret_examples()
    {
        using var client = factory.CreateSecureClient();
        var response = await client.GetAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("components")
            .GetProperty("securitySchemes")
            .TryGetProperty("AdminSession", out var scheme));
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("cookie", scheme.GetProperty("in").GetString());
        Assert.Equal("__Host-puntiro_session", scheme.GetProperty("name").GetString());
        Assert.Contains("application/problem+json", json, StringComparison.Ordinal);
        Assert.Contains("\"code\"", json, StringComparison.Ordinal);
        Assert.Contains("\"traceId\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"example\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "AdminAuthLogin",
            document.RootElement.GetProperty("paths")
                .GetProperty("/api/admin/auth/login")
                .GetProperty("post")
                .GetProperty("operationId")
                .GetString());
        Assert.Equal(
            "AdminSession",
            document.RootElement.GetProperty("paths")
                .GetProperty("/api/admin/auth/session")
                .GetProperty("get")
                .GetProperty("security")[0]
                .EnumerateObject()
                .Single()
                .Name);
    }
}
