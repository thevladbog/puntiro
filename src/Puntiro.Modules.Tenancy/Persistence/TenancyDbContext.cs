using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Tenancy.Domain;

namespace Puntiro.Modules.Tenancy.Persistence;

public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options) : DbContext(options)
{
    internal DbSet<Organization> Organizations => Set<Organization>();

    internal DbSet<Membership> Memberships => Set<Membership>();

    internal DbSet<TenancySecurityEvent> SecurityEvents => Set<TenancySecurityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        TenancyModelConfiguration.Configure(modelBuilder);
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
        if (ChangeTracker.Entries<TenancySecurityEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Tenancy security events are append-only.");
        }
    }
}
