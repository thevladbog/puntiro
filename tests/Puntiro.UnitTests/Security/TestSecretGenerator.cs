using Puntiro.Security;

namespace Puntiro.UnitTests.Security;

internal sealed class TestSecretGenerator : ISecretGenerator
{
    private readonly Queue<byte[]> _values;

    public TestSecretGenerator(params byte[][] values)
    {
        _values = new Queue<byte[]>(values.Select(static value => (byte[])value.Clone()));
    }

    public void Fill(Span<byte> destination)
    {
        if (!_values.TryDequeue(out var value))
        {
            throw new InvalidOperationException("No deterministic secret remains for this test.");
        }

        if (value.Length != destination.Length)
        {
            throw new InvalidOperationException("The deterministic secret has an unexpected length.");
        }

        value.CopyTo(destination);
    }
}

internal sealed class RepeatingSecretGenerator : ISecretGenerator
{
    private readonly byte[] _value;
    private readonly int _maximumDraws;
    private int _draws;

    public RepeatingSecretGenerator(byte[] value, int maximumDraws = 100)
    {
        _value = (byte[])value.Clone();
        _maximumDraws = maximumDraws;
    }

    public void Fill(Span<byte> destination)
    {
        _draws++;
        if (_draws > _maximumDraws)
        {
            throw new TestSecretLimitExceededException();
        }

        if (_value.Length != destination.Length)
        {
            throw new InvalidOperationException("The deterministic secret has an unexpected length.");
        }

        _value.CopyTo(destination);
    }
}

internal sealed class TestSecretLimitExceededException : Exception;
