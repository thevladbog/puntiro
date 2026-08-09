using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Puntiro.Cloud.Http;
using Puntiro.Modules.Identity.Contracts;

namespace Puntiro.Cloud.Auth;

public sealed class AdminSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAdminSessionService sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "AdminSession";
    public const string CookieName = "__Host-puntiro_session";
    private const int MaximumTokenLength = 256;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.Cookie.Count != 1 ||
            CountSessionCookies(Request.Headers.Cookie[0]) != 1 ||
            !Request.Cookies.TryGetValue(CookieName, out var token) ||
            string.IsNullOrEmpty(token) || token.Length > MaximumTokenLength)
        {
            return AuthenticateResult.NoResult();
        }

        var principal = await sessions.ValidateAsync(token, Context.RequestAborted);
        if (principal is null)
        {
            return AuthenticateResult.Fail("Session is unavailable.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, principal.UserId.ToString("D")),
            new(AdminClaimTypes.SessionId, principal.SessionId.ToString("D")),
            new(AdminClaimTypes.OrganizationId, principal.OrganizationId.ToString("D")),
            new(AdminClaimTypes.Email, principal.Email),
            new(AdminClaimTypes.IdleExpiresAt, principal.IdleExpiresAt.ToString("O", CultureInfo.InvariantCulture)),
            new(AdminClaimTypes.AbsoluteExpiresAt, principal.AbsoluteExpiresAt.ToString("O", CultureInfo.InvariantCulture))
        };
        if (principal.SecondFactorVerifiedAt is { } verifiedAt)
        {
            claims.Add(new Claim(
                AdminClaimTypes.SecondFactorVerifiedAt,
                verifiedAt.ToString("O", CultureInfo.InvariantCulture)));
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            AuthenticationScheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ApiProblem.WriteAsync(
            Context,
            StatusCodes.Status401Unauthorized,
            "auth.session_expired",
            "The admin session is unavailable or expired.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ApiProblem.WriteAsync(
            Context,
            StatusCodes.Status403Forbidden,
            "auth.forbidden",
            "The operation is not allowed.");

    private static int CountSessionCookies(string? header)
    {
        if (string.IsNullOrEmpty(header) || header.Length > 4096)
        {
            return 0;
        }

        var count = 0;
        foreach (var segment in header.Split(';', StringSplitOptions.TrimEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator > 0 &&
                string.Equals(segment[..separator], CookieName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
