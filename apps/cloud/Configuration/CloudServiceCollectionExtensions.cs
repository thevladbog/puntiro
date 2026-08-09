using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Puntiro.Cloud.Auth;
using Puntiro.Cloud.Health;
using Puntiro.Cloud.Http;
using Puntiro.Cloud.OpenApi;
using Puntiro.Modules.Identity;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Security;
using Puntiro.Modules.Integrations;
using Puntiro.Modules.Integrations.Persistence;
using Puntiro.Modules.Integrations.Security;
using Puntiro.Modules.Tenancy;
using Puntiro.Modules.Tenancy.Persistence;

namespace Puntiro.Cloud.Configuration;

public static class CloudServiceCollectionExtensions
{
    public const string AdminOwnerPolicy = "AdminOwner";

    public static IServiceCollection AddPuntiroCloud(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Puntiro");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The Puntiro PostgreSQL connection string is required.");
        }

        var options = configuration.GetSection(PuntiroCloudOptions.SectionName)
            .Get<PuntiroCloudOptions>() ?? new PuntiroCloudOptions();
        Validate(options);
        services.AddOptions<PuntiroCloudOptions>()
            .Bind(configuration.GetSection(PuntiroCloudOptions.SectionName))
            .Validate(Validate, "Puntiro Cloud security configuration is invalid.")
            .ValidateOnStart();
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);

        var sessionKeys = options.Security.SessionHmac.Decode("session HMAC");
        var recoveryKeys = options.Security.RecoveryHmac.Decode("recovery HMAC");
        var integrationKeys = options.Security.IntegrationHmac.Decode("integration HMAC");
        EnsureIndependentKeys(sessionKeys, recoveryKeys, integrationKeys);
        IdentityKeyOptions identityKeys;
        IntegrationKeyOptions integrationOptions;
        try
        {
            identityKeys = new IdentityKeyOptions(
                options.Security.SessionHmac.CurrentVersion,
                sessionKeys,
                options.Security.RecoveryHmac.CurrentVersion,
                recoveryKeys);
            integrationOptions = new IntegrationKeyOptions(
                options.Security.IntegrationHmac.CurrentVersion,
                integrationKeys);
        }
        finally
        {
            foreach (var key in sessionKeys.Values.Concat(recoveryKeys.Values).Concat(integrationKeys.Values))
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            options.Security.DataProtectionCertificatePath,
            options.Security.DataProtectionCertificatePassword);
        services.AddSingleton(certificate);
        services.AddDataProtection()
            .SetApplicationName("Puntiro.Cloud")
            .PersistKeysToFileSystem(new DirectoryInfo(options.Security.DataProtectionKeysPath))
            .ProtectKeysWithCertificate(certificate);

        services.AddIdentityModule(connectionString, identityKeys, TimeProvider.System);
        services.AddTenancyModule(connectionString, TimeProvider.System);
        services.AddIntegrationsModule(connectionString, integrationOptions, TimeProvider.System);
        ConfigureDatabaseRetries(services, connectionString, environment.IsEnvironment("Testing"));

        services.Configure<JsonOptions>(json =>
        {
            json.SerializerOptions.UnmappedMemberHandling =
                System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
            json.SerializerOptions.MaxDepth = 16;
        });
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddAntiforgery(antiforgery =>
        {
            antiforgery.HeaderName = "X-Puntiro-CSRF";
            antiforgery.Cookie.Name = "__Host-puntiro_csrf";
            antiforgery.Cookie.Path = "/";
            antiforgery.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            antiforgery.Cookie.HttpOnly = true;
            antiforgery.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddHttpContextAccessor();
        services.AddScoped<TenantContext>();
        services.AddSingleton<AdminRateLimitService>();
        services.AddAuthentication(AdminSessionAuthenticationHandler.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, AdminSessionAuthenticationHandler>(
                AdminSessionAuthenticationHandler.AuthenticationScheme,
                _ => { });
        services.AddAuthorizationBuilder().AddPolicy(AdminOwnerPolicy, policy =>
        {
            policy.AddAuthenticationSchemes(AdminSessionAuthenticationHandler.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new OwnerRequirement());
        });
        services.AddScoped<IAuthorizationHandler, OwnerAuthorizationHandler>();
        services.AddHealthChecks().AddCheck<CloudReadinessHealthCheck>("cloud-readiness");
        services.AddOpenApi("v1", openApi =>
        {
            openApi.AddDocumentTransformer<AuthenticationDocumentTransformer>();
            openApi.AddOperationTransformer<AuthenticationOperationTransformer>();
        });
        return services;
    }

    public static IApplicationBuilder UsePuntiroAdminCsrf(this IApplicationBuilder app) =>
        app.UseMiddleware<AdminCsrfMiddleware>();

    private static void ConfigureDatabaseRetries(
        IServiceCollection services,
        string connectionString,
        bool testing)
    {
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.SetPostgresVersion(17, 10);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                if (!testing) npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            }));
        services.AddDbContext<TenancyDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.SetPostgresVersion(17, 10);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy");
                if (!testing) npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            }));
        services.AddDbContext<IntegrationsDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.SetPostgresVersion(17, 10);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "integrations");
                if (!testing) npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            }));
    }

    private static bool Validate(PuntiroCloudOptions options)
    {
        if (!Uri.TryCreate(options.Admin.AllowedOrigin, UriKind.Absolute, out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps || origin.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) ||
            !Path.IsPathRooted(options.Security.DataProtectionKeysPath) ||
            !Path.IsPathRooted(options.Security.DataProtectionCertificatePath) ||
            string.IsNullOrWhiteSpace(options.Security.DataProtectionKeysPath) ||
            string.IsNullOrWhiteSpace(options.Security.DataProtectionCertificatePath) ||
            string.IsNullOrEmpty(options.Security.DataProtectionCertificatePassword) ||
            !Directory.Exists(options.Security.DataProtectionKeysPath) ||
            !File.Exists(options.Security.DataProtectionCertificatePath) ||
            options.Security.LoginIpLimit is < 1 or > 1000 ||
            options.Security.LoginAccountLimit is < 1 or > 1000 ||
            options.Security.StepUpSessionLimit is < 1 or > 1000 ||
            options.Security.RateLimitWindowSeconds is < 1 or > 3600 ||
            options.Security.MaximumRateLimitPartitions is < 100 or > 1_000_000 ||
            options.Security.MaximumRequestBodyBytes is < 1024 or > 1_048_576 ||
            options.Security.MaximumHeaderBytes is < 4096 or > 1_048_576)
        {
            return false;
        }

        try
        {
            foreach (var set in new[]
                     {
                         options.Security.SessionHmac,
                         options.Security.RecoveryHmac,
                         options.Security.IntegrationHmac
                     })
            {
                var decoded = set.Decode("configured HMAC");
                foreach (var key in decoded.Values) CryptographicOperations.ZeroMemory(key);
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void EnsureIndependentKeys(params IReadOnlyDictionary<string, byte[]>[] sets)
    {
        for (var left = 0; left < sets.Length; left++)
        {
            for (var right = left + 1; right < sets.Length; right++)
            {
                if (sets[left].Values.Any(a => sets[right].Values.Any(b =>
                        CryptographicOperations.FixedTimeEquals(a, b))))
                {
                    throw new InvalidOperationException("HMAC keys cannot be reused across purposes.");
                }
            }
        }
    }
}

internal sealed class AdminRateLimitService(PuntiroCloudOptions options, TimeProvider timeProvider)
{
    private readonly byte[] _emailPartitionKey = RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentDictionary<string, Counter> _partitions = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    internal bool TryLogin(IPAddress? address, string normalizedEmail, out int retryAfter)
    {
        var ip = address?.ToString() ?? "unknown";
        var email = HashEmail(normalizedEmail);
        return TryAcquire($"login-ip:{ip}", options.Security.LoginIpLimit, out retryAfter) &&
            TryAcquire($"login-account:{email}", options.Security.LoginAccountLimit, out retryAfter);
    }

    internal bool TryStepUp(Guid sessionId, out int retryAfter) =>
        TryAcquire($"step-up:{sessionId:N}", options.Security.StepUpSessionLimit, out retryAfter);

    private bool TryAcquire(string partition, int permitLimit, out int retryAfter)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            var window = TimeSpan.FromSeconds(options.Security.RateLimitWindowSeconds);
            if (_partitions.Count >= options.Security.MaximumRateLimitPartitions)
            {
                foreach (var stale in _partitions.Where(item => item.Value.Start + window <= now)
                             .Take(Math.Max(1, _partitions.Count / 10)).ToArray())
                {
                    _partitions.TryRemove(stale.Key, out _);
                }
            }

            if (_partitions.Count >= options.Security.MaximumRateLimitPartitions &&
                !_partitions.ContainsKey(partition))
            {
                retryAfter = options.Security.RateLimitWindowSeconds;
                return false;
            }

            var counter = _partitions.GetOrAdd(partition, _ => new Counter(now));
            if (counter.Start + window <= now)
            {
                counter.Start = now;
                counter.Count = 0;
            }

            if (counter.Count >= permitLimit)
            {
                retryAfter = Math.Max(1, (int)Math.Ceiling((counter.Start + window - now).TotalSeconds));
                return false;
            }

            counter.Count++;
            retryAfter = 0;
            return true;
        }
    }

    private string HashEmail(string email)
    {
        var normalized = AdminEmailPartition.Normalize(email);
        var bytes = Encoding.UTF8.GetBytes(normalized.Length <= 320 ? normalized : normalized[..320]);
        try
        {
            return Convert.ToHexString(HMACSHA256.HashData(_emailPartitionKey, bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private sealed class Counter(DateTimeOffset start)
    {
        internal DateTimeOffset Start { get; set; } = start;
        internal int Count { get; set; }
    }
}
