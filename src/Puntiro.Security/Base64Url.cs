namespace Puntiro.Security;

public static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(ReadOnlySpan<char> encoded, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
        if (encoded.IsEmpty)
        {
            return true;
        }

        var remainder = encoded.Length % 4;
        if (remainder == 1)
        {
            return false;
        }

        int requiredLength;
        try
        {
            requiredLength = checked((encoded.Length * 6) / 8);
        }
        catch (OverflowException)
        {
            return false;
        }

        if (destination.Length < requiredLength)
        {
            return false;
        }

        var inputIndex = 0;
        var outputIndex = 0;
        while (encoded.Length - inputIndex >= 4)
        {
            if (!TryDecodeCharacter(encoded[inputIndex], out var first) ||
                !TryDecodeCharacter(encoded[inputIndex + 1], out var second) ||
                !TryDecodeCharacter(encoded[inputIndex + 2], out var third) ||
                !TryDecodeCharacter(encoded[inputIndex + 3], out var fourth))
            {
                return false;
            }

            destination[outputIndex++] = (byte)((first << 2) | (second >> 4));
            destination[outputIndex++] = (byte)((second << 4) | (third >> 2));
            destination[outputIndex++] = (byte)((third << 6) | fourth);
            inputIndex += 4;
        }

        if (remainder == 2)
        {
            if (!TryDecodeCharacter(encoded[inputIndex], out var first) ||
                !TryDecodeCharacter(encoded[inputIndex + 1], out var second) ||
                (second & 0x0f) != 0)
            {
                return false;
            }

            destination[outputIndex++] = (byte)((first << 2) | (second >> 4));
        }
        else if (remainder == 3)
        {
            if (!TryDecodeCharacter(encoded[inputIndex], out var first) ||
                !TryDecodeCharacter(encoded[inputIndex + 1], out var second) ||
                !TryDecodeCharacter(encoded[inputIndex + 2], out var third) ||
                (third & 0x03) != 0)
            {
                return false;
            }

            destination[outputIndex++] = (byte)((first << 2) | (second >> 4));
            destination[outputIndex++] = (byte)((second << 4) | (third >> 2));
        }

        if (outputIndex != requiredLength)
        {
            return false;
        }

        bytesWritten = outputIndex;
        return true;
    }

    private static bool TryDecodeCharacter(char character, out int value)
    {
        if (character is >= 'A' and <= 'Z')
        {
            value = character - 'A';
            return true;
        }

        if (character is >= 'a' and <= 'z')
        {
            value = character - 'a' + 26;
            return true;
        }

        if (character is >= '0' and <= '9')
        {
            value = character - '0' + 52;
            return true;
        }

        if (character == '-')
        {
            value = 62;
            return true;
        }

        if (character == '_')
        {
            value = 63;
            return true;
        }

        value = 0;
        return false;
    }
}
