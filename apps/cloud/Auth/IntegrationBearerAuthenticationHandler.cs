using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Puntiro.Cloud.Configuration;
using Puntiro.Cloud.Http;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Domain;
using Puntiro.Modules.Tenancy.Contracts;

namespace Puntiro.Cloud.Auth;

public sealed class IntegrationBearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IIntegrationTokenService tokens,
    ITenantAccessService tenantAccess,
    IntegrationBearerRateLimitService rateLimits)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "IntegrationToken";
    private const string Prefix = "pnt_live_";
    private const int TokenLength = 75;
    private static readonly object RateLimitedItem = new();

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryReadToken(out var presentedToken))
        {
            ApplyUnknownLimit();
            return AuthenticateResult.Fail("Integration credential is unavailable.");
        }

        var principal = await tokens.AuthenticateAsync(presentedToken, Context.RequestAborted);
        if (principal is null ||
            !await tenantAccess.IsOrganizationActiveAsync(
                principal.OrganizationId,
                Context.RequestAborted))
        {
            ApplyUnknownLimit();
            return AuthenticateResult.Fail("Integration credential is unavailable.");
        }

        var publicId = presentedToken.Substring(Prefix.Length, 22);
        if (!rateLimits.TryVerified(
                Request.HttpContext.Connection.RemoteIpAddress,
                publicId,
                out var retryAfter))
        {
            MarkRateLimited(retryAfter);
            return AuthenticateResult.Fail("Integration request is rate limited.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, principal.TokenId.ToString("D")),
            new(IntegrationClaimTypes.TokenId, principal.TokenId.ToString("D")),
            new(IntegrationClaimTypes.OrganizationId, principal.OrganizationId.ToString("D"))
        };
        claims.AddRange(principal.Scopes.Select(scope => new Claim(
            IntegrationClaimTypes.Scope,
            IntegrationClaimTypes.ScopeValue(scope))));
        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            AuthenticationScheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Context.Items.TryGetValue(RateLimitedItem, out var retry) && retry is int seconds)
        {
            Context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            return ApiProblem.WriteAsync(
                Context,
                StatusCodes.Status429TooManyRequests,
                "integration.rate_limited",
                "Too many integration requests.");
        }

        return ApiProblem.WriteAsync(
            Context,
            StatusCodes.Status401Unauthorized,
            "integration.invalid_credentials",
            "The integration credential is unavailable or invalid.");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ApiProblem.WriteAsync(
            Context,
            StatusCodes.Status403Forbidden,
            "integration.scope_forbidden",
            "The integration credential does not grant the required scope.");

    private bool TryReadToken(out string token)
    {
        token = string.Empty;
        if (Request.Headers.Authorization.Count != 1)
        {
            return false;
        }

        var header = Request.Headers.Authorization[0];
        if (header is null || header.Length != "Bearer ".Length + TokenLength ||
            !header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }

        token = header["Bearer ".Length..];
        return token.StartsWith(Prefix, StringComparison.Ordinal);
    }

    private void ApplyUnknownLimit()
    {
        if (!rateLimits.TryUnknown(
                Request.HttpContext.Connection.RemoteIpAddress,
                out var retryAfter))
        {
            MarkRateLimited(retryAfter);
        }
    }

    private void MarkRateLimited(int retryAfter) => Context.Items[RateLimitedItem] = retryAfter;
}

internal static class IntegrationClaimTypes
{
    internal const string TokenId = "puntiro:integration_token_id";
    internal const string OrganizationId = "puntiro:organization_id";
    internal const string Scope = "puntiro:integration_scope";

    internal static string ScopeValue(IntegrationScope scope) => scope switch
    {
        IntegrationScope.ShipmentsRead => "shipments.read",
        IntegrationScope.ShipmentsWrite => "shipments.write",
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    internal static bool TryReadPrincipal(
        ClaimsPrincipal principal,
        out Guid tokenId,
        out Guid organizationId)
    {
        tokenId = Guid.Empty;
        organizationId = Guid.Empty;
        var identity = principal.Identities.SingleOrDefault(candidate => string.Equals(
            candidate.AuthenticationType,
            IntegrationBearerAuthenticationHandler.AuthenticationScheme,
            StringComparison.Ordinal));
        return identity is not null &&
            Guid.TryParse(identity.FindFirst(TokenId)?.Value, out tokenId) &&
            Guid.TryParse(identity.FindFirst(OrganizationId)?.Value, out organizationId);
    }
}

public sealed class IntegrationBearerRateLimitService(
    PuntiroCloudOptions options,
    TimeProvider timeProvider) : IDisposable
{
    private const int PermitLimit = 120;
    private readonly byte[] _partitionKey = RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentDictionary<string, Counter> _partitions = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    internal bool TryVerified(IPAddress? address, string publicId, out int retryAfter) =>
        TryAcquire($"verified:{Peer(address)}:{Hash(publicId)}", out retryAfter);

    internal bool TryUnknown(IPAddress? address, out int retryAfter) =>
        TryAcquire($"unknown:{Peer(address)}", out retryAfter);

    public void Dispose() => CryptographicOperations.ZeroMemory(_partitionKey);

    private bool TryAcquire(string partition, out int retryAfter)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            var window = TimeSpan.FromSeconds(options.Security.RateLimitWindowSeconds);
            if (_partitions.Count >= options.Security.MaximumRateLimitPartitions)
            {
                foreach (var stale in _partitions
                             .Where(item => item.Value.Start + window <= now)
                             .Take(Math.Max(1, _partitions.Count / 10))
                             .ToArray())
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

            if (counter.Count >= PermitLimit)
            {
                retryAfter = Math.Max(
                    1,
                    (int)Math.Ceiling((counter.Start + window - now).TotalSeconds));
                return false;
            }

            counter.Count++;
            retryAfter = 0;
            return true;
        }
    }

    private string Hash(string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        try
        {
            return Convert.ToHexString(HMACSHA256.HashData(_partitionKey, bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string Peer(IPAddress? address) => address?.ToString() ?? "unknown";

    private sealed class Counter(DateTimeOffset start)
    {
        internal DateTimeOffset Start { get; set; } = start;
        internal int Count { get; set; }
    }
}
