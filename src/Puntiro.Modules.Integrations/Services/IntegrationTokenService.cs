using System.Collections.Frozen;
using System.Data;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Domain;
using Puntiro.Modules.Integrations.Persistence;
using Puntiro.Modules.Integrations.Security;

namespace Puntiro.Modules.Integrations.Services;

internal sealed class IntegrationTokenService(
    IntegrationsDbContext context,
    IIntegrationTokenCodec tokenCodec,
    IIntegrationTokenTransactionFactory transactionFactory,
    TimeProvider timeProvider) : IIntegrationTokenService
{
    private const int MaximumActiveTokens = 2;
    private const int MaximumCreationAttempts = 4;
    private const string ManualRevokeReason = "manual_revoke";
    private static readonly TimeSpan LastUsedWriteInterval = TimeSpan.FromMinutes(15);

    public async Task<IssuedIntegrationToken> CreateAsync(
        CreateIntegrationToken command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureId(command.OrganizationId, nameof(command.OrganizationId));
        EnsureId(command.CreatedByUserId, nameof(command.CreatedByUserId));
        var displayName = IntegrationToken.NormalizeDisplayName(command.DisplayName);
        var scopes = IntegrationScopes.Normalize(command.Scopes, nameof(command.Scopes));

        Exception? lastRetriableError = null;
        for (var attempt = 1; attempt <= MaximumCreationAttempts; attempt++)
        {
            var material = tokenCodec.Issue();
            var tokenId = Guid.CreateVersion7();
            var eventId = Guid.CreateVersion7();
            var createdAt = timeProvider.GetUtcNow();
            try
            {
                try
                {
                    var strategy = context.Database.CreateExecutionStrategy();
                    var metadata = await strategy.ExecuteAsync(async retryToken =>
                    {
                        context.ChangeTracker.Clear();
                        var committed = await TryReadCommittedCreationAsync(
                            tokenId,
                            eventId,
                            command,
                            displayName,
                            scopes,
                            material.PublicId,
                            retryToken);
                        return committed ?? await CreateAttemptAsync(
                            tokenId,
                            eventId,
                            createdAt,
                            command,
                            displayName,
                            scopes,
                            material,
                            retryToken);
                    }, cancellationToken);

                    var rawToken = material.TakeRawToken();
                    try
                    {
                        return new IssuedIntegrationToken(metadata, rawToken);
                    }
                    catch
                    {
                        rawToken.Dispose();
                        throw;
                    }
                }
                catch (Exception exception) when (IsRetriableCreationFailure(exception))
                {
                    lastRetriableError = exception;
                    if (attempt == MaximumCreationAttempts)
                    {
                        throw new IntegrationTokenCreationConflictException(exception);
                    }
                }
            }
            finally
            {
                material.Dispose();
            }
        }

        throw new IntegrationTokenCreationConflictException(
            lastRetriableError ?? new InvalidOperationException("No creation attempt was made."));
    }

    private async Task<IntegrationTokenMetadata> CreateAttemptAsync(
        Guid tokenId,
        Guid eventId,
        DateTimeOffset createdAt,
        CreateIntegrationToken command,
        string displayName,
        IReadOnlySet<IntegrationScope> scopes,
        IssuedIntegrationTokenMaterial material,
        CancellationToken cancellationToken)
    {
        IntegrationToken? token = null;
        IIntegrationTokenTransaction? transaction = null;
        ExceptionDispatchInfo? bodyFailure = null;
        Exception? disposalFailure = null;
        var commitConfirmed = false;
        try
        {
            try
            {
                transaction = await transactionFactory.BeginSerializableAsync(cancellationToken);
                var activeSlots = await context.IntegrationTokens
                    .AsNoTracking()
                    .Where(item =>
                        item.OrganizationId == command.OrganizationId &&
                        item.RevokedAtUtc == null)
                    .Select(item => item.ActiveSlot)
                    .ToListAsync(cancellationToken);
                if (activeSlots.Count >= MaximumActiveTokens)
                {
                    throw new ActiveTokenLimitException();
                }

                var activeSlot = activeSlots.Contains((short?)1) ? (short)2 : (short)1;
                token = IntegrationToken.Issue(
                    tokenId,
                    material.PublicId,
                    command.OrganizationId,
                    displayName,
                    material.SecretVerifier,
                    material.KeyVersion,
                    command.CreatedByUserId,
                    createdAt,
                    scopes,
                    activeSlot);
                context.IntegrationTokens.Add(token);
                context.SecurityEvents.Add(IntegrationSecurityEvent.Created(
                    eventId,
                    command.OrganizationId,
                    command.CreatedByUserId,
                    token.Id,
                    createdAt));
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                commitConfirmed = true;
            }
            catch (Exception exception)
            {
                bodyFailure = ExceptionDispatchInfo.Capture(exception);
            }

            if (transaction is not null)
            {
                try
                {
                    await transaction.DisposeAsync();
                }
                catch (Exception exception)
                {
                    disposalFailure = exception;
                }
            }

            if (bodyFailure is not null)
            {
                var primaryException = bodyFailure.SourceException;
                CleanupFailedCreationAttempt(token);
                if (disposalFailure is not null)
                {
                    throw new IntegrationTokenAttemptCleanupException(
                        primaryException,
                        disposalFailure);
                }

                bodyFailure.Throw();
            }

            if (disposalFailure is not null)
            {
                CleanupFailedCreationAttempt(token);
                throw new IntegrationTokenCommittedWithoutCredentialException(
                    token?.Id ?? tokenId,
                    command.OrganizationId,
                    disposalFailure);
            }

            if (!commitConfirmed || token is null)
            {
                throw new InvalidOperationException(
                    "Integration token creation ended without a committed result.");
            }

            try
            {
                var metadata = ToMetadata(token);
                DetachAndClear(token);
                return metadata;
            }
            catch (Exception exception) when (
                exception is not IntegrationTokenCommittedWithoutCredentialException)
            {
                CleanupFailedCreationAttempt(token);
                throw new IntegrationTokenCommittedWithoutCredentialException(
                    token.Id,
                    token.OrganizationId,
                    exception);
            }
        }
        catch
        {
            CleanupFailedCreationAttempt(token);
            throw;
        }
    }

    private async Task<IntegrationTokenMetadata?> TryReadCommittedCreationAsync(
        Guid tokenId,
        Guid eventId,
        CreateIntegrationToken command,
        string displayName,
        IReadOnlySet<IntegrationScope> scopes,
        string publicId,
        CancellationToken cancellationToken)
    {
        var row = await context.IntegrationTokens
            .AsNoTracking()
            .Where(item => item.Id == tokenId)
            .Select(item => new
            {
                item.Id,
                item.PublicId,
                item.OrganizationId,
                item.CreatedByUserId,
                item.DisplayName,
                Scopes = item.Scopes.Select(scope => scope.Scope).ToArray(),
                item.CreatedAtUtc,
                item.LastUsedAtUtc,
                item.RevokedAtUtc,
                item.Version
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var hasEvent = await context.SecurityEvents.AsNoTracking().AnyAsync(
            item => item.Id == eventId &&
                item.TokenId == tokenId &&
                item.OrganizationId == command.OrganizationId &&
                item.ActorUserId == command.CreatedByUserId,
            cancellationToken);
        if (!hasEvent ||
            row.OrganizationId != command.OrganizationId ||
            row.CreatedByUserId != command.CreatedByUserId ||
            !string.Equals(row.PublicId, publicId, StringComparison.Ordinal) ||
            !string.Equals(row.DisplayName, displayName, StringComparison.Ordinal) ||
            !row.Scopes.ToHashSet().SetEquals(scopes))
        {
            throw new IntegrationTokenCommittedWithoutCredentialException(
                tokenId,
                command.OrganizationId,
                new InvalidOperationException("Integration token commit verification failed."));
        }

        return new IntegrationTokenMetadata(
            row.Id,
            row.PublicId,
            row.DisplayName,
            row.Scopes.ToFrozenSet(),
            row.CreatedAtUtc,
            row.LastUsedAtUtc,
            row.RevokedAtUtc,
            row.Version);
    }

    public async Task<IReadOnlyList<IntegrationTokenMetadata>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        EnsureId(organizationId, nameof(organizationId));
        var rows = await context.IntegrationTokens
            .AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => new
            {
                item.Id,
                item.PublicId,
                item.DisplayName,
                Scopes = item.Scopes.Select(scope => scope.Scope).ToArray(),
                item.CreatedAtUtc,
                item.LastUsedAtUtc,
                item.RevokedAtUtc,
                item.Version
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new IntegrationTokenMetadata(
                item.Id,
                item.PublicId,
                item.DisplayName,
                item.Scopes.ToFrozenSet(),
                item.CreatedAtUtc,
                item.LastUsedAtUtc,
                item.RevokedAtUtc,
                item.Version))
            .ToArray();
    }

    public async Task RevokeAsync(
        Guid organizationId,
        Guid tokenId,
        Guid revokedByUserId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        EnsureId(organizationId, nameof(organizationId));
        EnsureId(tokenId, nameof(tokenId));
        EnsureId(revokedByUserId, nameof(revokedByUserId));
        if (expectedVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        }

        var operationEventId = Guid.CreateVersion7();
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async retryToken =>
        {
            context.ChangeTracker.Clear();
            var existing = await context.IntegrationTokens.AsNoTracking()
                .Where(item => item.Id == tokenId && item.OrganizationId == organizationId)
                .Select(item => new { item.RevokedAtUtc })
                .SingleOrDefaultAsync(retryToken);
            if (existing is null)
            {
                throw new KeyNotFoundException("Integration token was not found.");
            }

            if (existing.RevokedAtUtc is not null)
            {
                return;
            }

            await RevokeAttemptAsync(
                organizationId,
                tokenId,
                revokedByUserId,
                expectedVersion,
                operationEventId,
                retryToken);
        }, cancellationToken);
    }

    private async Task RevokeAttemptAsync(
        Guid organizationId,
        Guid tokenId,
        Guid revokedByUserId,
        long expectedVersion,
        Guid operationEventId,
        CancellationToken cancellationToken)
    {
        IntegrationToken? token = null;
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM integrations.integration_tokens WHERE id = {tokenId} AND organization_id = {organizationId} FOR UPDATE",
                cancellationToken);
            token = await context.IntegrationTokens.SingleOrDefaultAsync(
                item => item.Id == tokenId && item.OrganizationId == organizationId,
                cancellationToken) ?? throw new KeyNotFoundException(
                    "Integration token was not found.");
            await context.Entry(token).ReloadAsync(cancellationToken);
            if (token.RevokedAtUtc is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                DetachAndClear(token);
                return;
            }

            if (token.Version != expectedVersion)
            {
                throw new DbUpdateConcurrencyException("Integration token version does not match.");
            }
            var now = timeProvider.GetUtcNow();
            token.Revoke(now, revokedByUserId, ManualRevokeReason);
            context.SecurityEvents.Add(IntegrationSecurityEvent.Revoked(
                operationEventId,
                organizationId,
                revokedByUserId,
                token.Id,
                now));
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            DetachAndClear(token);
        }
        catch
        {
            if (token is not null)
            {
                DetachAndClear(token);
            }

            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<IntegrationPrincipal?> AuthenticateAsync(
        string presentedToken,
        CancellationToken cancellationToken)
    {
        if (!tokenCodec.TryRead(presentedToken, out var publicId, out var parsedSecret))
        {
            return null;
        }

        CryptographicOperations.ZeroMemory(parsedSecret);
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async retryToken =>
        {
            context.ChangeTracker.Clear();
            return await AuthenticateAttemptAsync(
                presentedToken,
                publicId,
                retryToken);
        }, cancellationToken);
    }

    private async Task<IntegrationPrincipal?> AuthenticateAttemptAsync(
        string presentedToken,
        string publicId,
        CancellationToken cancellationToken)
    {
        IntegrationToken? token = null;
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM integrations.integration_tokens WHERE public_id = {publicId} FOR UPDATE",
                cancellationToken);
            token = await context.IntegrationTokens
                .Include(item => item.Scopes)
                .SingleOrDefaultAsync(item => item.PublicId == publicId, cancellationToken);
            if (token is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            await context.Entry(token).ReloadAsync(cancellationToken);
            await context.Entry(token).Collection(item => item.Scopes).LoadAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            if (token.RevokedAtUtc is not null ||
                token.CreatedAtUtc > now ||
                token.LastUsedAtUtc > now ||
                token.ScopeValues.Count is 0 or > 2 ||
                token.ScopeValues.Any(static scope => !IntegrationScopes.IsApproved(scope)) ||
                !tokenCodec.Verify(
                    presentedToken,
                    token.PublicId,
                    token.KeyVersion,
                    token.SecretVerifier))
            {
                await transaction.CommitAsync(cancellationToken);
                DetachAndClear(token);
                return null;
            }

            if (token.LastUsedAtUtc is null ||
                now - token.LastUsedAtUtc.Value >= LastUsedWriteInterval)
            {
                token.Observe(now);
                await context.SaveChangesAsync(cancellationToken);
            }

            var principal = new IntegrationPrincipal(
                token.Id,
                token.OrganizationId,
                token.ScopeValues);
            await transaction.CommitAsync(cancellationToken);
            DetachAndClear(token);
            return principal;
        }
        catch
        {
            if (token is not null)
            {
                DetachAndClear(token);
            }

            context.ChangeTracker.Clear();
            throw;
        }
    }

    private static IntegrationTokenMetadata ToMetadata(IntegrationToken token) =>
        new(
            token.Id,
            token.PublicId,
            token.DisplayName,
            token.ScopeValues,
            token.CreatedAtUtc,
            token.LastUsedAtUtc,
            token.RevokedAtUtc,
            token.Version);

    private void DetachAndClear(IntegrationToken token)
    {
        foreach (var scope in token.Scopes.ToArray())
        {
            context.Entry(scope).State = EntityState.Detached;
        }

        context.Entry(token).State = EntityState.Detached;
        token.ClearSecretVerifier();
    }

    private void CleanupFailedCreationAttempt(IntegrationToken? token)
    {
        try
        {
            token?.ClearSecretVerifier();
        }
        catch
        {
            // Cleanup is best effort and must never replace the primary create failure.
        }

        try
        {
            context.ChangeTracker.Clear();
        }
        catch
        {
            // The caller must receive the original provider/cancellation failure.
        }
    }

    private static bool IsRetriableCreationFailure(Exception exception)
    {
        if (exception is IntegrationTokenAttemptCleanupException or
            IntegrationTokenCommittedWithoutCredentialException)
        {
            return false;
        }

        var postgres = FindPostgresException(exception);
        if (postgres is null)
        {
            return false;
        }

        if (postgres.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            return true;
        }

        return postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgres.ConstraintName is
                "ux_integration_tokens_public_id" or
                "ux_integration_tokens_organization_active_slot";
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        const int maximumWrapperDepth = 8;
        Exception? current = exception;
        for (var depth = 0; depth < maximumWrapperDepth && current is not null; depth++)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }

            current = current.InnerException;
        }

        return null;
    }

    private static void EnsureId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }
}
