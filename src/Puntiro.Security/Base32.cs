namespace Puntiro.Security;

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// Encodes non-secret data into an immutable string. Secret callers must use the span overload
    /// so their caller-owned destination can be cleared after use.
    /// </summary>
    public static string Encode(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        var encodedLength = GetEncodedLength(value.Length);
        var output = new char[encodedLength];
        try
        {
            Encode(value, output);
            return new string(output);
        }
        finally
        {
            Array.Clear(output);
        }
    }

    public static int GetEncodedLength(int byteLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteLength);
        return checked(((byteLength * 8) + 4) / 5);
    }

    /// <summary>
    /// Encodes into a caller-owned destination whose length must equal
    /// <see cref="GetEncodedLength(int)"/>. The caller remains responsible for clearing it.
    /// </summary>
    public static void Encode(ReadOnlySpan<byte> value, Span<char> destination)
    {
        var encodedLength = GetEncodedLength(value.Length);
        if (destination.Length != encodedLength)
        {
            throw new ArgumentException(
                $"Base32 destination must contain exactly {encodedLength} characters.",
                nameof(destination));
        }

        var buffer = 0;
        var bufferedBits = 0;
        var outputIndex = 0;

        foreach (var item in value)
        {
            buffer = (buffer << 8) | item;
            bufferedBits += 8;

            while (bufferedBits >= 5)
            {
                bufferedBits -= 5;
                destination[outputIndex++] = Alphabet[(buffer >> bufferedBits) & 0x1f];
            }

            buffer &= (1 << bufferedBits) - 1;
        }

        if (bufferedBits > 0)
        {
            destination[outputIndex] = Alphabet[(buffer << (5 - bufferedBits)) & 0x1f];
        }
    }

    public static bool TryDecode(ReadOnlySpan<char> encoded, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
        if (encoded.IsEmpty)
        {
            return true;
        }

        var remainder = encoded.Length % 8;
        if (remainder is not (0 or 2 or 4 or 5 or 7))
        {
            return false;
        }

        int requiredLength;
        try
        {
            requiredLength = checked((encoded.Length * 5) / 8);
        }
        catch (OverflowException)
        {
            return false;
        }

        if (destination.Length < requiredLength)
        {
            return false;
        }

        var buffer = 0;
        var bufferedBits = 0;
        var outputIndex = 0;

        foreach (var character in encoded)
        {
            var value = DecodeCharacter(character);
            if (value < 0)
            {
                return false;
            }

            buffer = (buffer << 5) | value;
            bufferedBits += 5;

            if (bufferedBits >= 8)
            {
                bufferedBits -= 8;
                destination[outputIndex++] = (byte)(buffer >> bufferedBits);
            }

            buffer &= (1 << bufferedBits) - 1;
        }

        if (buffer != 0 || outputIndex != requiredLength)
        {
            return false;
        }

        bytesWritten = outputIndex;
        return true;
    }

    private static int DecodeCharacter(char value)
    {
        if (value is >= 'A' and <= 'Z')
        {
            return value - 'A';
        }

        if (value is >= '2' and <= '7')
        {
            return value - '2' + 26;
        }

        return -1;
    }
}
