using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Puntiro.IntegrationTests.Infrastructure;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Domain;
using Puntiro.Modules.Integrations.Persistence;
using Puntiro.Modules.Integrations.Security;
using Puntiro.Modules.Integrations.Services;
using Puntiro.Security;
using Xunit;

namespace Puntiro.IntegrationTests.Integrations;

[Collection(PostgresCollection.Name)]
public sealed class IntegrationTokenServiceTests(PostgresDatabase database)
{
    [Fact]
    public async Task Body_and_transaction_disposal_failures_preserve_primary_and_clear_attempt()
    {
        var bodyFailure = new InjectedCreateFailureException();
        var disposalFailure = new PostgresException(
            "Injected transaction disposal failure.",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.SerializationFailure);
        var saveInterceptor = new FailFirstIntegrationTokenSaveInterceptor(bodyFailure);
        FailingDisposeIntegrationTokenTransactionFactory? transactionFactory = null;
        TrackingIntegrationTokenCodec? trackingCodec = null;
        await using var scope = await IntegrationTestScope.CreateAsync(
            database.ConnectionString,
            integrationInterceptor: saveInterceptor,
            transactionFactoryFactory: context =>
                transactionFactory = new(context, disposalFailure),
            tokenCodecFactory: codec => trackingCodec = new(codec));

        var error = await Assert.ThrowsAsync<IntegrationTokenAttemptCleanupException>(() =>
            scope.Service.CreateAsync(
                scope.Command("Body and disposal failure"),
                TestContext.Current.CancellationToken));

        Assert.Same(bodyFailure, error.InnerException);
        Assert.Same(bodyFailure, error.InnerExceptions[0]);
        Assert.Same(disposalFailure, error.InnerExceptions[1]);
        var observedTransactionFactory = Assert.IsType<
            FailingDisposeIntegrationTokenTransactionFactory>(transactionFactory);
        Assert.Equal(1, observedTransactionFactory.BeginCount);
        Assert.Equal(1, observedTransactionFactory.DisposeCount);
        AssertCapturedCredentialCleared(
            Assert.IsType<TrackingIntegrationTokenCodec>(trackingCodec));
        var failedToken = Assert.IsType<IntegrationToken>(saveInterceptor.CapturedToken);
        Assert.True(
            failedToken.SecretVerifier.All(static value => value == 0),
            "A double-failed create retained verifier bytes on its domain entity.");
        Assert.False(
            scope.Context.ChangeTracker.Entries().Any(),
            "A double-failed create retained attempt-owned tracked state.");
        Assert.Equal(0, await scope.Context.IntegrationTokens.AsNoTracking().CountAsync(
            item => item.OrganizationId == scope.OrganizationId,
            TestContext.Current.CancellationToken));
        Assert.Equal(0, await scope.Context.SecurityEvents.AsNoTracking().CountAsync(
            item => item.OrganizationId == scope.OrganizationId,
            TestContext.Current.CancellationToken));
        Assert.Equal(0, await scope.Context.IntegrationTokenScopes.AsNoTracking().CountAsync(
            item => item.TokenId == failedToken.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Committed_create_disposal_failure_never_transfers_raw_or_retries()
    {
        var disposalFailure = new InjectedTransactionDisposalException();
        FailingDisposeIntegrationTokenTransactionFactory? transactionFactory = null;
        TrackingIntegrationTokenCodec? trackingCodec = null;
        await using var scope = await IntegrationTestScope.CreateAsync(
            database.ConnectionString,
            transactionFactoryFactory: context =>
                transactionFactory = new(context, disposalFailure),
            tokenCodecFactory: codec => trackingCodec = new(codec));

        var error = await Assert.ThrowsAsync<IntegrationTokenCommittedWithoutCredentialException>(() =>
            scope.Service.CreateAsync(
                scope.Command("Committed without credential"),
                TestContext.Current.CancellationToken));

        Assert.Same(disposalFailure, error.InnerException);
        Assert.Equal(scope.OrganizationId, error.OrganizationId);
        Assert.NotEqual(Guid.Empty, error.TokenId);
        var observedTransactionFactory = Assert.IsType<
            FailingDisposeIntegrationTokenTransactionFactory>(transactionFactory);
        Assert.Equal(1, observedTransactionFactory.BeginCount);
        Assert.Equal(1, observedTransactionFactory.CommitCount);
        Assert.Equal(1, observedTransactionFactory.DisposeCount);
        AssertCapturedCredentialCleared(
            Assert.IsType<TrackingIntegrationTokenCodec>(trackingCodec));
        Assert.False(
            scope.Context.ChangeTracker.Entries().Any(),
            "A committed create with failed disposal retained tracked credential state.");
        Assert.Equal(1, await scope.Context.IntegrationTokens.AsNoTracking().CountAsync(
            item => item.OrganizationId == scope.OrganizationId,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await scope.Context.SecurityEvents.AsNoTracking().CountAsync(
            item => item.OrganizationId == scope.OrganizationId,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await scope.Context.IntegrationTokenScopes.AsNoTracking().CountAsync(
            item => scope.Context.IntegrationTokens.Any(token =>
                token.Id == item.TokenId && token.OrganizationId == scope.OrganizationId),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Non_retriable_save_failure_cleans_attempt_before_reusing_the_same_scope()
    {
        await AssertFailedCreateIsIsolatedAsync(
            new InjectedCreateFailureException(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Non_retriable_commit_failure_cleans_attempt_before_reusing_the_same_scope()
    {
        await AssertFailedCreateIsIsolatedAsync(
            new InjectedCreateFailureException(),
            TestContext.Current.CancellationToken,
            failAtCommit: true);
    }

    [Fact]
    public async Task Cancellation_during_save_cleans_attempt_before_reusing_the_same_scope()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var failure = new OperationCanceledException(
            "Injected integration token save cancellation.",
            cancellation.Token);
        var caught = await AssertFailedCreateIsIsolatedAsync(
            failure,
            TestContext.Current.CancellationToken);

        Assert.Equal(cancellation.Token, caught.CancellationToken);
    }

    [Fact]
    public async Task Create_returns_secret_once_but_persists_only_verifier_and_safe_metadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);

        using var issued = await scope.Service.CreateAsync(scope.Command("  ERP connector  "), cancellationToken);
        var raw = issued.RawToken.Reveal();
        scope.Context.ChangeTracker.Clear();
        var row = await scope.Context.IntegrationTokens
            .Include(item => item.Scopes)
            .SingleAsync(item => item.Id == issued.Metadata.Id, cancellationToken);

        Assert.True(
            Regex.IsMatch(
                raw,
                "^pnt_live_[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+$",
                RegexOptions.CultureInvariant),
            "Issued bearer did not use the canonical bounded token format.");
        Assert.Equal("ERP connector", issued.Metadata.DisplayName);
        Assert.Equal(32, row.SecretVerifier.Length);
        Assert.False(
            string.Equals(raw, Convert.ToBase64String(row.SecretVerifier), StringComparison.Ordinal),
            "The durable verifier representation matched the raw bearer.");
        AssertSensitiveTextAbsent(raw, scope.Context.ChangeTracker.DebugView.LongView);
        AssertSensitiveTextAbsent(raw, JsonSerializer.Serialize(row));
        AssertSensitiveTextAbsent(raw, JsonSerializer.Serialize(issued));
        Assert.Equal(scope.OrganizationId, row.OrganizationId);
        Assert.Equal(scope.ActorUserId, row.CreatedByUserId);
    }

    [Fact]
    public async Task Create_rejects_invalid_identifiers_display_names_and_scope_sets_before_persistence()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);

        await Assert.ThrowsAsync<ArgumentException>(() => scope.Service.CreateAsync(
            scope.Command("ERP") with { OrganizationId = Guid.Empty }, cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => scope.Service.CreateAsync(
            scope.Command("ERP") with { CreatedByUserId = Guid.Empty }, cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => scope.Service.CreateAsync(
            scope.Command("   "), cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => scope.Service.CreateAsync(
            scope.Command("ERP") with { Scopes = new HashSet<IntegrationScope>() }, cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => scope.Service.CreateAsync(
            scope.Command("ERP") with
            {
                Scopes = new HashSet<IntegrationScope> { (IntegrationScope)999 }
            }, cancellationToken));

        Assert.Empty(await scope.Context.IntegrationTokens
            .Where(item =>
                item.OrganizationId == scope.OrganizationId &&
                item.CreatedByUserId == scope.ActorUserId)
            .ToListAsync(cancellationToken));
        Assert.Empty(await scope.Context.SecurityEvents
            .Where(item =>
                item.OrganizationId == scope.OrganizationId &&
                item.ActorUserId == scope.ActorUserId)
            .ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task Third_active_token_is_rejected_but_a_revoked_slot_can_be_reused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var first = await scope.Service.CreateAsync(scope.Command("First"), cancellationToken);
        using var second = await scope.Service.CreateAsync(scope.Command("Second"), cancellationToken);

        await Assert.ThrowsAsync<ActiveTokenLimitException>(() =>
            scope.Service.CreateAsync(scope.Command("Third"), cancellationToken));
        await scope.Service.RevokeAsync(
            scope.OrganizationId,
            first.Metadata.Id,
            scope.ActorUserId,
            first.Metadata.Version,
            cancellationToken);
        using var replacement = await scope.Service.CreateAsync(
            scope.Command("Replacement"), cancellationToken);

        Assert.Equal(2, await scope.Context.IntegrationTokens.CountAsync(
            item => item.OrganizationId == scope.OrganizationId && item.RevokedAtUtc == null,
            cancellationToken));
    }

    [Fact]
    public async Task Concurrent_creation_from_one_active_token_allows_exactly_one_winner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var existing = await scope.Service.CreateAsync(scope.Command("Existing"), cancellationToken);

        var first = CaptureCreateAsync(
            database.ConnectionString, scope.OrganizationId, scope.ActorUserId, "Candidate A", cancellationToken);
        var second = CaptureCreateAsync(
            database.ConnectionString, scope.OrganizationId, scope.ActorUserId, "Candidate B", cancellationToken);
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, static result => result.Success);
        Assert.Single(results, static result => result.Error is ActiveTokenLimitException);
        Assert.DoesNotContain(results, static result => !result.Success && result.Error is not ActiveTokenLimitException);
        await using var verification = await IntegrationTestScope.CreateAsync(
            database.ConnectionString, migrate: false);
        Assert.Equal(2, await verification.Context.IntegrationTokens.AsNoTracking().CountAsync(
            item => item.OrganizationId == scope.OrganizationId && item.RevokedAtUtc == null,
            cancellationToken));
    }

    [Fact]
    public async Task Authenticate_fails_generically_for_malformed_unknown_wrong_and_retired_credentials()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var issued = await scope.Service.CreateAsync(scope.Command("ERP"), cancellationToken);
        var raw = issued.RawToken.Reveal();
        var wrongSecret = raw[..^1] + (raw[^1] == 'A' ? "B" : "A");
        var unknownPublicId = Base64Url.Encode(Enumerable.Repeat((byte)0xdd, 16).ToArray());
        var separator = raw.IndexOf('.');
        var unknown = $"pnt_live_{unknownPublicId}{raw[separator..]}";

        Assert.Null(await scope.Service.AuthenticateAsync("not-a-token", cancellationToken));
        Assert.Null(await scope.Service.AuthenticateAsync(wrongSecret, cancellationToken));
        Assert.Null(await scope.Service.AuthenticateAsync(unknown, cancellationToken));

        await scope.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE integrations.integration_tokens SET key_version = {'r' + "etired-v0"} WHERE id = {issued.Metadata.Id}",
            cancellationToken);
        scope.Context.ChangeTracker.Clear();
        Assert.Null(await scope.Service.AuthenticateAsync(raw, cancellationToken));
    }

    [Fact]
    public async Task Authenticate_derives_organization_and_closed_scopes_only_from_the_verified_row()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var issued = await scope.Service.CreateAsync(
            scope.Command("Writer") with
            {
                Scopes = new HashSet<IntegrationScope>
                {
                    IntegrationScope.ShipmentsRead,
                    IntegrationScope.ShipmentsWrite
                }
            },
            cancellationToken);

        var principal = await scope.Service.AuthenticateAsync(
            issued.RawToken.Reveal(), cancellationToken);

        Assert.NotNull(principal);
        Assert.Equal(issued.Metadata.Id, principal.TokenId);
        Assert.Equal(scope.OrganizationId, principal.OrganizationId);
        Assert.Equal(
            new[] { IntegrationScope.ShipmentsRead, IntegrationScope.ShipmentsWrite },
            principal.Scopes.OrderBy(static scope => scope));
    }

    [Fact]
    public async Task Authentication_coalesces_last_used_writes_to_fifteen_minutes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var issued = await scope.Service.CreateAsync(scope.Command("ERP"), cancellationToken);
        var raw = issued.RawToken.Reveal();

        Assert.NotNull(await scope.Service.AuthenticateAsync(raw, cancellationToken));
        var first = (await scope.Service.ListAsync(scope.OrganizationId, cancellationToken)).Single();
        Assert.Equal(scope.Time.GetUtcNow(), first.LastUsedAt);

        scope.Time.SetUtcNow(scope.Time.GetUtcNow().AddMinutes(14).AddSeconds(59));
        Assert.NotNull(await scope.Service.AuthenticateAsync(raw, cancellationToken));
        var coalesced = (await scope.Service.ListAsync(scope.OrganizationId, cancellationToken)).Single();
        Assert.Equal(first.LastUsedAt, coalesced.LastUsedAt);
        Assert.Equal(first.Version, coalesced.Version);

        scope.Time.SetUtcNow(scope.Time.GetUtcNow().AddSeconds(1));
        Assert.NotNull(await scope.Service.AuthenticateAsync(raw, cancellationToken));
        var updated = (await scope.Service.ListAsync(scope.OrganizationId, cancellationToken)).Single();
        Assert.Equal(scope.Time.GetUtcNow(), updated.LastUsedAt);
        Assert.Equal(first.Version + 1, updated.Version);
    }

    [Fact]
    public async Task Future_dated_and_revoked_tokens_fail_immediately()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var future = await scope.Service.CreateAsync(scope.Command("Future"), cancellationToken);
        await scope.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE integrations.integration_tokens SET created_at = {scope.Time.GetUtcNow().AddSeconds(1)} WHERE id = {future.Metadata.Id}",
            cancellationToken);
        scope.Context.ChangeTracker.Clear();
        Assert.Null(await scope.Service.AuthenticateAsync(future.RawToken.Reveal(), cancellationToken));

        using var revoked = await scope.Service.CreateAsync(scope.Command("Revoked"), cancellationToken);
        await scope.Service.RevokeAsync(
            scope.OrganizationId,
            revoked.Metadata.Id,
            scope.ActorUserId,
            revoked.Metadata.Version,
            cancellationToken);
        Assert.Null(await scope.Service.AuthenticateAsync(revoked.RawToken.Reveal(), cancellationToken));
    }

    [Fact]
    public async Task Revoke_is_tenant_concealed_optimistic_and_idempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var issued = await scope.Service.CreateAsync(scope.Command("ERP"), cancellationToken);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => scope.Service.RevokeAsync(
            Guid.CreateVersion7(),
            issued.Metadata.Id,
            scope.ActorUserId,
            issued.Metadata.Version,
            cancellationToken));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => scope.Service.RevokeAsync(
            scope.OrganizationId,
            issued.Metadata.Id,
            scope.ActorUserId,
            issued.Metadata.Version + 1,
            cancellationToken));
        Assert.NotNull(await scope.Service.AuthenticateAsync(
            issued.RawToken.Reveal(), cancellationToken));
        var latest = (await scope.Service.ListAsync(scope.OrganizationId, cancellationToken)).Single();

        await scope.Service.RevokeAsync(
            scope.OrganizationId,
            issued.Metadata.Id,
            scope.ActorUserId,
            latest.Version,
            cancellationToken);
        await scope.Service.RevokeAsync(
            scope.OrganizationId,
            issued.Metadata.Id,
            scope.ActorUserId,
            expectedVersion: 1,
            cancellationToken);

        Assert.Null(await scope.Service.AuthenticateAsync(
            issued.RawToken.Reveal(), cancellationToken));
        var metadata = (await scope.Service.ListAsync(scope.OrganizationId, cancellationToken)).Single();
        Assert.NotNull(metadata.RevokedAt);
        AssertSensitiveTextAbsent(
            issued.RawToken.Reveal(),
            JsonSerializer.Serialize(metadata));
    }

