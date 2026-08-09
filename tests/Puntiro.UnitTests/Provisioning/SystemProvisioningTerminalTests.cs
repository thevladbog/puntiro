using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Puntiro.Provisioning.Cli;
using Xunit;

namespace Puntiro.UnitTests.Provisioning;

public sealed class SystemProvisioningTerminalTests
{
    [Fact]
    public async Task Redirected_subprocess_fails_generically_without_echoing_identifiers()
    {
        var executable = Path.GetFullPath(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(),
            "..",
            "..",
            "..",
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        var assembly = typeof(ProvisioningArguments).Assembly.Location;
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add(assembly);
        process.StartInfo.ArgumentList.Add("bootstrap-owner");
        process.StartInfo.ArgumentList.Add("--organization-name");
        process.StartInfo.ArgumentList.Add("private-identifier-marker");
        process.StartInfo.ArgumentList.Add("--organization-slug");
        process.StartInfo.ArgumentList.Add("private-slug-marker");
        process.StartInfo.ArgumentList.Add("--email");
        process.StartInfo.ArgumentList.Add("private-email-marker@example.test");

        Assert.True(process.Start());
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal((int)ProvisioningExit.InfrastructureFailure, process.ExitCode);
        Assert.Empty(stdout);
        Assert.DoesNotContain("private-", stderr, StringComparison.Ordinal);
        Assert.Equal(
            "Provisioning could not run. Verify the secure terminal and deployment configuration.\n",
            stderr.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Hidden_input_intercepts_echo_and_clears_the_rented_buffer()
    {
        var console = new FakeProvisioningConsole([Key('a'), Key('b'), Enter()]);
        var buffers = new TrackingCharPool();
        var terminal = new SystemProvisioningTerminal(console, buffers, Task.Delay);

        using var value = await terminal.ReadPasswordAsync(TestContext.Current.CancellationToken);

        Assert.True(value.Use(span => span.SequenceEqual("ab")), "The hidden input value was incorrect.");
        Assert.All(console.InterceptArguments, Assert.True);
        Assert.Equal("Password: \n", console.Output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.True(buffers.Returned);
        Assert.All(buffers.Buffer, item => Assert.Equal('\0', item));
    }

    [Fact]
    public async Task Cancellation_clears_a_partially_filled_buffer()
    {
        using var cancellation = new CancellationTokenSource();
        var console = new FakeProvisioningConsole([Key('x')], keyAvailableAfterQueue: false);
        var buffers = new TrackingCharPool();
        Task CancelDelay(TimeSpan _, CancellationToken token)
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        }
        var terminal = new SystemProvisioningTerminal(console, buffers, CancelDelay);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => terminal.ReadPasswordAsync(cancellation.Token));

        Assert.True(buffers.Returned);
        Assert.All(buffers.Buffer, item => Assert.Equal('\0', item));
    }

    [Fact]
    public async Task Input_beyond_the_fixed_bound_fails_and_clears_the_buffer()
    {
        var keys = Enumerable.Repeat(Key('x'), 300).Append(Enter()).ToArray();
        var console = new FakeProvisioningConsole(keys);
        var buffers = new TrackingCharPool();
        var terminal = new SystemProvisioningTerminal(console, buffers, Task.Delay);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => terminal.ReadPasswordAsync(TestContext.Current.CancellationToken));

        Assert.True(buffers.Returned);
        Assert.All(buffers.Buffer, item => Assert.Equal('\0', item));
    }

    private static ConsoleKeyInfo Key(char value) => new(value, ConsoleKey.A, false, false, false);

    private static ConsoleKeyInfo Enter() => new('\r', ConsoleKey.Enter, false, false, false);
}

internal sealed class FakeProvisioningConsole(
    IReadOnlyList<ConsoleKeyInfo> keys,
    bool keyAvailableAfterQueue = true) : IProvisioningConsole
{
    private int _index;

    public bool IsInputRedirected => false;
    public bool IsOutputRedirected => false;
    public bool KeyAvailable => _index < keys.Count || keyAvailableAfterQueue;
    public StringBuilder Output { get; } = new();
    public List<bool> InterceptArguments { get; } = [];

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        InterceptArguments.Add(intercept);
        return keys[_index++];
    }

    public void Write(string value) => Output.Append(value);
    public void WriteLine() => Output.AppendLine();
    public void WriteLine(string value) => Output.AppendLine(value);
    public void WriteSecretLine(ReadOnlySpan<char> value)
    {
        Output.Append(value);
        Output.AppendLine();
    }
}

internal sealed class TrackingCharPool : ArrayPool<char>
{
    public char[] Buffer { get; private set; } = [];
    public bool Returned { get; private set; }

    public override char[] Rent(int minimumLength)
    {
        Buffer = new char[minimumLength];
        return Buffer;
    }

    public override void Return(char[] array, bool clearArray = false)
    {
        Assert.Same(Buffer, array);
        Returned = true;
    }
}
