using Microsoft.Extensions.DependencyInjection;
using Puntiro.Modules.Identity;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Security;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class IdentityCompositionTests
{
    [Fact]
    public void Module_resolves_every_public_identity_service_in_a_real_scope()
    {
        var services = new ServiceCollection();
        services.AddIdentityModule(
            "Host=127.0.0.1;Port=5432;Database=puntiro_composition;Username=puntiro_composition",
            IdentityKeyOptions.ForTesting(
                "session-v1",
                Enumerable.Repeat((byte)0x51, 32).ToArray(),
                "recovery-v1",
                Enumerable.Repeat((byte)0x72, 32).ToArray()));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIdentityProvisioningService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAdminAuthenticationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAdminSessionService>());
    }
}
