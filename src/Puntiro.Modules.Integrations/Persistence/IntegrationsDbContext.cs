using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Integrations.Domain;

namespace Puntiro.Modules.Integrations.Persistence;

public sealed class IntegrationsDbContext(DbContextOptions<IntegrationsDbContext> options)
    : DbContext(options)
{
    internal DbSet<IntegrationToken> IntegrationTokens => Set<IntegrationToken>();

    internal DbSet<IntegrationTokenScope> IntegrationTokenScopes => Set<IntegrationTokenScope>();

    internal DbSet<IntegrationSecurityEvent> SecurityEvents => Set<IntegrationSecurityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        IntegrationsModelConfiguration.Configure(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureSecurityEventsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureSecurityEventsAreAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureSecurityEventsAreAppendOnly()
    {
        if (ChangeTracker.Entries<IntegrationSecurityEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Integration security events are append-only.");
        }
    }
}
