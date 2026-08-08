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
