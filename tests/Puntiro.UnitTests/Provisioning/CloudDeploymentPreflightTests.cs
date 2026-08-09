using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Puntiro.Provisioning.Deployment;
using Xunit;

namespace Puntiro.UnitTests.Provisioning;

public sealed class CloudDeploymentPreflightTests
{
    private const string Database = "puntiro_restore_drill";
    private const string Username = "puntiro_restore_fixture";
    private const string Password = "fixture-only";

    [Theory]
    [InlineData("Host=postgres;Server=elsewhere;Port=5432;Database=puntiro_restore_drill;Username=puntiro_restore_fixture;Password=fixture-only")]
    [InlineData("Host=postgres;Port=5432;Database=puntiro_restore_drill;Username=puntiro_restore_fixture;User ID=other;Password=fixture-only")]
    [InlineData("Host=postgres;Port=5432;Database=puntiro_restore_drill;Username=puntiro_restore_fixture;Password=fixture-only;Pwd=other")]
    public void Connection_aliases_cannot_override_an_already_declared_canonical_property(
        string containerConnection)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CloudDeploymentPreflight.ValidateConnections(RestoreConnections(containerConnection)));

        Assert.Equal("Cloud connection configuration is invalid.", exception.Message);
        Assert.DoesNotContain("fixture-only", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Target Session Attributes=read-write")]
    [InlineData("Load Balance Hosts=true")]
    [InlineData("Options=-c search_path=other")]
    public void Unsupported_routing_properties_are_rejected(string property)
    {
        var container = $"{ContainerConnection()};{property}";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CloudDeploymentPreflight.ValidateConnections(RestoreConnections(container)));

        Assert.Equal("Cloud connection configuration is invalid.", exception.Message);
    }

    [Fact]
    public void Canonical_container_and_host_targets_are_resolved_by_Npgsql()
    {
        CloudDeploymentPreflight.ValidateConnections(RestoreConnections(ContainerConnection()));
    }

    [Fact]
    public void Normal_startup_does_not_depend_on_the_integration_test_maintenance_connection()
    {
        CloudDeploymentPreflight.ValidateConnections(new CloudPreflightConnections(
            "Host=postgres;Port=5432;Database=puntiro_normal;Username=puntiro_app;Password=fixture-only",
            "Host=127.0.0.1;Port=55439;Database=puntiro_normal;Username=puntiro_app;Password=fixture-only",
            null,
            "puntiro_normal",
            "puntiro_app",
            Password,
            55439,
            RestoreMode: false));
    }

    [Fact]
    public void Real_certificate_and_protected_ring_support_a_Data_Protection_roundtrip_without_rotation()
    {
        using var fixture = CryptographicFixture.CreateValid();
        var before = Directory.GetFiles(fixture.RingPath)
            .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes, StringComparer.Ordinal);

        CloudDeploymentPreflight.ValidateCryptographicMaterial(
            fixture.RingPath,
            fixture.CertificatePath,
            fixture.CertificatePassword);

        var after = Directory.GetFiles(fixture.RingPath)
            .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes, StringComparer.Ordinal);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var name in before.Keys)
        {
            Assert.Equal(before[name], after[name]);
        }
    }

    [Fact]
    public void A_fake_PKCS12_sequence_is_rejected()
    {
        using var fixture = CryptographicFixture.CreateValid();
        File.WriteAllBytes(fixture.CertificatePath, [0x30, 0x82, 0x00, 0x01, 0x00]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CloudDeploymentPreflight.ValidateCryptographicMaterial(
                fixture.RingPath,
                fixture.CertificatePath,
                fixture.CertificatePassword));

        Assert.Equal("Cloud cryptographic material is unusable.", exception.Message);
        Assert.DoesNotContain(fixture.CertificatePassword, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_encryptedSecret_payload_is_rejected_by_the_real_Data_Protection_stack()
    {
        using var fixture = CryptographicFixture.CreateValid();
        foreach (var keyPath in Directory.GetFiles(fixture.RingPath, "key-*.xml"))
        {
            File.WriteAllText(
                keyPath,
                "<key id=\"11111111-2222-3333-4444-555555555555\" version=\"1\"><creationDate>2026-08-09T00:00:00Z</creationDate><activationDate>2026-08-09T00:00:00Z</activationDate><expirationDate>2026-11-09T00:00:00Z</expirationDate><descriptor><encryptedSecret decryptorType=\"fixture\" /></descriptor></key>");
        }

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CloudDeploymentPreflight.ValidateCryptographicMaterial(
                fixture.RingPath,
                fixture.CertificatePath,
                fixture.CertificatePassword));

        Assert.Equal("Cloud cryptographic material is unusable.", exception.Message);
    }

    private static CloudPreflightConnections RestoreConnections(string container) => new(
        container,
        HostConnection(),
        HostConnection(),
        Database,
        Username,
        Password,
        55440,
        RestoreMode: true);

    private static string ContainerConnection() =>
        $"Host=postgres;Port=5432;Database={Database};Username={Username};Password={Password};Include Error Detail=false";

    private static string HostConnection() =>
        $"Host=127.0.0.1;Port=55440;Database={Database};Username={Username};Password={Password};Include Error Detail=false";

    private sealed class CryptographicFixture : IDisposable
    {
        private CryptographicFixture(
            string root,
            string ringPath,
            string certificatePath,
            string certificatePassword)
        {
            Root = root;
            RingPath = ringPath;
            CertificatePath = certificatePath;
            CertificatePassword = certificatePassword;
        }

        internal string Root { get; }
        internal string RingPath { get; }
        internal string CertificatePath { get; }
        internal string CertificatePassword { get; }

        internal static CryptographicFixture CreateValid()
        {
            var root = Path.Combine(Path.GetTempPath(), $"puntiro-preflight-{Guid.NewGuid():N}");
            var ring = Directory.CreateDirectory(Path.Combine(root, "ring"));
            var certificatePath = Path.Combine(root, "data-protection.pfx");
            var password = $"fixture-{Guid.NewGuid():N}";

            using var key = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=Puntiro Cloud Preflight Test",
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddDays(2));
            File.WriteAllBytes(
                certificatePath,
                certificate.Export(X509ContentType.Pfx, password));

            var services = new ServiceCollection();
            services.AddDataProtection()
                .SetApplicationName("Puntiro.Cloud")
                .PersistKeysToFileSystem(ring)
                .ProtectKeysWithCertificate(certificate);
            using var provider = services.BuildServiceProvider(validateScopes: true);
            _ = provider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("Puntiro.Cloud.Preflight")
                .Protect("fixture-probe");

            return new CryptographicFixture(root, ring.FullName, certificatePath, password);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
