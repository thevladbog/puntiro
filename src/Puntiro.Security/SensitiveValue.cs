using System.Diagnostics;

namespace Puntiro.Security;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
[DebuggerTypeProxy(typeof(SensitiveValueDebugView))]
public sealed class SensitiveValue
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string _value;

    public SensitiveValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    public string Reveal() => _value;

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
