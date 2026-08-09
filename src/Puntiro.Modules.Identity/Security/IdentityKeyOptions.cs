using System.Diagnostics;
using System.Security.Cryptography;

namespace Puntiro.Modules.Identity.Security;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class IdentityKeyOptions
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly IReadOnlyDictionary<string, byte[]> _sessionKeys;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly IReadOnlyDictionary<string, byte[]> _recoveryKeys;

    public IdentityKeyOptions(
        string currentSessionKeyVersion,
        IReadOnlyDictionary<string, byte[]> sessionHmacKeys,
        string currentRecoveryKeyVersion,
        IReadOnlyDictionary<string, byte[]> recoveryHmacKeys)
    {
        CurrentSessionKeyVersion = ValidateVersion(currentSessionKeyVersion, nameof(currentSessionKeyVersion));
        CurrentRecoveryKeyVersion = ValidateVersion(currentRecoveryKeyVersion, nameof(currentRecoveryKeyVersion));
        _sessionKeys = CloneAndValidate(sessionHmacKeys, nameof(sessionHmacKeys));
        _recoveryKeys = CloneAndValidate(recoveryHmacKeys, nameof(recoveryHmacKeys));

        if (!_sessionKeys.ContainsKey(CurrentSessionKeyVersion) ||
            !_recoveryKeys.ContainsKey(CurrentRecoveryKeyVersion))
        {
            throw new ArgumentException("Each current key version must exist in its purpose-specific key set.");
        }

        foreach (var sessionKey in _sessionKeys.Values)
        {
            if (_recoveryKeys.Values.Any(recoveryKey =>
                    CryptographicOperations.FixedTimeEquals(sessionKey, recoveryKey)))
            {
                throw new ArgumentException("An HMAC key cannot be reused across identity purposes.");
            }
        }
    }

    public string CurrentSessionKeyVersion { get; }
    public string CurrentRecoveryKeyVersion { get; }

    internal static IdentityKeyOptions ForTesting(
        string sessionVersion,
        byte[] sessionKey,
        string recoveryVersion,
        byte[] recoveryKey) =>
        new(
            sessionVersion,
            new Dictionary<string, byte[]> { [sessionVersion] = sessionKey },
            recoveryVersion,
            new Dictionary<string, byte[]> { [recoveryVersion] = recoveryKey });

    internal byte[] GetSessionKey(string version) => GetKey(_sessionKeys, version);

    internal byte[] GetRecoveryKey(string version) => GetKey(_recoveryKeys, version);

    internal bool HasSessionKey(string version) => _sessionKeys.ContainsKey(version);

    internal bool HasRecoveryKey(string version) => _recoveryKeys.ContainsKey(version);

    public override string ToString() => nameof(IdentityKeyOptions);

    private string DebuggerDisplay => nameof(IdentityKeyOptions);

    private static byte[] GetKey(IReadOnlyDictionary<string, byte[]> source, string version)
    {
        if (!source.TryGetValue(version, out var key))
        {
            throw new KeyNotFoundException("The requested identity key version is unavailable.");
        }

        return (byte[])key.Clone();
    }

    private static IReadOnlyDictionary<string, byte[]> CloneAndValidate(
        IReadOnlyDictionary<string, byte[]> source,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        if (source.Count == 0)
        {
            throw new ArgumentException("At least one key is required.", parameterName);
        }

        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var item in source)
        {
            var version = ValidateVersion(item.Key, parameterName);
            if (item.Value is not { Length: HMACSHA256.HashSizeInBytes })
            {
                throw new ArgumentException("Identity HMAC keys must contain exactly 32 bytes.", parameterName);
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
            throw new ArgumentException("Key version must be a bounded ASCII identifier.", parameterName);
        }

        return version;
    }
}
