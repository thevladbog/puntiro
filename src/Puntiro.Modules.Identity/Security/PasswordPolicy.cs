using System.Buffers;
using System.Text;

namespace Puntiro.Modules.Identity.Security;

internal static class PasswordPolicy
{
    private const int MinimumScalarCount = 12;
    private const int MaximumScalarCount = 128;
    private const int MaximumUtf8Bytes = 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static void Validate(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        int utf8ByteCount;
        try
        {
            utf8ByteCount = StrictUtf8.GetByteCount(password);
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException("Password must contain valid Unicode scalar values.", nameof(password));
        }

        if (utf8ByteCount > MaximumUtf8Bytes)
        {
            throw new ArgumentException(
                $"Password cannot exceed {MaximumUtf8Bytes} UTF-8 bytes.",
                nameof(password));
        }

        var scalarCount = 0;
        var remaining = password.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out var consumed) != OperationStatus.Done)
            {
                throw new ArgumentException("Password must contain valid Unicode scalar values.", nameof(password));
            }

            scalarCount++;
            if (scalarCount > MaximumScalarCount)
            {
                throw new ArgumentException(
                    $"Password cannot exceed {MaximumScalarCount} Unicode scalar values.",
                    nameof(password));
            }

            remaining = remaining[consumed..];
        }

        if (scalarCount < MinimumScalarCount)
        {
            throw new ArgumentException(
                $"Password must contain at least {MinimumScalarCount} Unicode scalar values.",
                nameof(password));
        }
    }
}
