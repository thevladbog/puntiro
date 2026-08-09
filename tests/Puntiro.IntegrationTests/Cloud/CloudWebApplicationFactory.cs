extern alias cloud;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Puntiro.IntegrationTests.Identity;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Integrations.Persistence;
using Puntiro.Modules.Tenancy.Contracts;
using Puntiro.Modules.Tenancy.Persistence;
using Xunit;
using IdentityTotp = Puntiro.Modules.Identity.Security.Rfc6238Totp;

namespace Puntiro.IntegrationTests.Cloud;

public sealed class CloudWebApplicationFactory : WebApplicationFactory<cloud::Program>, IAsyncLifetime
{
    private readonly PostgresDatabase _database = new();
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        $"puntiro-cloud-tests-{Guid.NewGuid():N}");
    private readonly string _certificatePassword = Convert.ToBase64String(
        RandomNumberGenerator.GetBytes(32));
    private byte[] _totpSecret = [];
    private readonly CapturingLoggerProvider _logs = new();

    public CloudWebApplicationFactory()
    {
        OwnerEmail = $"owner-{Guid.NewGuid():N}@example.test";
        OwnerPassword = $"P!{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}";
        Time = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
    }

    public string OwnerEmail { get; }
    public string OwnerPassword { get; }
    public Guid OwnerUserId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string RecoveryCode { get; private set; } = string.Empty;
    public AdjustableTimeProvider Time { get; }
    public IReadOnlyList<string> Logs => _logs.Messages;

    public HttpClient CreateSecureClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false
    });

    public string CurrentTotp() => IdentityTotp.Generate(
        _totpSecret,
        Time.GetUtcNow().ToUnixTimeSeconds());

    public string NextTotp()
    {
        Time.Advance(TimeSpan.FromSeconds(30));
        return CurrentTotp();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_temporaryRoot);
        var keyPath = Path.Combine(_temporaryRoot, "keys");
        Directory.CreateDirectory(keyPath);
        var certificatePath = Path.Combine(_temporaryRoot, "key-protection.pfx");
        CreateCertificate(certificatePath, _certificatePassword);
        var sessionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var recoveryKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var integrationKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        builder.UseEnvironment("Testing");
        foreach (var setting in new Dictionary<string, string>
                 {
                     ["ConnectionStrings:Puntiro"] = _database.ConnectionString,
                     ["Puntiro:Admin:AllowedOrigin"] = "https://localhost",
                     ["Puntiro:Security:DataProtectionKeysPath"] = keyPath,
                     ["Puntiro:Security:DataProtectionCertificatePath"] = certificatePath,
                     ["Puntiro:Security:DataProtectionCertificatePassword"] = _certificatePassword,
                     ["Puntiro:Security:SessionHmac:CurrentVersion"] = "v1",
                     ["Puntiro:Security:SessionHmac:Keys:v1"] = sessionKey,
                     ["Puntiro:Security:RecoveryHmac:CurrentVersion"] = "v1",
                     ["Puntiro:Security:RecoveryHmac:Keys:v1"] = recoveryKey,
                     ["Puntiro:Security:IntegrationHmac:CurrentVersion"] = "v1",
                     ["Puntiro:Security:IntegrationHmac:Keys:v1"] = integrationKey,
                     ["Puntiro:Security:LoginIpLimit"] = "3",
                     ["Puntiro:Security:LoginAccountLimit"] = "3",
                     ["Puntiro:Security:StepUpSessionLimit"] = "2"
                 })
        {
            builder.UseSetting(setting.Key, setting.Value);
        }
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.AddLogging(logging => logging.AddProvider(_logs));
        });
    }

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();
        _ = Services;
        await using var scope = Services.CreateAsyncScope();
        var cancellationToken = TestContext.Current.CancellationToken;
        await scope.ServiceProvider.GetRequiredService<TenancyDbContext>()
            .Database.MigrateAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .Database.MigrateAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>()
            .Database.MigrateAsync(cancellationToken);
        _ = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Puntiro.Cloud.Tests")
            .Protect("readiness");
        await ProvisionOwnerAsync(scope.ServiceProvider, cancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        CryptographicOperations.ZeroMemory(_totpSecret);
        await base.DisposeAsync();
        await _database.DisposeAsync();
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private async Task ProvisionOwnerAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var tenancy = services.GetRequiredService<ITenancyProvisioningService>();
        var identity = services.GetRequiredService<IIdentityProvisioningService>();
        var organization = await tenancy.GetOrCreateProvisioningAsync(
            "Puntiro Cloud Test",
            $"cloud-{Guid.NewGuid():N}",
            cancellationToken);
        using var pending = await identity.BeginOwnerAsync(
            organization.Id,
            OwnerEmail,
            OwnerPassword,
            new IdentityAuditContext(null, "test-cloud-bootstrap"),
            cancellationToken);
        _totpSecret = IdentityTestScope.ReadTotpSecret(pending.TotpUri);
        RecoveryCode = pending.RecoveryCodes[0].Reveal();
        var firstCode = CurrentTotp();
        await identity.ConfirmOwnerTotpAsync(
            pending.UserId,
            firstCode,
            new IdentityAuditContext(pending.UserId, "test-cloud-confirm"),
            cancellationToken);
        await tenancy.EnsureOwnerMembershipAsync(
            organization.Id,
            pending.UserId,
            cancellationToken);
        await tenancy.ActivateAsync(
            organization.Id,
            new TenancyAuditContext(pending.UserId, "test-cloud-activate"),
            cancellationToken);
        await identity.CompleteOwnerAsync(
            pending.UserId,
            organization.Id,
            new IdentityAuditContext(pending.UserId, "test-cloud-complete"),
            cancellationToken);
        OwnerUserId = pending.UserId;
        OrganizationId = organization.Id;
        Time.Advance(TimeSpan.FromSeconds(30));
    }

    private static void CreateCertificate(string path, string password)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Puntiro Cloud Test Key Protection",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(2));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
    }
}

public sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(
        System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));
            if (exception is not null)
            {
                messages.Enqueue(exception.GetType().Name);
            }

            while (messages.Count > 500)
            {
                messages.TryDequeue(out _);
            }
        }
    }
}
