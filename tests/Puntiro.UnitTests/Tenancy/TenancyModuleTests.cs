using Microsoft.Extensions.DependencyInjection;
using Puntiro.Modules.Tenancy;
using Xunit;

namespace Puntiro.UnitTests.Tenancy;

public sealed class TenancyModuleTests
{
    [Fact]
    public void Default_registration_preserves_the_host_time_provider()
    {
        var expected = new FixedTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(expected);

        services.AddTenancyModule("Host=127.0.0.1;Database=not_opened");

        using var provider = services.BuildServiceProvider();
        Assert.Same(expected, provider.GetRequiredService<TimeProvider>());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(2026, 8, 8, 0, 0, 0, TimeSpan.Zero);
        }
    }
}
