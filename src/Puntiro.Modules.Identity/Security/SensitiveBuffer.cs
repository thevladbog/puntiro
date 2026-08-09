using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Puntiro.Modules.Identity.Security;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class SensitiveBuffer<T> : IDisposable
    where T : unmanaged
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private T[]? _buffer;

    public SensitiveBuffer(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _buffer = new T[length];
    }

    public SensitiveBuffer(T[] ownedBuffer)
    {
        ArgumentNullException.ThrowIfNull(ownedBuffer);
        _buffer = ownedBuffer;
    }

    public Span<T> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_buffer is null, this);
            return _buffer;
        }
    }

    public ReadOnlySpan<T> ReadOnlySpan => Span;

    public void Dispose()
    {
        var buffer = _buffer;
        if (buffer is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(buffer.AsSpan()));
        _buffer = null;
    }

    public override string ToString() => "[REDACTED]";

    private string DebuggerDisplay => "[REDACTED]";
}
