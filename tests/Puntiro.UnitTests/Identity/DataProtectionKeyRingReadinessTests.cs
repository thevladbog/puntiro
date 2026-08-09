using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Puntiro.Modules.Identity.Security;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class DataProtectionKeyRingReadinessTests
{
    [Fact]
    public void Empty_ring_is_not_ready_and_an_existing_current_key_is_read_without_rotation()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"puntiro-readiness-{Guid.NewGuid():N}");
        var directory = Directory.CreateDirectory(path);
        try
        {
            var services = new ServiceCollection();
            services.AddDataProtection()
                .SetApplicationName("Puntiro.Readiness.Test")
                .PersistKeysToFileSystem(directory);
            using var provider = services.BuildServiceProvider(validateScopes: true);
            var keyManager = provider.GetRequiredService<IKeyManager>();
            var readiness = new DataProtectionKeyRingReadiness(
                keyManager,
                TimeProvider.System);

            Assert.False(readiness.HasUsableCurrentKey());
            Assert.Empty(keyManager.GetAllKeys());

            var plaintext = RandomNumberGenerator.GetBytes(20);
            byte[]? protectedPayload = null;
            try
            {
                protectedPayload = provider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("Puntiro.Readiness.Test.Probe")
                    .Protect(plaintext);
                var keyIds = keyManager.GetAllKeys().Select(item => item.KeyId).ToArray();

                Assert.True(readiness.HasUsableCurrentKey());
                Assert.Equal(keyIds, keyManager.GetAllKeys().Select(item => item.KeyId).ToArray());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
                if (protectedPayload is not null)
                {
                    CryptographicOperations.ZeroMemory(protectedPayload);
                }
            }
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
