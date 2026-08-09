using System.Globalization;
using System.Text;

namespace Puntiro.Modules.Identity.Security;

internal sealed class EmailAddress
{
    private const int MaximumNormalizedUtf8Bytes = 320;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private EmailAddress(string display, string normalized)
    {
        Display = display;
        Normalized = normalized;
    }

    public string Display { get; }

    public string Normalized { get; }

    public static EmailAddress Normalize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var display = input.Trim();
        if (display.Length == 0)
        {
            throw new ArgumentException("Email cannot be empty.", nameof(input));
        }

        string canonical;
        try
        {
            canonical = display.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("Email must contain valid Unicode.", nameof(input));
        }

        var separator = canonical.IndexOf('@');
        if (separator <= 0 ||
            separator != canonical.LastIndexOf('@') ||
            separator == canonical.Length - 1)
        {
            throw new ArgumentException("Email must contain one local part and one domain.", nameof(input));
        }

        var localPart = FoldLocalPart(canonical.AsSpan(0, separator));
        var domain = canonical[(separator + 1)..];

        string asciiDomain;
        try
        {
            asciiDomain = new IdnMapping { UseStd3AsciiRules = true }.GetAscii(domain);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("Email domain is invalid.", nameof(input));
        }

        if (asciiDomain.Length == 0)
        {
            throw new ArgumentException("Email domain is invalid.", nameof(input));
        }

        var normalized = $"{localPart}@{asciiDomain.ToLowerInvariant()}";
        int byteCount;
        try
        {
            byteCount = StrictUtf8.GetByteCount(normalized);
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException("Email must contain valid Unicode.", nameof(input));
        }

        if (byteCount > MaximumNormalizedUtf8Bytes)
        {
            throw new ArgumentException(
                $"Normalized email cannot exceed {MaximumNormalizedUtf8Bytes} UTF-8 bytes.",
                nameof(input));
        }

        return new EmailAddress(display, normalized);
    }

    private static string FoldLocalPart(ReadOnlySpan<char> localPart)
    {
        // Version 1 identity-key fold: NFC first, then invariant upper/lower per Unicode scalar.
        // The upper pass collapses simple-fold peers such as sigma/final-sigma before the
        // lower pass materializes one stable key without using the current culture.
        var folded = new StringBuilder(localPart.Length);
        Span<char> encoded = stackalloc char[2];
        foreach (var rune in localPart.EnumerateRunes())
        {
            var canonicalRune = Rune.ToLowerInvariant(Rune.ToUpperInvariant(rune));
            var written = canonicalRune.EncodeToUtf16(encoded);
            folded.Append(encoded[..written]);
        }

        return folded.ToString();
    }

    public override string ToString() => nameof(EmailAddress);
}
