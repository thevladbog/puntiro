namespace Puntiro.Provisioning.Cli;

public enum ProvisioningExit
{
    Success = 0,
    InvalidArguments = 2,
    Conflict = 3,
    InvalidCredentials = 4,
    InfrastructureFailure = 5,
}

public sealed record ProvisioningParseResult(
    ProvisioningExit ExitCode,
    string? Command,
    IReadOnlyDictionary<string, string> Arguments);

public static class ProvisioningArguments
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> CommandOptions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["bootstrap-owner"] = new HashSet<string>(
                ["organization-name", "organization-slug", "email"],
                StringComparer.Ordinal),
            ["reset-owner-totp"] = new HashSet<string>(
                ["organization-slug", "email"],
                StringComparer.Ordinal),
            ["cloud-preflight"] = new HashSet<string>(["mode"], StringComparer.Ordinal),
        };

    private static readonly ProvisioningParseResult Invalid = new(
        ProvisioningExit.InvalidArguments,
        null,
        new Dictionary<string, string>());

    public static ProvisioningParseResult Parse(IReadOnlyList<string> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Count == 0 ||
            !CommandOptions.TryGetValue(input[0], out var allowed) ||
            input.Count != 1 + (allowed.Count * 2))
        {
            return Invalid;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < input.Count; index += 2)
        {
            var rawOption = input[index];
            var value = input[index + 1];
            if (!rawOption.StartsWith("--", StringComparison.Ordinal) ||
                rawOption.Length == 2 ||
                value.StartsWith("--", StringComparison.Ordinal))
            {
                return Invalid;
            }

            var option = rawOption[2..];
            if (!allowed.Contains(option) || !values.TryAdd(option, value))
            {
                return Invalid;
            }
        }

        return values.Count == allowed.Count
            ? new ProvisioningParseResult(ProvisioningExit.Success, input[0], values)
            : Invalid;
    }
}
