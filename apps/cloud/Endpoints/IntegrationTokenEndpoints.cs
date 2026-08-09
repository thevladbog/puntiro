using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Puntiro.Cloud.Configuration;
using Puntiro.Cloud.Http;
using Puntiro.Cloud.OpenApi;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Domain;

namespace Puntiro.Cloud.Endpoints;

public sealed record CreateIntegrationTokenRequest(string DisplayName, string[] Scopes)
{
    public override string ToString() => nameof(CreateIntegrationTokenRequest);
}

public sealed class IssuedIntegrationTokenResponse
{
    public required Guid Id { get; init; }
    public required string PublicId { get; init; }
    public required string DisplayName { get; init; }
    public required string[] Scopes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string Token { get; init; }
    public override string ToString() => nameof(IssuedIntegrationTokenResponse);
}

public sealed record IntegrationTokenResponse(
    Guid Id,
    string PublicId,
    string DisplayName,
    string[] Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    long Version);

public sealed record RevokeIntegrationTokenRequest(long ExpectedVersion, string Reason)
{
    public override string ToString() => nameof(RevokeIntegrationTokenRequest);
}

public static class IntegrationTokenEndpoints
{
    private static readonly TimeSpan StepUpFreshness = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapIntegrationTokenEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/integration-tokens")
            .WithTags("Integration Tokens")
            .RequireAuthorization(CloudServiceCollectionExtensions.AdminOwnerPolicy);
        group.MapGet("", ListAsync)
            .WithName("IntegrationTokenList")
            .Produces<IntegrationTokenResponse[]>()
            .Produces<ApiProblemDocument>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status429TooManyRequests, "application/problem+json")
            .WithMetadata(
                new ApiProblemResponseMetadata(
                    StatusCodes.Status401Unauthorized,
                    "auth.session_expired"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status403Forbidden,
                    "auth.forbidden"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status429TooManyRequests,
                    "auth.rate_limited"));
        group.MapPost("", CreateAsync)
            .WithName("IntegrationTokenCreate")
            .Accepts<CreateIntegrationTokenRequest>("application/json")
            .Produces<IssuedIntegrationTokenResponse>(StatusCodes.Status201Created)
            .Produces<ApiProblemDocument>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status429TooManyRequests, "application/problem+json")
            .WithMetadata(
                new ApiProblemResponseMetadata(
                    StatusCodes.Status400BadRequest,
                    "request.invalid"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status401Unauthorized,
                    "auth.session_expired"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status403Forbidden,
                    "auth.csrf_invalid",
                    "auth.forbidden",
                    "auth.step_up_required"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status409Conflict,
                    "integration_token.active_limit",
                    "integration_token.creation_conflict"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status429TooManyRequests,
                    "auth.rate_limited"));
        group.MapPost("/{id:guid}/revoke", RevokeAsync)
            .WithName("IntegrationTokenRevoke")
            .Accepts<RevokeIntegrationTokenRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiProblemDocument>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status429TooManyRequests, "application/problem+json")
            .WithMetadata(
                new ApiProblemResponseMetadata(
                    StatusCodes.Status400BadRequest,
                    "request.invalid"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status401Unauthorized,
                    "auth.session_expired"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status403Forbidden,
                    "auth.csrf_invalid",
                    "auth.forbidden"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status404NotFound,
                    "integration_token.not_found"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status409Conflict,
                    "integration_token.version_conflict"),
                new ApiProblemResponseMetadata(
                    StatusCodes.Status429TooManyRequests,
                    "auth.rate_limited"));
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        TenantContext tenant,
        IIntegrationTokenService tokens,
        AdminRateLimitService rateLimits,
        CancellationToken cancellationToken)
    {
        if (!rateLimits.TryIntegrationTokenAdministration(
                context.Connection.RemoteIpAddress,
                out var retryAfter))
        {
            return RateLimited(context, retryAfter);
        }

        if (!tenant.IsEstablished)
        {
            return SessionUnavailable(context);
        }

        var metadata = await tokens.ListAsync(tenant.OrganizationId, cancellationToken);
        return Results.Ok(metadata.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        TenantContext tenant,
        IIntegrationTokenService tokens,
        AdminRateLimitService rateLimits,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!rateLimits.TryIntegrationTokenAdministration(
                context.Connection.RemoteIpAddress,
                out var retryAfter))
        {
            return RateLimited(context, retryAfter);
        }

        if (!tenant.IsEstablished)
        {
            return SessionUnavailable(context);
        }

        var now = timeProvider.GetUtcNow();
        if (!TryReadSecondFactor(context, out var verifiedAt) ||
            verifiedAt > now ||
            verifiedAt < now - StepUpFreshness)
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status403Forbidden,
                "auth.step_up_required",
                "A recent TOTP verification is required.");
        }

        var request = await SensitiveBodyRedaction.ReadJsonAsync<CreateIntegrationTokenRequest>(
            context,
            cancellationToken);
        if (request is null || !TryParseScopes(request.Scopes, out var scopes))
        {
            return InvalidRequest(context);
        }

        try
        {
            var issued = await tokens.CreateAsync(
                new CreateIntegrationToken(
                    tenant.OrganizationId,
                    tenant.UserId,
                    request.DisplayName,
                    scopes),
                cancellationToken);
            return new IssuedIntegrationTokenResult(issued);
        }
        catch (ArgumentException)
        {
            return InvalidRequest(context);
        }
        catch (ActiveTokenLimitException)
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status409Conflict,
                "integration_token.active_limit",
                "The maximum number of active integration tokens has been reached.");
        }
        catch (IntegrationTokenCreationConflictException)
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status409Conflict,
                "integration_token.creation_conflict",
                "The integration token could not be created due to a concurrent change.");
        }
    }

    private static async Task<IResult> RevokeAsync(
        Guid id,
        HttpContext context,
        TenantContext tenant,
        IIntegrationTokenService tokens,
        AdminRateLimitService rateLimits,
        CancellationToken cancellationToken)
    {
        if (!rateLimits.TryIntegrationTokenAdministration(
                context.Connection.RemoteIpAddress,
                out var retryAfter))
        {
            return RateLimited(context, retryAfter);
        }

        if (!tenant.IsEstablished)
        {
            return SessionUnavailable(context);
        }

        var request = await SensitiveBodyRedaction.ReadJsonAsync<RevokeIntegrationTokenRequest>(
            context,
            cancellationToken);
        if (request is null || request.ExpectedVersion <= 0 ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 80)
        {
            return InvalidRequest(context);
        }

        try
        {
            await tokens.RevokeAsync(
                tenant.OrganizationId,
                id,
                tenant.UserId,
                request.ExpectedVersion,
                cancellationToken);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status404NotFound,
                "integration_token.not_found",
                "The integration token was not found.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status409Conflict,
                "integration_token.version_conflict",
                "The integration token changed before it could be revoked.");
        }
    }

    private static bool TryParseScopes(
        string[]? values,
        out IReadOnlySet<IntegrationScope> scopes)
    {
        scopes = new HashSet<IntegrationScope>();
        if (values is null || values.Length is 0 or > 2 || values.Distinct().Count() != values.Length)
        {
            return false;
        }

        var parsed = new HashSet<IntegrationScope>();
        foreach (var value in values)
        {
            if (!parsed.Add(value switch
                {
                    "shipments.read" => IntegrationScope.ShipmentsRead,
                    "shipments.write" => IntegrationScope.ShipmentsWrite,
                    _ => (IntegrationScope)(-1)
                }))
            {
                return false;
            }
        }

        if (parsed.Any(scope => scope is not (
                IntegrationScope.ShipmentsRead or IntegrationScope.ShipmentsWrite)))
        {
            return false;
        }

        scopes = parsed;
        return true;
    }

    private static IntegrationTokenResponse ToResponse(IntegrationTokenMetadata metadata) => new(
        metadata.Id,
        metadata.PublicId,
        metadata.DisplayName,
        metadata.Scopes.Select(ScopeValue).Order(StringComparer.Ordinal).ToArray(),
        metadata.CreatedAt,
        metadata.LastUsedAt,
        metadata.RevokedAt,
        metadata.Version);

    private static string ScopeValue(IntegrationScope scope) => scope switch
    {
        IntegrationScope.ShipmentsRead => "shipments.read",
        IntegrationScope.ShipmentsWrite => "shipments.write",
        _ => throw new InvalidOperationException("Unknown integration scope.")
    };

    private static bool TryReadSecondFactor(HttpContext context, out DateTimeOffset value) =>
        DateTimeOffset.TryParseExact(
            context.User.FindFirst(AdminClaimTypes.SecondFactorVerifiedAt)?.Value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value);

    private static IResult InvalidRequest(HttpContext context) => ApiProblem.Result(
        context,
        StatusCodes.Status400BadRequest,
        "request.invalid",
        "The request is invalid.");

    private static IResult SessionUnavailable(HttpContext context) => ApiProblem.Result(
        context,
        StatusCodes.Status401Unauthorized,
        "auth.session_expired",
        "The admin session is unavailable or expired.");

    private static IResult RateLimited(HttpContext context, int retryAfter)
    {
        context.Response.Headers.RetryAfter = retryAfter.ToString(
            CultureInfo.InvariantCulture);
        return ApiProblem.Result(
            context,
            StatusCodes.Status429TooManyRequests,
            "auth.rate_limited",
            "Too many Admin requests.");
    }

    private sealed class IssuedIntegrationTokenResult(IssuedIntegrationToken issued) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            using (issued)
            {
                var metadata = issued.Metadata;
                var response = new IssuedIntegrationTokenResponse
                {
                    Id = metadata.Id,
                    PublicId = metadata.PublicId,
                    DisplayName = metadata.DisplayName,
                    Scopes = metadata.Scopes
                        .Select(ScopeValue)
                        .Order(StringComparer.Ordinal)
                        .ToArray(),
                    CreatedAt = metadata.CreatedAt,
                    Token = issued.RawToken.Reveal()
                };
                httpContext.Response.Headers.Location =
                    $"/api/admin/integration-tokens/{metadata.Id:D}";
                httpContext.Response.StatusCode = StatusCodes.Status201Created;
                await httpContext.Response.WriteAsJsonAsync(
                    response,
                    httpContext.RequestAborted);
            }
        }

        public override string ToString() => nameof(IssuedIntegrationTokenResult);
    }
}
