using System.Diagnostics;
using System.Text.Json.Serialization;
using Puntiro.Modules.Integrations.Domain;
using Puntiro.Security;

namespace Puntiro.Modules.Integrations.Contracts;

public sealed record CreateIntegrationToken(
    Guid OrganizationId,
    Guid CreatedByUserId,
    string DisplayName,
    IReadOnlySet<IntegrationScope> Scopes)
{
    public override string ToString() => nameof(CreateIntegrationToken);
}

public sealed record IntegrationTokenMetadata(
    Guid Id,
    string PublicId,
    string DisplayName,
    IReadOnlySet<IntegrationScope> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    long Version);

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record IssuedIntegrationToken : IDisposable
{
    public IssuedIntegrationToken(
        IntegrationTokenMetadata metadata,
        SensitiveValue rawToken)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        RawToken = rawToken ?? throw new ArgumentNullException(nameof(rawToken));
    }

    public IntegrationTokenMetadata Metadata { get; }

    [JsonIgnore]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public SensitiveValue RawToken { get; }

    public void Dispose() => RawToken.Dispose();

    public override string ToString() => nameof(IssuedIntegrationToken);

    private string DebuggerDisplay => nameof(IssuedIntegrationToken);
}

public sealed record IntegrationPrincipal(
    Guid TokenId,
    Guid OrganizationId,
    IReadOnlySet<IntegrationScope> Scopes);

public sealed class ActiveTokenLimitException : Exception
{
    public ActiveTokenLimitException()
        : base("The organization already has the maximum number of active integration tokens.")
    {
    }
}

public sealed class IntegrationTokenCreationConflictException : Exception
{
    public IntegrationTokenCreationConflictException(Exception innerException)
        : base("Integration token creation could not be serialized after bounded retries.", innerException)
    {
    }
}

public interface IIntegrationTokenService
{
    Task<IssuedIntegrationToken> CreateAsync(
        CreateIntegrationToken command,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IntegrationTokenMetadata>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        Guid organizationId,
        Guid tokenId,
        Guid revokedByUserId,
        long expectedVersion,
        CancellationToken cancellationToken);

    Task<IntegrationPrincipal?> AuthenticateAsync(
        string presentedToken,
        CancellationToken cancellationToken);
}