    [Fact]
    public async Task List_returns_only_the_requested_organizations_metadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = await IntegrationTestScope.CreateAsync(database.ConnectionString);
        using var own = await scope.Service.CreateAsync(scope.Command("Own"), cancellationToken);
        using var foreign = await scope.Service.CreateAsync(
            scope.Command("Foreign") with { OrganizationId = Guid.CreateVersion7() },
            cancellationToken);

        var items = await scope.Service.ListAsync(scope.OrganizationId, cancellationToken);

        var item = Assert.Single(items);
        Assert.Equal(own.Metadata.Id, item.Id);
        Assert.DoesNotContain(foreign.Metadata.PublicId, JsonSerializer.Serialize(items));
    }

    private static async Task<CreateResult> CaptureCreateAsync(
        string connectionString,
        Guid organizationId,
        Guid actorUserId,
        string displayName,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = await IntegrationTestScope.CreateAsync(connectionString, migrate: false);
            using var issued = await scope.Service.CreateAsync(
                new CreateIntegrationToken(
                    organizationId,
                    actorUserId,
                    displayName,
                    new HashSet<IntegrationScope> { IntegrationScope.ShipmentsWrite }),
                cancellationToken);
            return new CreateResult(true, null);
        }
        catch (Exception exception)
        {
            return new CreateResult(false, exception);
        }
    }

    private static void AssertSensitiveTextAbsent(string sensitive, string candidate) =>
        Assert.False(
            candidate.Contains(sensitive, StringComparison.Ordinal),
            "A metadata, JSON, debugger or tracker representation exposed bearer material.");

    private static void AssertCapturedCredentialCleared(TrackingIntegrationTokenCodec codec)
    {
        Assert.Equal(1, codec.IssueCount);
        var rawToken = Assert.IsType<SensitiveValue>(codec.CapturedRawToken);
        Assert.Throws<ObjectDisposedException>(() => rawToken.Reveal());
        var verifier = Assert.IsType<byte[]>(codec.CapturedVerifier);
        Assert.True(
            verifier.All(static value => value == 0),
            "A failed transaction lifecycle retained integration verifier bytes.");
    }

    private async Task<TException> AssertFailedCreateIsIsolatedAsync<TException>(
        TException failure,
        CancellationToken cancellationToken,
        bool failAtCommit = false)
        where TException : Exception
    {
        IFailedIntegrationTokenCreateInterceptor interceptor = failAtCommit
            ? new FailFirstIntegrationTokenCommitInterceptor(failure)
            : new FailFirstIntegrationTokenSaveInterceptor(failure);
        await using var scope = await IntegrationTestScope.CreateAsync(
            database.ConnectionString,
            integrationInterceptor: interceptor);

        var caught = await Assert.ThrowsAsync<TException>(() =>
            scope.Service.CreateAsync(scope.Command("Failed attempt"), cancellationToken));

        Assert.Same(failure, caught);
        var failedToken = Assert.IsType<IntegrationToken>(interceptor.CapturedToken);
        Assert.True(
            failedToken.SecretVerifier.All(static value => value == 0),
            "A failed create retained credential verifier bytes in memory.");
        Assert.False(
            scope.Context.ChangeTracker.Entries().Any(),
            "A failed create retained attempt-owned entities in the scoped change tracker.");

        using var issued = await scope.Service.CreateAsync(
            scope.Command("Successful retry"), cancellationToken);
        scope.Context.ChangeTracker.Clear();

        Assert.Equal(1, await scope.Context.IntegrationTokens.AsNoTracking().CountAsync(
            item => item.OrganizationId == scope.OrganizationId,
            cancellationToken));
        Assert.Equal(1, await scope.Context.IntegrationTokenScopes.AsNoTracking().CountAsync(
            item => scope.Context.IntegrationTokens.Any(token =>
                token.Id == item.TokenId && token.OrganizationId == scope.OrganizationId),
            cancellationToken));
        Assert.Equal(1, await scope.Context.SecurityEvents.AsNoTracking().CountAsync(
            item => item.OrganizationId == scope.OrganizationId,
            cancellationToken));

        return caught;
    }

    private sealed record CreateResult(bool Success, Exception? Error);
}

