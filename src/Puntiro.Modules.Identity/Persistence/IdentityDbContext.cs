using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Identity.Domain;

namespace Puntiro.Modules.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    internal DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    internal DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();
    internal DbSet<TotpCredential> TotpCredentials => Set<TotpCredential>();
    internal DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
    internal DbSet<AdminSession> Sessions => Set<AdminSession>();
    internal DbSet<IdentitySecurityEvent> SecurityEvents => Set<IdentitySecurityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        IdentityModelConfiguration.Configure(modelBuilder);

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
        if (ChangeTracker.Entries<IdentitySecurityEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Identity security events are append-only.");
        }
    }
}
