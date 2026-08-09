using System.Collections;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Puntiro.Modules.Identity;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Security;
using Puntiro.Modules.Tenancy;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Provisioning.Bootstrap;
using Puntiro.Provisioning.Cli;
using Puntiro.Provisioning.Recovery;

return await ProvisioningProgram.RunAsync(args);

internal static class ProvisioningProgram
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(10);

    internal static async Task<int> RunAsync(string[] arguments)
    {
        var parsed = ProvisioningArguments.Parse(arguments);
        if (parsed.ExitCode != ProvisioningExit.Success)
        {
            WriteUsage();
            return (int)ProvisioningExit.InvalidArguments;
        }

        try
        {
            var terminal = new SystemProvisioningTerminal();
            await using var runtime = ProvisioningRuntime.CreateFromEnvironment();
            using var timeout = new CancellationTokenSource(CommandTimeout);
            await using var scope = runtime.Services.CreateAsyncScope();
            var exit = parsed.Command switch
            {
                "bootstrap-owner" => await new BootstrapOwnerOrchestrator(
                    scope.ServiceProvider.GetRequiredService<ITenancyProvisioningService>(),
                    scope.ServiceProvider.GetRequiredService<ITenantAccessService>(),
                    scope.ServiceProvider.GetRequiredService<IIdentityProvisioningService>(),
                    terminal).RunAsync(
                        parsed.Arguments["organization-name"],
                        parsed.Arguments["organization-slug"],
                        parsed.Arguments["email"],
                        timeout.Token),
                "reset-owner-totp" => await new ResetOwnerTotpOrchestrator(
                    scope.ServiceProvider.GetRequiredService<ITenancyProvisioningService>(),
                    scope.ServiceProvider.GetRequiredService<ITenantAccessService>(),
                    scope.ServiceProvider.GetRequiredService<IIdentityProvisioningService>(),
                    terminal).RunAsync(
                        parsed.Arguments["organization-slug"],
                        parsed.Arguments["email"],
                        timeout.Token),
                _ => ProvisioningExit.InvalidArguments
            };
            WriteOutcome(exit);
            return (int)exit;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Provisioning was cancelled or timed out; inspect durable state before retrying.");
            return (int)ProvisioningExit.InfrastructureFailure;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Provisioning could not run. Verify the secure terminal and deployment configuration.");
            return (int)ProvisioningExit.InfrastructureFailure;
        }
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine(
            "Usage: Puntiro.Provisioning bootstrap-owner --organization-name <name> " +
            "--organization-slug <slug> --email <email>");
        Console.Error.WriteLine(
            "   or: Puntiro.Provisioning reset-owner-totp --organization-slug <slug> --email <email>");
        Console.Error.WriteLine("Passwords, TOTP values and recovery codes are interactive only.");
    }

    private static void WriteOutcome(ProvisioningExit exit)
    {
        Console.Error.WriteLine(exit switch
        {
            ProvisioningExit.Success => "Provisioning completed.",
            ProvisioningExit.InvalidArguments => "Provisioning arguments are invalid.",
            ProvisioningExit.Conflict => "Provisioning stopped because durable state conflicts with the request.",
            ProvisioningExit.InvalidCredentials => "Provisioning credentials or confirmation are invalid.",
            _ => "Provisioning failed because infrastructure is unavailable."
        });
    }
}

internal sealed class ProvisioningRuntime : IAsyncDisposable
{
    private const string SessionKeyPrefix = "Puntiro__Security__SessionHmac__Keys__";
    private const string RecoveryKeyPrefix = "Puntiro__Security__RecoveryHmac__Keys__";
    private readonly X509Certificate2 _certificate;

    private ProvisioningRuntime(ServiceProvider services, X509Certificate2 certificate)
    {
        Services = services;
        _certificate = certificate;
    }

    internal ServiceProvider Services { get; }

    internal static ProvisioningRuntime CreateFromEnvironment()
    {
        var connectionString = RequireEnvironment("ConnectionStrings__Puntiro");
        var keyRingPath = RequireEnvironment("Puntiro__Security__DataProtectionKeysPath");
        var certificatePath = RequireEnvironment("Puntiro__Security__DataProtectionCertificatePath");
        var certificatePassword = RequireEnvironment(
            "Puntiro__Security__DataProtectionCertificatePassword");
        var currentSessionVersion = RequireEnvironment(
            "Puntiro__Security__SessionHmac__CurrentVersion");
        var currentRecoveryVersion = RequireEnvironment(
            "Puntiro__Security__RecoveryHmac__CurrentVersion");
        if (!Directory.Exists(keyRingPath) || !File.Exists(certificatePath))
        {
            throw new InvalidOperationException("Required cryptographic storage is unavailable.");
        }

        var sessionKeys = ReadKeySet(SessionKeyPrefix);
        var recoveryKeys = ReadKeySet(RecoveryKeyPrefix);
        IdentityKeyOptions keyOptions;
        try
        {
            keyOptions = new IdentityKeyOptions(
                currentSessionVersion,
                sessionKeys,
                currentRecoveryVersion,
                recoveryKeys);
        }
        finally
        {
            ClearKeys(sessionKeys);
            ClearKeys(recoveryKeys);
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            certificatePath,
            certificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);
        try
        {
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException(
                    "The Data Protection certificate does not contain a private key.");
            }

            var services = new ServiceCollection();
            services.AddDataProtection()
                .SetApplicationName("Puntiro.Cloud")
                .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                .ProtectKeysWithCertificate(certificate);
            services.AddIdentityModule(connectionString, keyOptions);
            services.AddTenancyModule(connectionString);
            return new ProvisioningRuntime(
                services.BuildServiceProvider(validateScopes: true),
                certificate);
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        _certificate.Dispose();
    }

    private static string RequireEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Required deployment configuration is missing.");
        }

        return value;
    }

    private static Dictionary<string, byte[]> ReadKeySet(string prefix)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is not string name ||
                    entry.Value is not string encoded ||
                    !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var version = name[prefix.Length..];
                var key = Convert.FromBase64String(encoded);
                if (key.Length != HMACSHA256.HashSizeInBytes || !result.TryAdd(version, key))
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw new InvalidOperationException("An identity HMAC key is invalid.");
                }
            }

            if (result.Count == 0)
            {
                throw new InvalidOperationException("Required identity HMAC keys are missing.");
            }

            return result;
        }
        catch
        {
            ClearKeys(result);
            throw;
        }
    }

    private static void ClearKeys(IReadOnlyDictionary<string, byte[]> keys)
    {
        foreach (var key in keys.Values)
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
