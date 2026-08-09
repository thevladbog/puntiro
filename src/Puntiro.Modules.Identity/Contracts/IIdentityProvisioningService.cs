using System.Diagnostics;
using Puntiro.Security;

namespace Puntiro.Modules.Identity.Contracts;

public sealed class PendingOwnerIdentity : IDisposable
{
    public PendingOwnerIdentity(
        Guid userId,
        string displayEmail,
        SensitiveValue totpUri,
        IReadOnlyList<SensitiveValue> recoveryCodes)
    {
        UserId = userId;
        DisplayEmail = displayEmail;
        TotpUri = totpUri;
        RecoveryCodes = recoveryCodes;
    }

    public Guid UserId { get; }
    public string DisplayEmail { get; }
    public SensitiveValue TotpUri { get; }
    public IReadOnlyList<SensitiveValue> RecoveryCodes { get; }

    public void Dispose()
    {
        TotpUri.Dispose();
        foreach (var code in RecoveryCodes)
        {
            code.Dispose();
        }
    }
}

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class PendingOwnerTotpReset : IDisposable
{
    private bool _disposed;

    public required Guid UserId { get; init; }
    public required SensitiveValue TotpUri { get; init; }
    public required IReadOnlyList<SensitiveValue> RecoveryCodes { get; init; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal byte[] CandidateSecret { get; init; } = Array.Empty<byte>();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal IReadOnlyList<byte[]> CandidateRecoveryVerifiers { get; init; } = [];
    internal Guid CandidateBatchId { get; init; }
    internal Guid VerifiedRecoveryCodeId { get; init; }
    internal long VerifiedRecoveryCodeVersion { get; init; }
    internal string RecoveryKeyVersion { get; init; } = string.Empty;

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        TotpUri.Dispose();
        foreach (var code in RecoveryCodes)
        {
            code.Dispose();
        }

        System.Security.Cryptography.CryptographicOperations.ZeroMemory(CandidateSecret);
        foreach (var verifier in CandidateRecoveryVerifiers)
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(verifier);
        }

        _disposed = true;
    }

    public override string ToString() => nameof(PendingOwnerTotpReset);

    private string DebuggerDisplay => nameof(PendingOwnerTotpReset);
}

public interface IIdentityProvisioningService
{
    Task<PendingOwnerIdentity> BeginOwnerAsync(
        Guid provisioningOrganizationId,
        string email,
        string password,
        CancellationToken cancellationToken);

    Task ConfirmOwnerTotpAsync(Guid userId, string code, CancellationToken cancellationToken);

    Task CompleteOwnerAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);

    Task<PendingOwnerTotpReset> PrepareOwnerTotpResetAsync(
        Guid userId,
        string password,
        string recoveryCode,
        CancellationToken cancellationToken);

    Task CompleteOwnerTotpResetAsync(
        PendingOwnerTotpReset pending,
        string firstTotpCode,
        CancellationToken cancellationToken);
}
