using System.Buffers;
using Puntiro.Security;

namespace Puntiro.Provisioning.Cli;

public sealed class SystemProvisioningTerminal : IProvisioningTerminal
{
    private const int MaximumPasswordLength = 256;
    private const int MaximumRecoveryCodeLength = 128;
    private const int MaximumTotpLength = 16;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private readonly IProvisioningConsole _console;
    private readonly ArrayPool<char> _buffers;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public SystemProvisioningTerminal() : this(
        new SystemProvisioningConsole(),
        ArrayPool<char>.Shared,
        Task.Delay)
    {
    }

    internal SystemProvisioningTerminal(
        IProvisioningConsole console,
        ArrayPool<char> buffers,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _buffers = buffers ?? throw new ArgumentNullException(nameof(buffers));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
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

        _console.WriteLine("TOTP enrollment URI (secret, shown once):");
        totpUri.Use(value =>
        {
            _console.WriteSecretLine(value);
            return 0;
        });
        _console.WriteLine("Recovery codes (secret, shown once):");
        foreach (var recoveryCode in recoveryCodes)
        {
            recoveryCode.Use(value =>
            {
                _console.WriteSecretLine(value);
                return 0;
            });
        }

        return Task.CompletedTask;
    }

    private async Task<SensitiveValue> ReadHiddenAsync(
        string prompt,
        int maximumLength,
        CancellationToken cancellationToken)
    {
        EnsureInteractive();
        _console.Write(prompt);
        var buffer = _buffers.Rent(maximumLength);
        var length = 0;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_console.KeyAvailable)
                {
                    await _delay(PollInterval, cancellationToken);
                    continue;
                }

                var key = _console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    _console.WriteLine();
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

                if (length == maximumLength)
                {
                    throw new InvalidOperationException("A supplied secret exceeds its allowed length.");
                }

                buffer[length++] = key.KeyChar;
            }
        }
        finally
        {
            Array.Clear(buffer);
            _buffers.Return(buffer, clearArray: true);
        }
    }

    private void EnsureInteractive()
    {
        if (_console.IsInputRedirected || _console.IsOutputRedirected)
        {
            throw new InvalidOperationException(
                "Provisioning requires an interactive terminal with direct input and output.");
        }
    }
}

internal interface IProvisioningConsole
{
    bool IsInputRedirected { get; }
    bool IsOutputRedirected { get; }
    bool KeyAvailable { get; }
    ConsoleKeyInfo ReadKey(bool intercept);
    void Write(string value);
    void WriteLine();
    void WriteLine(string value);
    void WriteSecretLine(ReadOnlySpan<char> value);
}

internal sealed class SystemProvisioningConsole : IProvisioningConsole
{
    public bool IsInputRedirected => Console.IsInputRedirected;
    public bool IsOutputRedirected => Console.IsOutputRedirected;
    public bool KeyAvailable => Console.KeyAvailable;

    public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);

    public void Write(string value)
    {
        Console.Out.Write(value);
        Console.Out.Flush();
    }

    public void WriteLine()
    {
        Console.Out.WriteLine();
        Console.Out.Flush();
    }

    public void WriteLine(string value)
    {
        Console.Out.WriteLine(value);
        Console.Out.Flush();
    }

    public void WriteSecretLine(ReadOnlySpan<char> value)
    {
        Console.Out.WriteLine(value);
        Console.Out.Flush();
    }
}
