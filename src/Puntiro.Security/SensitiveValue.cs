using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Puntiro.Security;

public delegate TResult SensitiveValueReader<TResult>(ReadOnlySpan<char> value);

[DebuggerDisplay("{DebuggerDisplay,nq}")]
[DebuggerTypeProxy(typeof(SensitiveValueDebugView))]
[JsonConverter(typeof(SensitiveValueJsonConverter))]
public sealed class SensitiveValue : IDisposable
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private char[]? _buffer;

    private readonly object _sync = new();

    /// <summary>
    /// Copies the value into owned mutable storage. The caller-provided managed string remains
    /// caller-owned and cannot be cleared by this instance.
    /// </summary>
    public SensitiveValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _buffer = value.ToCharArray();
    }

    public SensitiveValue(ReadOnlySpan<char> value)
    {
        _buffer = value.ToArray();
    }

    /// <summary>
    /// Creates a caller-owned immutable copy for the narrow response/cookie boundary.
    /// Prefer <see cref="Use{TResult}"/> when a scoped span consumer is sufficient.
    /// </summary>
    public string Reveal() => Use(static value => new string(value));

    public TResult Use<TResult>(SensitiveValueReader<TResult> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_buffer is null, this);
            return reader(_buffer);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_buffer is null)
            {
                return;
            }

            Array.Clear(_buffer);
            _buffer = null;
        }
    }

    public override string ToString() => "[REDACTED]";

    private string DebuggerDisplay => "[REDACTED]";

    private sealed class SensitiveValueDebugView
    {
        public SensitiveValueDebugView(SensitiveValue value)
        {
            ArgumentNullException.ThrowIfNull(value);
        }

        public string Value => "[REDACTED]";
    }
}

public sealed class SensitiveValueJsonConverter : JsonConverter<SensitiveValue>
{
    public override SensitiveValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        throw new JsonException("Sensitive values cannot be created through JSON deserialization.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        SensitiveValue value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue("[REDACTED]");
    }
}
