using System.Diagnostics;

namespace Puntiro.Modules.Identity.Contracts;

public enum VerifiedFactor
{
    Totp,
    RecoveryCode
}

public sealed record VerifiedIdentity(Guid UserId, VerifiedFactor Factor, DateTimeOffset VerifiedAt);

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class AdminCredentials
{
    public AdminCredentials(string email, string password, string? totpCode, string? recoveryCode) =>
        (Email, Password, TotpCode, RecoveryCode) = (email, password, totpCode, recoveryCode);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string Email { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string Password { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string? TotpCode { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string? RecoveryCode { get; }

    public override string ToString() => nameof(AdminCredentials);

    private string DebuggerDisplay => nameof(AdminCredentials);
}

public interface IAdminAuthenticationService
{
    Task<VerifiedIdentity?> VerifyAsync(
        AdminCredentials credentials,
        CancellationToken cancellationToken);

    Task<DateTimeOffset?> StepUpTotpAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken);
}