internal sealed class IntegrationTestScope : IAsyncDisposable
{
    private IntegrationTestScope(
        IntegrationsDbContext context,
        IntegrationTimeProvider time,
        IntegrationTokenService service)
    {
        Context = context;
        Time = time;
        Service = service;
    }

    internal Guid OrganizationId { get; } = Guid.CreateVersion7();
    internal Guid ActorUserId { get; } = Guid.CreateVersion7();
    internal IntegrationsDbContext Context { get; }
    internal IntegrationTimeProvider Time { get; }
    internal IntegrationTokenService Service { get; }

    internal static async Task<IntegrationTestScope> CreateAsync(
        string connectionString,
        DateTimeOffset? now = null,
        bool migrate = true,
        IInterceptor? integrationInterceptor = null,
        Func<IntegrationsDbContext, IIntegrationTokenTransactionFactory>?
            transactionFactoryFactory = null,
        Func<IIntegrationTokenCodec, IIntegrationTokenCodec>? tokenCodecFactory = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationsDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "integrations"));
        if (migrate)
        {
            await using var migrationContext = new IntegrationsDbContext(optionsBuilder.Options);
            await migrationContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        if (integrationInterceptor is not null)
        {
            optionsBuilder.AddInterceptors(integrationInterceptor);
        }

        var context = new IntegrationsDbContext(optionsBuilder.Options);
        var time = new IntegrationTimeProvider(
            now ?? new DateTimeOffset(2026, 8, 8, 10, 0, 0, TimeSpan.Zero));
        var keys = IntegrationKeyOptions.ForTesting(
            "integration-v1", Enumerable.Repeat((byte)0x49, 32).ToArray());
        IIntegrationTokenCodec codec = new IntegrationTokenCodec(
            keys,
            new SystemSecretGenerator());
        if (tokenCodecFactory is not null)
        {
            codec = tokenCodecFactory(codec);
        }

        var transactionFactory = transactionFactoryFactory?.Invoke(context) ??
            new EfIntegrationTokenTransactionFactory(context);
        return new IntegrationTestScope(
            context,
            time,
            new IntegrationTokenService(context, codec, transactionFactory, time));
    }

    internal CreateIntegrationToken Command(string displayName) =>
        new(
            OrganizationId,
            ActorUserId,
            displayName,
            new HashSet<IntegrationScope> { IntegrationScope.ShipmentsWrite });

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}

