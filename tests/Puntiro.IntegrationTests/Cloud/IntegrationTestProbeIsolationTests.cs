using System.Net;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class IntegrationTestProbeIsolationTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Base_program_does_not_expose_test_probes_in_testing_or_production()
    {
        using var testing = factory.CreateBaseTestingFactory();
        using var testingClient = testing.CreateClient(
            CloudWebApplicationFactory.SecureClientOptions());
        using var production = factory.CreateProductionFactory();
        using var productionClient = production.CreateClient(
            CloudWebApplicationFactory.SecureClientOptions());

        var testingResponse = await testingClient.GetAsync(
            "/api/v1/test/shipments/read",
            TestContext.Current.CancellationToken);
        var productionResponse = await productionClient.GetAsync(
            "/api/v1/test/shipments/read",
            TestContext.Current.CancellationToken);
        var testingOpenApi = await testingClient.GetStringAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);
        var productionOpenApi = await productionClient.GetStringAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, testingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, productionResponse.StatusCode);
        Assert.DoesNotContain("/api/v1/test/shipments", testingOpenApi, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/test/shipments", productionOpenApi, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Integration_test_factory_composition_exposes_authorized_probes()
    {
        using var client = factory.CreateClient(
            CloudWebApplicationFactory.SecureClientOptions());

        var response = await client.GetAsync(
            "/api/v1/test/shipments/read",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
