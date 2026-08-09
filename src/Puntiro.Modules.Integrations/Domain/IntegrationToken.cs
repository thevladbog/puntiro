using System.Collections.Frozen;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Puntiro.Modules.Integrations.Domain;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class IntegrationToken
{
    private readonly List<IntegrationTokenScope> _scopes = [];

    private IntegrationToken()
    {
    }

    private IntegrationToken(
        Guid id,
        string publicId,
        Guid organizationId,
        string displayName,
        byte[] secretVerifier,
        string keyVersion,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc,
        IReadOnlySet<IntegrationScope> scopes,
        short activeSlot)
    {
        EnsureId(id, nameof(id));
        EnsureId(organizationId, nameof(organizationId));
        EnsureId(createdByUserId, nameof(createdByUserId));
        if (string.IsNullOrEmpty(publicId) || publicId.Length > 32 ||
            publicId.Any(static character => character is not (
                >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
        {
            throw new ArgumentException("Public ID must be a bounded Base64Url value.", nameof(publicId));
        }

        if (secretVerifier is not { Length: HMACSHA256.HashSizeInBytes })
        {
            throw new ArgumentException("Token verifier must contain exactly 32 bytes.", nameof(secretVerifier));
        }

        if (string.IsNullOrWhiteSpace(keyVersion) || keyVersion.Length > 32)
        {
            throw new ArgumentException("Key version must be a bounded identifier.", nameof(keyVersion));
        }

        if (activeSlot is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(activeSlot));
        }

        var normalizedScopes = IntegrationScopes.Normalize(scopes, nameof(scopes));
        Id = id;
        PublicId = publicId;
        OrganizationId = organizationId;
        DisplayName = NormalizeDisplayName(displayName);
        SecretVerifier = (byte[])secretVerifier.Clone();
        KeyVersion = keyVersion;
        ActiveSlot = activeSlot;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        Version = 1;
        _scopes.AddRange(normalizedScopes.Select(scope => new IntegrationTokenScope(id, scope)));
    }

    public Guid Id { get; private set; }
    public string PublicId { get; private set; } = string.Empty;
    public Guid OrganizationId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    [JsonIgnore]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public byte[] SecretVerifier { get; private set; } = [];

    public string KeyVersion { get; private set; } = string.Empty;
    public short? ActiveSlot { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastUsedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevokeReason { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyCollection<IntegrationTokenScope> Scopes => _scopes;

    [JsonIgnore]
    public IReadOnlySet<IntegrationScope> ScopeValues =>
        _scopes.Select(static item => item.Scope).ToFrozenSet();

    internal static IntegrationToken Issue(
        Guid id,
        string publicId,
        Guid organizationId,
        string displayName,
        byte[] secretVerifier,
        string keyVersion,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc,
        IReadOnlySet<IntegrationScope> scopes,
        short activeSlot) =>
        new(
            id,
            publicId,
            organizationId,
            displayName,
            secretVerifier,
            keyVersion,
            createdByUserId,
            createdAtUtc,
            scopes,
            activeSlot);

    internal void Observe(DateTimeOffset observedAtUtc)
    {
        if (RevokedAtUtc is not null)
        {
            throw new InvalidOperationException("A revoked integration token cannot be observed.");
        }

        LastUsedAtUtc = EnsureUtc(observedAtUtc);
        Version = checked(Version + 1);
    }

    internal void Revoke(DateTimeOffset revokedAtUtc, Guid revokedByUserId, string reason)
    {
        EnsureId(revokedByUserId, nameof(revokedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = EnsureUtc(revokedAtUtc);
        RevokedByUserId = revokedByUserId;
        RevokeReason = reason;
        ActiveSlot = null;
        Version = checked(Version + 1);
    }

    internal void ClearSecretVerifier()
    {
        CryptographicOperations.ZeroMemory(SecretVerifier);
    }

    internal static string NormalizeDisplayName(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        var normalized = displayName.Trim();
        var count = 0;
        var remaining = normalized.AsSpan();
        while (!remaining.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(remaining, out _, out var consumed);
            if (status != System.Buffers.OperationStatus.Done)
            {
                throw new ArgumentException("Display name must contain valid Unicode text.", nameof(displayName));
            }

            count++;
            if (count > 100)
            {
                throw new ArgumentException(
                    "Display name must contain between 1 and 100 Unicode scalar values.",
                    nameof(displayName));
            }

            remaining = remaining[consumed..];
        }

        if (count == 0)
        {
            throw new ArgumentException(
                "Display name must contain between 1 and 100 Unicode scalar values.",
                nameof(displayName));
        }

        return normalized;
    }

    public override string ToString() => nameof(IntegrationToken);

    private string DebuggerDisplay => nameof(IntegrationToken);

    private static void EnsureId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value) =>
        value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
}

internal sealed class IntegrationTokenScope
{
    private IntegrationTokenScope()
    {
    }

    internal IntegrationTokenScope(Guid tokenId, IntegrationScope scope)
    {
        if (tokenId == Guid.Empty)
        {
            throw new ArgumentException("Token ID cannot be empty.", nameof(tokenId));
        }

        if (!IntegrationScopes.IsApproved(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        TokenId = tokenId;
        Scope = scope;
    }

    public Guid TokenId { get; private set; }
    public IntegrationScope Scope { get; private set; }
}
