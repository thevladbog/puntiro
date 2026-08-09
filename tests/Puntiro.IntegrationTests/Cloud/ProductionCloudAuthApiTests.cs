using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Puntiro.Modules.Identity.Persistence;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class ProductionCloudAuthApiTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Production_retry_strategy_supports_the_complete_admin_session_flow()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        using var production = factory.CreateProductionFactory();
        using var client = production.CreateSecureClient();

        var login = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.CurrentTotp()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        var session = await client.GetFromJsonAsync<AdminSessionResponse>(
            "/api/admin/auth/session",
            TestContext.Current.CancellationToken);
        Assert.NotNull(session);

        using var stepUp = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/step-up")
        {
            Content = JsonContent.Create(new { totpCode = factory.NextTotp() })
        };
        stepUp.Headers.Add("X-Puntiro-CSRF", session.AntiforgeryToken);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.SendAsync(stepUp, TestContext.Current.CancellationToken)).StatusCode);

        var refreshed = await client.GetFromJsonAsync<AdminSessionResponse>(
            "/api/admin/auth/session",
            TestContext.Current.CancellationToken);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/logout");
        logout.Headers.Add("X-Puntiro-CSRF", refreshed!.AntiforgeryToken);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.SendAsync(logout, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Production_transient_login_replay_commits_one_session_and_one_success_event()
    {
        factory.Time.Advance(TimeSpan.FromSeconds(60));
        var interceptor = new FailFirstIdentitySecurityEventCommandInterceptor();
        using var production = factory.CreateProductionFactory(interceptor);
        using var client = production.CreateSecureClient();
        var (sessionsBefore, eventsBefore) = await CountsAsync();

        var login = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = factory.OwnerEmail,
            password = factory.OwnerPassword,
            totpCode = factory.CurrentTotp()
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Equal(1, interceptor.FailureCount);
        var (sessionsAfter, eventsAfter) = await CountsAsync();
        Assert.Equal(sessionsBefore + 1, sessionsAfter);
        Assert.Equal(eventsBefore + 1, eventsAfter);

        async Task<(int Sessions, int Events)> CountsAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var sessions = await context.Sessions.CountAsync(
                item => item.UserId == factory.OwnerUserId,
                TestContext.Current.CancellationToken);
            var events = await context.SecurityEvents.CountAsync(
                item => item.UserId == factory.OwnerUserId &&
                    item.EventType == "authentication.login" &&
                    item.Result == "success",
                TestContext.Current.CancellationToken);
            return (sessions, events);
        }
    }

    private sealed record AdminSessionResponse(string AntiforgeryToken);
}
