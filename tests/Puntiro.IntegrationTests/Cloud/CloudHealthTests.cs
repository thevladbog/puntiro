using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Domain;
using Puntiro.Modules.Integrations.Persistence;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class CloudHealthTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Live_and_ready_probes_are_healthy_after_reviewed_migrations()
    {
        using var client = factory.CreateSecureClient();

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
        var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var report = await factory.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);
        Assert.True(
            report.Status == HealthStatus.Healthy,
            string.Join("; ", report.Entries.Select(item =>
                $"{item.Key}:{item.Value.Description}:{item.Value.Exception?.GetType().Name}")));
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}

public sealed class CloudRetainedIntegrationKeyReadinessTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Ready_accepts_an_unrevoked_token_when_its_old_key_is_retained()
    {
        await CreateLegacyVersionTokenAsync(factory);
        using var client = factory.CreateSecureClient();

        var response = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    internal static async Task CreateLegacyVersionTokenAsync(CloudWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIntegrationTokenService>();
        using var issued = await service.CreateAsync(
            new CreateIntegrationToken(
                factory.OrganizationId,
                factory.OwnerUserId,
                "Legacy readiness token",
                new HashSet<IntegrationScope> { IntegrationScope.ShipmentsRead }),
            TestContext.Current.CancellationToken);
        var context = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
        const string legacyVersion = "v0";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE integrations.integration_tokens SET key_version = {legacyVersion} WHERE id = {issued.Metadata.Id}",
            TestContext.Current.CancellationToken);
    }
}

public sealed class CloudMissingIntegrationKeyReadinessTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Ready_fails_closed_after_an_unrevoked_tokens_key_is_removed()
    {
        await CloudRetainedIntegrationKeyReadinessTests.CreateLegacyVersionTokenAsync(factory);
        using var withoutRetainedKey = factory.CreateProductionFactory(
            includeRetainedIntegrationKey: false);
        using var client = withoutRetainedKey.CreateSecureClient();

        var response = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("configuration.not_ready", problem!.Code);
    }
}

public sealed class CloudReadinessFailureTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Ready_fails_closed_when_a_reviewed_migration_is_pending()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<
            Puntiro.Modules.Integrations.Persistence.IntegrationsDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM integrations.\"__EFMigrationsHistory\"",
            TestContext.Current.CancellationToken);
        using var client = factory.CreateSecureClient();

        var response = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("configuration.not_ready", problem!.Code);
    }
}
