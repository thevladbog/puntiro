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

    [Fact]
    public async Task V1_documents_token_request_bodies_successes_and_exact_problem_contracts()
    {
        using var client = factory.CreateSecureClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        var adminCollection = paths.GetProperty("/api/admin/integration-tokens");
        var list = adminCollection.GetProperty("get");
        var create = adminCollection.GetProperty("post");
        var revoke = paths.GetProperty("/api/admin/integration-tokens/{id}/revoke")
            .GetProperty("post");

        AssertJsonRequestBody(create, "CreateIntegrationTokenRequest");
        AssertJsonRequestBody(revoke, "RevokeIntegrationTokenRequest");
        AssertJsonSuccess(list, "200", "IntegrationTokenResponse", isArray: true);
        AssertJsonSuccess(create, "201", "IssuedIntegrationTokenResponse", isArray: false);
        Assert.False(revoke.GetProperty("responses").GetProperty("204")
            .TryGetProperty("content", out _));

        AssertProblemCodes(list, "401", "auth.session_expired");
        AssertProblemCodes(list, "403", "auth.forbidden");
        AssertProblemCodes(list, "429", "auth.rate_limited");
        AssertProblemCodes(create, "400", "request.invalid");
        AssertProblemCodes(create, "401", "auth.session_expired");
        AssertProblemCodes(
            create,
            "403",
            "auth.csrf_invalid",
            "auth.forbidden",
            "auth.step_up_required");
        AssertProblemCodes(
            create,
            "409",
            "integration_token.active_limit",
            "integration_token.creation_conflict");
        AssertProblemCodes(create, "429", "auth.rate_limited");
        AssertProblemCodes(revoke, "400", "request.invalid");
        AssertProblemCodes(revoke, "401", "auth.session_expired");
        AssertProblemCodes(revoke, "403", "auth.csrf_invalid", "auth.forbidden");
        AssertProblemCodes(revoke, "404", "integration_token.not_found");
        AssertProblemCodes(revoke, "409", "integration_token.version_conflict");
        AssertProblemCodes(revoke, "429", "auth.rate_limited");

        var integrationRead = paths.GetProperty("/api/v1/test/shipments/read")
            .GetProperty("get");
        AssertProblemCodes(integrationRead, "401", "integration.invalid_credentials");
        AssertProblemCodes(integrationRead, "403", "integration.scope_forbidden");
        AssertProblemCodes(integrationRead, "429", "integration.rate_limited");

        var issuedTokenSchema = document.RootElement.GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("IssuedIntegrationTokenResponse")
            .GetProperty("properties")
            .GetProperty("token");
        Assert.False(issuedTokenSchema.TryGetProperty("example", out _));
        Assert.False(issuedTokenSchema.TryGetProperty("default", out _));
    }

    private static void AssertJsonRequestBody(JsonElement operation, string schemaName)
    {
        var body = operation.GetProperty("requestBody");
        Assert.True(body.GetProperty("required").GetBoolean());
        Assert.EndsWith(
            "/" + schemaName,
            body.GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString(),
            StringComparison.Ordinal);
    }

    private static void AssertJsonSuccess(
        JsonElement operation,
        string status,
        string schemaName,
        bool isArray)
    {
        var response = operation.GetProperty("responses").GetProperty(status);
        var schema = response.GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        if (isArray)
        {
            Assert.Equal("array", schema.GetProperty("type").GetString());
            schema = schema.GetProperty("items");
        }

        Assert.EndsWith(
            "/" + schemaName,
            schema.GetProperty("$ref").GetString(),
            StringComparison.Ordinal);
    }

    private static void AssertProblemCodes(
        JsonElement operation,
        string status,
        params string[] codes)
    {
        var response = operation.GetProperty("responses").GetProperty(status);
        var problem = response.GetProperty("content").GetProperty("application/problem+json");
        Assert.EndsWith(
            "/ApiProblemDocument",
            problem.GetProperty("schema").GetProperty("$ref").GetString(),
            StringComparison.Ordinal);
        var description = response.GetProperty("description").GetString();
        foreach (var code in codes)
        {
            Assert.Contains(code, description, StringComparison.Ordinal);
        }
    }
}
