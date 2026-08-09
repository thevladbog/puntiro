using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Puntiro.Cloud.Auth;
using Puntiro.Cloud.Configuration;
using Puntiro.Cloud.Http;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Tenancy.Contracts;

namespace Puntiro.Cloud.Endpoints;

public sealed class AdminLoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public string? TotpCode { get; init; }
    public string? RecoveryCode { get; init; }
    public override string ToString() => nameof(AdminLoginRequest);
}

public sealed class StepUpRequest
{
    public required string TotpCode { get; init; }
    public override string ToString() => nameof(StepUpRequest);
}

public sealed record AdminSessionResponse(
    Guid UserId,
    string Email,
    Guid OrganizationId,
    string Role,
    DateTimeOffset IdleExpiresAt,
    DateTimeOffset AbsoluteExpiresAt,
    DateTimeOffset? SecondFactorVerifiedAt,
    string AntiforgeryToken);

public static class AdminAuthEndpoints
{
    public static IEndpointRouteBuilder MapAdminAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/auth").WithTags("Admin Authentication");
        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("AdminAuthLogin")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiProblemDocument>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status429TooManyRequests, "application/problem+json");
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization(CloudServiceCollectionExtensions.AdminOwnerPolicy)
            .WithName("AdminAuthLogout")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiProblemDocument>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status403Forbidden, "application/problem+json");
        group.MapGet("/session", SessionAsync)
            .RequireAuthorization(CloudServiceCollectionExtensions.AdminOwnerPolicy)
            .WithName("AdminAuthSession")
            .Produces<AdminSessionResponse>()
            .Produces<ApiProblemDocument>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status403Forbidden, "application/problem+json");
        group.MapPost("/step-up", StepUpAsync)
            .RequireAuthorization(CloudServiceCollectionExtensions.AdminOwnerPolicy)
            .WithName("AdminAuthStepUp")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiProblemDocument>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemDocument>(StatusCodes.Status429TooManyRequests, "application/problem+json");
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        IAdminAuthenticationService authentication,
        IAdminSessionService sessions,
        ITenantAccessService access,
        AdminRateLimitService rateLimits,
        CancellationToken cancellationToken)
    {
        var request = await SensitiveBodyRedaction.ReadJsonAsync<AdminLoginRequest>(
            context,
            cancellationToken);
        if (request is null || !ValidLoginShape(request))
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status400BadRequest,
                "request.invalid",
                "The request is invalid.");
        }

        if (!rateLimits.TryLogin(context.Connection.RemoteIpAddress, request.Email, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
            return ApiProblem.Result(
                context,
                StatusCodes.Status429TooManyRequests,
                "auth.rate_limited",
                "Too many authentication attempts.");
        }

        var audit = new IdentityAuditContext(null, ApiProblem.SafeTraceId(context));
        var verified = await authentication.VerifyAsync(
            new AdminCredentials(request.Email, request.Password, request.TotpCode, request.RecoveryCode),
            audit,
            cancellationToken);
        if (verified is null)
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status401Unauthorized,
                "auth.invalid_credentials",
                "The supplied credentials are invalid.");
        }

        TenantAccess? tenant;
        try
        {
            tenant = await access.FindSingleActiveMembershipAsync(verified.UserId, cancellationToken);
        }
        catch (OrganizationSelectionRequiredException)
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status409Conflict,
                "auth.organization_selection_required",
                "An organization must be selected before signing in.");
        }

        if (tenant is null || tenant.Role != Puntiro.Modules.Tenancy.Domain.MembershipRole.Owner)
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status403Forbidden,
                "auth.forbidden",
                "The operation is not allowed.");
        }

        using var issued = await sessions.CreateAsync(
            verified,
            tenant.OrganizationId,
            new IdentityAuditContext(verified.UserId, ApiProblem.SafeTraceId(context)),
            cancellationToken);
        context.Response.Cookies.Append(
            AdminSessionAuthenticationHandler.CookieName,
            issued.RawToken.Reveal(),
            SessionCookie(issued.Principal.AbsoluteExpiresAt));
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAdminSessionService sessions,
        CancellationToken cancellationToken)
    {
        if (!AdminClaimTypes.TryReadIds(context.User, out var userId, out var sessionId, out _))
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status401Unauthorized,
                "auth.session_expired",
                "The admin session is unavailable or expired.");
        }

        await sessions.RevokeAsync(
            sessionId,
            "logout",
            new IdentityAuditContext(userId, ApiProblem.SafeTraceId(context)),
            cancellationToken);
        context.Response.Cookies.Delete(
            AdminSessionAuthenticationHandler.CookieName,
            SessionCookie(expiresAt: null));
        return Results.NoContent();
    }

    private static IResult SessionAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (!AdminClaimTypes.TryReadIds(context.User, out var userId, out _, out var organizationId) ||
            !TryReadDate(context.User, AdminClaimTypes.IdleExpiresAt, out var idle) ||
            !TryReadDate(context.User, AdminClaimTypes.AbsoluteExpiresAt, out var absolute))
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status401Unauthorized,
                "auth.session_expired",
                "The admin session is unavailable or expired.");
        }

        var tokens = antiforgery.GetAndStoreTokens(context);
        var secondFactor = TryReadDate(
            context.User,
            AdminClaimTypes.SecondFactorVerifiedAt,
            out var verifiedAt)
            ? verifiedAt
            : (DateTimeOffset?)null;
        return Results.Ok(new AdminSessionResponse(
            userId,
            context.User.FindFirstValue(AdminClaimTypes.Email) ?? string.Empty,
            organizationId,
            "owner",
            idle,
            absolute,
            secondFactor,
            tokens.RequestToken ?? throw new InvalidOperationException("Antiforgery token was not issued.")));
    }

    private static async Task<IResult> StepUpAsync(
        HttpContext context,
        IAdminAuthenticationService authentication,
        IAdminSessionService sessions,
        AdminRateLimitService rateLimits,
        CancellationToken cancellationToken)
    {
        if (!AdminClaimTypes.TryReadIds(context.User, out var userId, out var sessionId, out _))
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status401Unauthorized,
                "auth.session_expired",
                "The admin session is unavailable or expired.");
        }

        var request = await SensitiveBodyRedaction.ReadJsonAsync<StepUpRequest>(context, cancellationToken);
        if (request is null || !IsTotp(request.TotpCode))
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status400BadRequest,
                "request.invalid",
                "The request is invalid.");
        }

        if (!rateLimits.TryStepUp(sessionId, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
            return ApiProblem.Result(
                context,
                StatusCodes.Status429TooManyRequests,
                "auth.rate_limited",
                "Too many authentication attempts.");
        }

        var audit = new IdentityAuditContext(userId, ApiProblem.SafeTraceId(context));
        var verifiedAt = await authentication.StepUpTotpAsync(
            userId,
            request.TotpCode,
            audit,
            cancellationToken);
        if (verifiedAt is null)
        {
            return ApiProblem.Result(
                context,
                StatusCodes.Status403Forbidden,
                "auth.forbidden",
                "The operation is not allowed.");
        }

        var updated = await sessions.RecordStepUpAsync(
            sessionId,
            verifiedAt.Value,
            audit,
            cancellationToken);
        return updated is null
            ? ApiProblem.Result(
                context,
                StatusCodes.Status401Unauthorized,
                "auth.session_expired",
                "The admin session is unavailable or expired.")
            : Results.NoContent();
    }

    private static bool ValidLoginShape(AdminLoginRequest request)
    {
        var factorCount = (string.IsNullOrEmpty(request.TotpCode) ? 0 : 1) +
            (string.IsNullOrEmpty(request.RecoveryCode) ? 0 : 1);
        return request.Email is { Length: > 0 and <= 1024 } &&
            request.Password is { Length: > 0 and <= 1024 } &&
            factorCount == 1 &&
            (request.TotpCode is null || IsTotp(request.TotpCode)) &&
            (request.RecoveryCode is null || request.RecoveryCode.Length is >= 8 and <= 128);
    }

    private static bool IsTotp(string? code) =>
        code is { Length: 6 } && code.All(static value => value is >= '0' and <= '9');

    private static CookieOptions SessionCookie(DateTimeOffset? expiresAt) => new()
    {
        Secure = true,
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expiresAt,
        IsEssential = true
    };

    private static bool TryReadDate(
        ClaimsPrincipal principal,
        string claimType,
        out DateTimeOffset value) =>
        DateTimeOffset.TryParseExact(
            principal.FindFirstValue(claimType),
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value);
}
