using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Puntiro.Modules.Identity.Contracts;

public enum VerifiedFactor
{
    Totp,
    RecoveryCode
}

public sealed record VerifiedIdentity(
    Guid UserId,
    long AuthenticationEpoch,
    VerifiedFactor Factor,
    DateTimeOffset VerifiedAt);

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class AdminCredentials
{
    public AdminCredentials(string email, string password, string? totpCode, string? recoveryCode) =>
        (Email, Password, TotpCode, RecoveryCode) = (email, password, totpCode, recoveryCode);

    [DebuggerBrowsable(DebuggerBrowsableState.Never), JsonIgnore]
    public string Email { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never), JsonIgnore]
    public string Password { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never), JsonIgnore]
    public string? TotpCode { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never), JsonIgnore]
    public string? RecoveryCode { get; }

    public override string ToString() => nameof(AdminCredentials);

    private string DebuggerDisplay => nameof(AdminCredentials);
}

public interface IAdminAuthenticationService
{
    Task<VerifiedIdentity?> VerifyAsync(
        AdminCredentials credentials,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken);

    Task<DateTimeOffset?> StepUpTotpAsync(
        Guid userId,
        string code,
        IdentityAuditContext auditContext,
        CancellationToken cancellationToken);
}
