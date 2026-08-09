using System.Security.Cryptography;

namespace Puntiro.Cloud.Configuration;

public sealed class PuntiroCloudOptions
{
    public const string SectionName = "Puntiro";

    public CloudAdminOptions Admin { get; init; } = new();
    public CloudProxyOptions Proxy { get; init; } = new();
    public CloudSecurityOptions Security { get; init; } = new();
}

public sealed class CloudAdminOptions
{
    public string AllowedOrigin { get; init; } = string.Empty;
}

public sealed class CloudProxyOptions
{
    public bool Enabled { get; init; }
    public string[] KnownProxies { get; init; } = [];
    public string[] KnownNetworks { get; init; } = [];
}

public sealed class CloudSecurityOptions
{
    public string DataProtectionKeysPath { get; init; } = string.Empty;
    public string DataProtectionCertificatePath { get; init; } = string.Empty;
    public string DataProtectionCertificatePassword { get; init; } = string.Empty;
    public VersionedHmacOptions SessionHmac { get; init; } = new();
    public VersionedHmacOptions RecoveryHmac { get; init; } = new();
    public VersionedHmacOptions IntegrationHmac { get; init; } = new();
    public int LoginIpLimit { get; init; } = 10;
    public int LoginAccountLimit { get; init; } = 5;
    public int StepUpSessionLimit { get; init; } = 5;
    public int RateLimitWindowSeconds { get; init; } = 60;
    public int MaximumRateLimitPartitions { get; init; } = 10_000;
    public int MaximumRequestBodyBytes { get; init; } = 16_384;
    public int MaximumHeaderBytes { get; init; } = 32_768;
}

public sealed class VersionedHmacOptions
{
    public string CurrentVersion { get; init; } = string.Empty;
    public Dictionary<string, string> Keys { get; init; } = new(StringComparer.Ordinal);

    internal IReadOnlyDictionary<string, byte[]> Decode(string purpose)
    {
        if (string.IsNullOrWhiteSpace(CurrentVersion) || CurrentVersion.Length > 32 ||
            !CurrentVersion.All(static value => char.IsAsciiLetterOrDigit(value) || value is '-' or '_'))
        {
            throw new InvalidOperationException($"{purpose} current key version is invalid.");
        }

        var decoded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var item in Keys)
        {
            byte[] value;
            try
            {
                value = Convert.FromBase64String(item.Value);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException($"{purpose} key material is invalid.", exception);
            }

            if (value.Length != HMACSHA256.HashSizeInBytes)
            {
                CryptographicOperations.ZeroMemory(value);
                throw new InvalidOperationException($"{purpose} keys must contain exactly 32 bytes.");
            }

            decoded.Add(item.Key, value);
        }

        if (!decoded.ContainsKey(CurrentVersion))
        {
            foreach (var key in decoded.Values)
            {
                CryptographicOperations.ZeroMemory(key);
            }

            throw new InvalidOperationException($"{purpose} current key is unavailable.");
        }

        return decoded;
    }
}
