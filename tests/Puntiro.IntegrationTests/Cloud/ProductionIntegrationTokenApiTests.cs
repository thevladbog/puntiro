using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Domain;
using Puntiro.Modules.Integrations.Persistence;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class ProductionIntegrationTokenApiTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Production_retry_strategy_supports_create_authenticate_and_revoke()
    {
        using var production = factory.CreateProductionFactory();
        using var admin = production.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(admin, factory);
        var issued = await CloudIntegrationTestClient.CreateTokenAsync(
            admin,
            csrf,
            "Production flow",
            ["shipments.write"]);

        await using var scope = production.Services.CreateAsyncScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IIntegrationTokenService>();
        var principal = await tokens.AuthenticateAsync(
            issued.Token,
            TestContext.Current.CancellationToken);
        var metadata = await tokens.ListAsync(
            factory.OrganizationId,
            TestContext.Current.CancellationToken);
        var response = await CloudIntegrationTestClient.RevokeAsync(
            admin,
            csrf,
            issued.Id,
            Assert.Single(metadata).Version);

        Assert.NotNull(principal);
        Assert.Equal(factory.OrganizationId, principal.OrganizationId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await tokens.AuthenticateAsync(
            issued.Token,
            TestContext.Current.CancellationToken));
    }
}

public sealed class ProductionIntegrationTokenTransientCreateTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Transient_create_replay_commits_one_token_scope_and_event()
    {
        var interceptor = new FailFirstIntegrationSecurityEventCommandInterceptor();
        using var production = factory.CreateProductionFactory(integrationInterceptor: interceptor);
        using var admin = production.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(admin, factory);

        var issued = await CloudIntegrationTestClient.CreateTokenAsync(
            admin,
            csrf,
            "Transient create",
            ["shipments.write"]);

        Assert.Equal(1, interceptor.FailureCount);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
        Assert.Equal(1, await context.IntegrationTokens.CountAsync(
            item => item.Id == issued.Id,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.IntegrationTokenScopes.CountAsync(
            item => item.TokenId == issued.Id,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.SecurityEvents.CountAsync(
            item => item.TokenId == issued.Id && item.EventType == "integration_token.created",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ambiguous_create_commit_is_verified_without_a_duplicate_or_second_secret()
    {
        var interceptor = new FailFirstIntegrationCommittedInterceptor();
        using var production = factory.CreateProductionFactory(integrationInterceptor: interceptor);
        using var admin = production.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(admin, factory);

        var issued = await CloudIntegrationTestClient.CreateTokenAsync(
            admin,
            csrf,
            "Ambiguous create",
            ["shipments.read"]);

        Assert.Equal(1, interceptor.FailureCount);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
        Assert.Equal(1, await context.IntegrationTokens.CountAsync(
            item => item.Id == issued.Id,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.SecurityEvents.CountAsync(
            item => item.TokenId == issued.Id && item.EventType == "integration_token.created",
            TestContext.Current.CancellationToken));
    }
}

public sealed class ProductionIntegrationTokenTransientUsageTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Transient_last_used_write_replays_as_one_coalesced_version_change()
    {
        using var admin = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(admin, factory);
        var issued = await CloudIntegrationTestClient.CreateTokenAsync(
            admin,
            csrf,
            "Usage",
            ["shipments.read"]);
        var interceptor = new FailFirstIntegrationUsageCommandInterceptor();
        using var production = factory.CreateProductionFactory(integrationInterceptor: interceptor);
        await using var scope = production.Services.CreateAsyncScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IIntegrationTokenService>();

        var principal = await tokens.AuthenticateAsync(
            issued.Token,
            TestContext.Current.CancellationToken);
        var metadata = Assert.Single(await tokens.ListAsync(
            factory.OrganizationId,
            TestContext.Current.CancellationToken));

        Assert.NotNull(principal);
        Assert.Equal(1, interceptor.FailureCount);
        Assert.Equal(2, metadata.Version);
        Assert.NotNull(metadata.LastUsedAt);
    }
}

public sealed class ProductionIntegrationTokenTransientRevokeTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Ambiguous_revoke_commit_is_verified_without_a_duplicate_event()
    {
        using var admin = factory.CreateSecureClient();
        var csrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(admin, factory);
        var issued = await CloudIntegrationTestClient.CreateTokenAsync(
            admin,
            csrf,
            "Revoke",
            ["shipments.write"]);
        var metadata = await admin.GetFromJsonAsync<CloudIntegrationTokenMetadata[]>(
            "/api/admin/integration-tokens",
            TestContext.Current.CancellationToken);
        var interceptor = new FailFirstIntegrationCommittedInterceptor();
        using var production = factory.CreateProductionFactory(integrationInterceptor: interceptor);
        using var productionAdmin = production.CreateSecureClient();
        var productionCsrf = await CloudIntegrationTestClient.LoginAndGetCsrfAsync(
            productionAdmin,
            factory);

        var response = await CloudIntegrationTestClient.RevokeAsync(
            productionAdmin,
            productionCsrf,
            issued.Id,
            Assert.Single(metadata!).Version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, interceptor.FailureCount);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
        Assert.Equal(1, await context.SecurityEvents.CountAsync(
            item => item.TokenId == issued.Id && item.EventType == "integration_token.revoked",
            TestContext.Current.CancellationToken));
    }
}

internal sealed class FailFirstIntegrationSecurityEventCommandInterceptor : DbCommandInterceptor
{
    private int _failed;
    public int FailureCount => Volatile.Read(ref _failed);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("INSERT INTO integrations.security_events", StringComparison.Ordinal) &&
            Interlocked.CompareExchange(ref _failed, 1, 0) == 0)
        {
            return ValueTask.FromException<InterceptionResult<DbDataReader>>(Transient());
        }

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    internal static PostgresException Transient() => new(
        "Injected transient serialization failure.",
        "ERROR",
        "ERROR",
        PostgresErrorCodes.SerializationFailure);
}

internal sealed class FailFirstIntegrationUsageCommandInterceptor : DbCommandInterceptor
{
    private int _failed;
    public int FailureCount => Volatile.Read(ref _failed);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("UPDATE integrations.integration_tokens", StringComparison.Ordinal) &&
            command.CommandText.Contains("last_used_at", StringComparison.Ordinal) &&
            Interlocked.CompareExchange(ref _failed, 1, 0) == 0)
        {
            return ValueTask.FromException<InterceptionResult<DbDataReader>>(
                FailFirstIntegrationSecurityEventCommandInterceptor.Transient());
        }

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

internal sealed class FailFirstIntegrationCommittedInterceptor : DbTransactionInterceptor
{
    private int _failed;
    public int FailureCount => Volatile.Read(ref _failed);

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is IntegrationsDbContext &&
            Interlocked.CompareExchange(ref _failed, 1, 0) == 0)
        {
            return Task.FromException(
                FailFirstIntegrationSecurityEventCommandInterceptor.Transient());
        }

        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }
}