internal interface IFailedIntegrationTokenCreateInterceptor : IInterceptor
{
    IntegrationToken? CapturedToken { get; }
}

internal sealed class FailFirstIntegrationTokenSaveInterceptor(Exception failure)
    : SaveChangesInterceptor, IFailedIntegrationTokenCreateInterceptor
{
    private int _remainingFailures = 1;

    public IntegrationToken? CapturedToken { get; private set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _remainingFailures, 0) == 0)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        CapturedToken = eventData.Context?.ChangeTracker
            .Entries<IntegrationToken>()
            .Single()
            .Entity;
        return ValueTask.FromException<InterceptionResult<int>>(failure);
    }
}

internal sealed class FailFirstIntegrationTokenCommitInterceptor(Exception failure)
    : DbTransactionInterceptor, IFailedIntegrationTokenCreateInterceptor
{
    private int _remainingFailures = 1;

    public IntegrationToken? CapturedToken { get; private set; }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _remainingFailures, 0) == 0)
        {
            return base.TransactionCommittingAsync(
                transaction,
                eventData,
                result,
                cancellationToken);
        }

        CapturedToken = eventData.Context?.ChangeTracker
            .Entries<IntegrationToken>()
            .Single()
            .Entity;
        return ValueTask.FromException<InterceptionResult>(failure);
    }
}

