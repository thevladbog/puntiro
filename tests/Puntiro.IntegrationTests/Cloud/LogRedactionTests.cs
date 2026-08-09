using System.Net.Http.Json;
using Xunit;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class LogRedactionTests(CloudWebApplicationFactory factory)
    : IClassFixture<CloudWebApplicationFactory>
{
    [Fact]
    public async Task Invalid_auth_payload_is_never_reflected_in_the_problem_response()
    {
        using var client = factory.CreateSecureClient();
        var marker = $"secret-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = "missing@example.test",
            password = marker,
            recoveryCode = marker
        }, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(marker, body, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.Logs, message =>
            message.Contains(marker, StringComparison.Ordinal) ||
            message.Contains("missing@example.test", StringComparison.OrdinalIgnoreCase));
    }
}
