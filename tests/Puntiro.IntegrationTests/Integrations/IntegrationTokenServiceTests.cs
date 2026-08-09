using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

        Assert.Matches("^pnt_live_[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+$", raw);
        Assert.Equal("ERP connector", issued.Metadata.DisplayName);
        Assert.Equal(32, row.SecretVerifier.Length);
        Assert.NotEqual(raw, Convert.ToBase64String(row.SecretVerifier));
        Assert.DoesNotContain(raw, scope.Context.ChangeTracker.DebugView.LongView);
        Assert.DoesNotContain(raw, JsonSerializer.Serialize(row));
        Assert.DoesNotContain(raw, JsonSerializer.Serialize(issued));
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
        Assert.DoesNotContain(issued.RawToken.Reveal(), JsonSerializer.Serialize(metadata));
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
        bool migrate = true)
    {
        var options = new DbContextOptionsBuilder<IntegrationsDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "integrations"))
            .Options;
        var context = new IntegrationsDbContext(options);
        if (migrate)
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        var time = new IntegrationTimeProvider(
            now ?? new DateTimeOffset(2026, 8, 8, 10, 0, 0, TimeSpan.Zero));
        var keys = IntegrationKeyOptions.ForTesting(
            "integration-v1", Enumerable.Repeat((byte)0x49, 32).ToArray());
        var codec = new IntegrationTokenCodec(keys, new SystemSecretGenerator());
        return new IntegrationTestScope(
            context,
            time,
            new IntegrationTokenService(context, codec, time));
    }

    internal CreateIntegrationToken Command(string displayName) =>
        new(
            OrganizationId,
            ActorUserId,
            displayName,
            new HashSet<IntegrationScope> { IntegrationScope.ShipmentsWrite });

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}

internal sealed class IntegrationTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;
    public override DateTimeOffset GetUtcNow() => _utcNow;
    internal void SetUtcNow(DateTimeOffset value) => _utcNow = value;
}
