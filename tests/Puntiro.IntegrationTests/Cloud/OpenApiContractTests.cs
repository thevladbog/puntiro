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
        foreach (var operation in new[]
                 {
                     document.RootElement.GetProperty("paths")
                         .GetProperty("/api/admin/auth/logout")
                         .GetProperty("post"),
                     document.RootElement.GetProperty("paths")
                         .GetProperty("/api/admin/auth/step-up")
                         .GetProperty("post")
                 })
        {
            var parameter = Assert.Single(
                operation.GetProperty("parameters").EnumerateArray(),
                item => item.GetProperty("name").GetString() == "X-Puntiro-CSRF");
            Assert.Equal("header", parameter.GetProperty("in").GetString());
            Assert.True(parameter.GetProperty("required").GetBoolean());
            Assert.Equal("string", parameter.GetProperty("schema").GetProperty("type").GetString());
            Assert.Contains(
                "antiforgery",
                parameter.GetProperty("description").GetString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task V1_distinguishes_admin_token_management_from_scoped_integration_bearer()
    {
        using var client = factory.CreateSecureClient();
        var json = await client.GetStringAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
        var integration = schemes.GetProperty("IntegrationToken");
        Assert.Equal("http", integration.GetProperty("type").GetString());
        Assert.Equal("bearer", integration.GetProperty("scheme").GetString());

        var adminCreate = document.RootElement.GetProperty("paths")
            .GetProperty("/api/admin/integration-tokens")
            .GetProperty("post");
        Assert.Equal(
            "AdminSession",
            adminCreate.GetProperty("security")[0].EnumerateObject().Single().Name);
        Assert.Single(
            adminCreate.GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "X-Puntiro-CSRF");

        var integrationRead = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/test/shipments/read")
            .GetProperty("get");
        var requirement = integrationRead.GetProperty("security")[0]
            .GetProperty("IntegrationToken")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Equal(new[] { "shipments.read" }, requirement);
        Assert.False(integrationRead.TryGetProperty("parameters", out _));
        Assert.DoesNotContain("pnt_live_", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"example\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
