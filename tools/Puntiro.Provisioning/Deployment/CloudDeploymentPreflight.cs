using System.Collections;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Puntiro.Provisioning.Deployment;

internal sealed record CloudPreflightConnections(
    string ContainerConnection,
    string HostConnection,
    string? TestConnection,
    string Database,
    string Username,
    string Password,
    int HostPort,
    bool RestoreMode);

internal static class CloudDeploymentPreflight
{
    private const string ConnectionError = "Cloud connection configuration is invalid.";
    private const string CryptographicError = "Cloud cryptographic material is unusable.";
    private static readonly HashSet<string> AllowedConnectionProperties = new(
        ["Host", "Port", "Database", "Username", "Password", "Include Error Detail"],
        StringComparer.OrdinalIgnoreCase);

    internal static void ValidateFromEnvironment(string mode)
    {
        var restoreMode = mode switch
        {
            "normal" => false,
            "restore" => true,
            _ => throw new InvalidOperationException(ConnectionError),
        };
        var hostPortText = Required("PUNTIRO_PREFLIGHT_HOST_PORT");
        if (!int.TryParse(hostPortText, out var hostPort))
        {
            throw new InvalidOperationException(ConnectionError);
        }

        ValidateConnections(new CloudPreflightConnections(
            Required("PUNTIRO_PREFLIGHT_CONTAINER_CONNECTION"),
            Required("PUNTIRO_PREFLIGHT_HOST_CONNECTION"),
            restoreMode ? Required("PUNTIRO_PREFLIGHT_TEST_CONNECTION") : null,
            Required("PUNTIRO_PREFLIGHT_DATABASE"),
            Required("PUNTIRO_PREFLIGHT_USERNAME"),
            Required("PUNTIRO_PREFLIGHT_PASSWORD"),
            hostPort,
            restoreMode));
        ValidateCryptographicMaterial(
            Required("PUNTIRO_PREFLIGHT_KEY_RING"),
            Required("PUNTIRO_PREFLIGHT_CERTIFICATE"),
            Required("PUNTIRO_PREFLIGHT_CERTIFICATE_PASSWORD"));
    }

    internal static void ValidateConnections(CloudPreflightConnections input)
    {
        try
        {
            var container = ParseConnection(input.ContainerConnection);
            var host = ParseConnection(input.HostConnection);
            if (input.HostPort is < 1 or > 65535 ||
                input.RestoreMode &&
                (input.HostPort == 5432 ||
                 !input.Database.StartsWith("puntiro_restore_", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(ConnectionError);
            }

            RequireTarget(
                container,
                "postgres",
                5432,
                input.Database,
                input.Username,
                input.Password);
            RequireTarget(
                host,
                "127.0.0.1",
                input.HostPort,
                input.Database,
                input.Username,
                input.Password);
            if (input.RestoreMode)
            {
                RequireTarget(
                    ParseConnection(input.TestConnection!),
                    "127.0.0.1",
                    input.HostPort,
                    input.Database,
                    input.Username,
                    input.Password);
            }
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException &&
            exception is not StackOverflowException)
        {
            throw new InvalidOperationException(ConnectionError);
        }
    }

    internal static void ValidateCryptographicMaterial(
        string keyRingPath,
        string certificatePath,
        string certificatePassword)
    {
        byte[]? plaintext = null;
        byte[]? protectedPayload = null;
        byte[]? roundtrip = null;
        try
        {
            var before = SnapshotRing(keyRingPath);
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                certificatePassword);
            if (!certificate.HasPrivateKey)
            {
                throw new CryptographicException();
            }

            var services = new ServiceCollection();
            services.AddDataProtection()
                .SetApplicationName("Puntiro.Cloud")
                .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                .ProtectKeysWithCertificate(certificate);
            services.Configure<KeyManagementOptions>(options => options.AutoGenerateKeys = false);
            using var provider = services.BuildServiceProvider(validateScopes: true);
            var protector = provider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("Puntiro.Cloud.Preflight");
            plaintext = RandomNumberGenerator.GetBytes(32);
            protectedPayload = protector.Protect(plaintext);
            roundtrip = protector.Unprotect(protectedPayload);
            if (!CryptographicOperations.FixedTimeEquals(plaintext, roundtrip) ||
                !SnapshotsEqual(before, SnapshotRing(keyRingPath)))
            {
                throw new CryptographicException();
            }
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException &&
            exception is not StackOverflowException)
        {
            throw new InvalidOperationException(CryptographicError);
        }
        finally
        {
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
            if (protectedPayload is not null) CryptographicOperations.ZeroMemory(protectedPayload);
            if (roundtrip is not null) CryptographicOperations.ZeroMemory(roundtrip);
        }
    }

    private static NpgsqlConnectionStringBuilder ParseConnection(string value)
    {
        var canonicalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0) throw new InvalidOperationException(ConnectionError);
            var rawName = segment[..separator].Trim();
            var rawValue = segment[(separator + 1)..];
            var single = new NpgsqlConnectionStringBuilder();
            single[rawName] = rawValue;
            var canonicalName = ((IEnumerable)single.Keys).Cast<string>().Single();
            if (!AllowedConnectionProperties.Contains(canonicalName) ||
                !canonicalNames.Add(canonicalName))
            {
                throw new InvalidOperationException(ConnectionError);
            }
        }

        var builder = new NpgsqlConnectionStringBuilder(value);
        if (canonicalNames.Count < 5 ||
            string.IsNullOrEmpty(builder.Host) ||
            builder.Host.Contains(',', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(ConnectionError);
        }

        return builder;
    }

    private static void RequireTarget(
        NpgsqlConnectionStringBuilder builder,
        string host,
        int port,
        string database,
        string username,
        string password)
    {
        if (!string.Equals(builder.Host, host, StringComparison.Ordinal) ||
            builder.Port != port ||
            !string.Equals(builder.Database, database, StringComparison.Ordinal) ||
            !string.Equals(builder.Username, username, StringComparison.Ordinal) ||
            !string.Equals(builder.Password, password, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(ConnectionError);
        }
    }

    private static Dictionary<string, byte[]> SnapshotRing(string keyRingPath) =>
        Directory.GetFiles(keyRingPath, "key-*.xml", SearchOption.TopDirectoryOnly)
            .ToDictionary(
                path => Path.GetFileName(path),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    private static bool SnapshotsEqual(
        IReadOnlyDictionary<string, byte[]> left,
        IReadOnlyDictionary<string, byte[]> right)
    {
        if (left.Count == 0 || left.Count != right.Count) return false;
        foreach (var (name, content) in left)
        {
            if (!right.TryGetValue(name, out var other) ||
                !CryptographicOperations.FixedTimeEquals(content, other)) return false;
        }
        return true;
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value)) throw new InvalidOperationException(ConnectionError);
        return value;
    }
}
