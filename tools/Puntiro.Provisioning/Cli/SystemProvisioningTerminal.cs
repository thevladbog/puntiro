using Puntiro.Security;

namespace Puntiro.Provisioning.Cli;

public sealed class SystemProvisioningTerminal : IProvisioningTerminal
{
    private const int MaximumPasswordLength = 256;
    private const int MaximumRecoveryCodeLength = 128;
    private const int MaximumTotpLength = 16;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    public SystemProvisioningTerminal()
    {
        EnsureInteractive();
    }

    public Task<SensitiveValue> ReadPasswordAsync(CancellationToken cancellationToken) =>
        ReadHiddenAsync("Password: ", MaximumPasswordLength, cancellationToken);

    public Task<SensitiveValue> ReadRecoveryCodeAsync(CancellationToken cancellationToken) =>
        ReadHiddenAsync("Unused recovery code: ", MaximumRecoveryCodeLength, cancellationToken);

    public Task<SensitiveValue> ReadTotpAsync(CancellationToken cancellationToken) =>
        ReadHiddenAsync("First TOTP code: ", MaximumTotpLength, cancellationToken);

    public Task WriteEnrollmentAsync(
        SensitiveValue totpUri,
        IReadOnlyList<SensitiveValue> recoveryCodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(totpUri);
        ArgumentNullException.ThrowIfNull(recoveryCodes);
        EnsureInteractive();
        cancellationToken.ThrowIfCancellationRequested();

        Console.Out.WriteLine("TOTP enrollment URI (secret, shown once):");
        totpUri.Use(value =>
        {
            Console.Out.WriteLine(value);
            return 0;
        });
        Console.Out.WriteLine("Recovery codes (secret, shown once):");
        foreach (var recoveryCode in recoveryCodes)
        {
            recoveryCode.Use(value =>
            {
                Console.Out.WriteLine(value);
                return 0;
            });
        }

        Console.Out.Flush();
        return Task.CompletedTask;
    }

    private static async Task<SensitiveValue> ReadHiddenAsync(
        string prompt,
        int maximumLength,
        CancellationToken cancellationToken)
    {
        EnsureInteractive();
        Console.Out.Write(prompt);
        Console.Out.Flush();
        var buffer = new char[maximumLength];
        var length = 0;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Console.KeyAvailable)
                {
                    await Task.Delay(PollInterval, cancellationToken);
                    continue;
                }

                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.Out.WriteLine();
                    if (length == 0)
                    {
                        throw new InvalidOperationException("A required secret was not supplied.");
                    }

                    return new SensitiveValue(buffer.AsSpan(0, length));
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (length > 0)
                    {
                        buffer[--length] = '\0';
                    }

                    continue;
                }

                if (char.IsControl(key.KeyChar))
                {
                    continue;
                }

                if (length == buffer.Length)
                {
                    throw new InvalidOperationException("A supplied secret exceeds its allowed length.");
                }

                buffer[length++] = key.KeyChar;
            }
        }
        finally
        {
            Array.Clear(buffer);
        }
    }

    private static void EnsureInteractive()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            throw new InvalidOperationException(
                "Provisioning requires an interactive terminal with direct input and output.");
        }
    }
}