internal sealed class InjectedCreateFailureException : Exception;

internal sealed class InjectedTransactionDisposalException : Exception;

internal sealed class TrackingIntegrationTokenCodec(IIntegrationTokenCodec inner)
    : IIntegrationTokenCodec
{
    internal int IssueCount { get; private set; }
    internal SensitiveValue? CapturedRawToken { get; private set; }
    internal byte[]? CapturedVerifier { get; private set; }

    public IssuedIntegrationTokenMaterial Issue()
    {
        IssueCount++;
        var material = inner.Issue();
        CapturedRawToken = material.RawToken;
        CapturedVerifier = material.SecretVerifier;
        return material;
    }

    public bool TryRead(string? token, out string publicId, out byte[] secret) =>
        inner.TryRead(token, out publicId, out secret);

    public bool Verify(
        string token,
        string expectedPublicId,
        string keyVersion,
        ReadOnlySpan<byte> verifier) =>
        inner.Verify(token, expectedPublicId, keyVersion, verifier);
}

internal sealed class FailingDisposeIntegrationTokenTransactionFactory(
    IntegrationsDbContext context,
    Exception disposalFailure) : IIntegrationTokenTransactionFactory
{
    internal int BeginCount { get; private set; }
    internal int CommitCount { get; private set; }
    internal int DisposeCount { get; private set; }

    public async Task<IIntegrationTokenTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken)
    {
        BeginCount++;
        var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new FailingDisposeIntegrationTokenTransaction(
            transaction,
            disposalFailure,
            this);
    }

    private sealed class FailingDisposeIntegrationTokenTransaction(
        IDbContextTransaction transaction,
        Exception disposalFailure,
        FailingDisposeIntegrationTokenTransactionFactory owner)
        : IIntegrationTokenTransaction
    {
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            owner.CommitCount++;
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            owner.DisposeCount++;
            await transaction.DisposeAsync();
            throw disposalFailure;
        }
    }
}

internal sealed class IntegrationTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;
    public override DateTimeOffset GetUtcNow() => _utcNow;
    internal void SetUtcNow(DateTimeOffset value) => _utcNow = value;
}
