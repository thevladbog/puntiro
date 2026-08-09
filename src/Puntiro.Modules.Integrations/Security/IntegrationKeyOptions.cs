using System.Diagnostics;
using System.Security.Cryptography;

namespace Puntiro.Modules.Integrations.Security;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class IntegrationKeyOptions
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly IReadOnlyDictionary<string, byte[]> _keys;

    public IntegrationKeyOptions(
        string currentIntegrationKeyVersion,
        IReadOnlyDictionary<string, byte[]> integrationHmacKeys)
    {
        CurrentIntegrationKeyVersion = ValidateVersion(
            currentIntegrationKeyVersion,
            nameof(currentIntegrationKeyVersion));
        _keys = CloneAndValidate(integrationHmacKeys, nameof(integrationHmacKeys));
        if (!_keys.ContainsKey(CurrentIntegrationKeyVersion))
        {
            throw new ArgumentException(
                "The current integration key version must exist in the key set.",
                nameof(currentIntegrationKeyVersion));
        }
    }

    public string CurrentIntegrationKeyVersion { get; }

    internal static IntegrationKeyOptions ForTesting(string version, byte[] key) =>
        new(version, new Dictionary<string, byte[]> { [version] = key });

    internal byte[] GetKey(string version)
    {
        if (!_keys.TryGetValue(version, out var key))
        {
            throw new KeyNotFoundException("The requested integration key version is unavailable.");
        }

        return (byte[])key.Clone();
    }

    internal bool HasKey(string version) => _keys.ContainsKey(version);

    public override string ToString() => nameof(IntegrationKeyOptions);

    private string DebuggerDisplay => nameof(IntegrationKeyOptions);

    private static IReadOnlyDictionary<string, byte[]> CloneAndValidate(
        IReadOnlyDictionary<string, byte[]> source,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        if (source.Count == 0)
        {
            throw new ArgumentException("At least one integration HMAC key is required.", parameterName);
        }

        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var item in source)
        {
            var version = ValidateVersion(item.Key, parameterName);
            if (item.Value is not { Length: HMACSHA256.HashSizeInBytes })
            {
                throw new ArgumentException(
                    "Integration HMAC keys must contain exactly 32 bytes.",
                    parameterName);
            }

            result.Add(version, (byte[])item.Value.Clone());
        }

        return result;
    }

    private static string ValidateVersion(string version, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 32 ||
            version.Any(static character => character is not (
                >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
        {
            throw new ArgumentException(
                "Key version must be a bounded ASCII identifier.",
                parameterName);
        }

        return version;
    }
}
