using Puntiro.Security;

namespace Puntiro.Provisioning.Cli;

public interface IProvisioningTerminal
{
    Task<SensitiveValue> ReadPasswordAsync(CancellationToken cancellationToken);

    Task<SensitiveValue> ReadRecoveryCodeAsync(CancellationToken cancellationToken);

    Task<SensitiveValue> ReadTotpAsync(CancellationToken cancellationToken);

    Task WriteEnrollmentAsync(
        SensitiveValue totpUri,
        IReadOnlyList<SensitiveValue> recoveryCodes,
        CancellationToken cancellationToken);
}
