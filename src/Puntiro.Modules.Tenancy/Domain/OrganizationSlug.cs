using System.Text;

namespace Puntiro.Modules.Tenancy.Domain;

internal readonly record struct OrganizationSlug
{
    private const int MaximumLength = 63;

    private OrganizationSlug(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static OrganizationSlug Normalize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var normalized = new StringBuilder(input.Length);
        var separatorPending = false;

        foreach (var character in input.Trim())
        {
            if (character is >= 'A' and <= 'Z')
            {
                AppendSeparatorIfRequired(normalized, ref separatorPending);
                normalized.Append((char)(character + ('a' - 'A')));
            }
            else if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                AppendSeparatorIfRequired(normalized, ref separatorPending);
                normalized.Append(character);
            }
            else if (character == '-' || char.IsWhiteSpace(character))
            {
                separatorPending = normalized.Length > 0;
            }
            else
            {
                throw new ArgumentException(
                    "Organization slug accepts only ASCII letters, digits, whitespace and hyphens.",
                    nameof(input));
            }

            if (normalized.Length > MaximumLength)
            {
                throw new ArgumentException(
                    $"Organization slug cannot exceed {MaximumLength} characters.",
                    nameof(input));
            }
        }

        if (normalized.Length == 0)
        {
            throw new ArgumentException("Organization slug cannot be empty.", nameof(input));
        }

        return new OrganizationSlug(normalized.ToString());
    }

    private static void AppendSeparatorIfRequired(StringBuilder builder, ref bool separatorPending)
    {
        if (separatorPending && builder.Length > 0)
        {
            builder.Append('-');
            separatorPending = false;
        }
    }
}
