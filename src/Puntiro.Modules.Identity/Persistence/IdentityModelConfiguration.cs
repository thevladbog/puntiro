using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Domain;

namespace Puntiro.Modules.Identity.Persistence;

internal static class IdentityModelConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        ConfigureUser(modelBuilder);
        ConfigurePassword(modelBuilder);
        ConfigureTotp(modelBuilder);
        ConfigureRecovery(modelBuilder);
        ConfigureSession(modelBuilder);
        ConfigureSecurityEvent(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AdminUser>();
        entity.ToTable("admin_users", table =>
        {
            table.HasCheckConstraint(
                "ck_admin_users_status", "status IN ('provisioning', 'active', 'suspended')");
            table.HasCheckConstraint(
                "ck_admin_users_authentication_epoch", "authentication_epoch > 0");
        });
        entity.HasKey(item => item.Id).HasName("pk_admin_users");
        entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(item => item.DisplayEmail).HasColumnName("display_email").HasMaxLength(320).IsRequired();
        entity.Property(item => item.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        entity.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired()
            .HasConversion(value => ToDatabaseValue(value), value => ParseUserStatus(value));
        entity.Property(item => item.AuthenticationEpoch).HasColumnName("authentication_epoch");
        entity.Property(item => item.ProvisioningOrganizationId).HasColumnName("provisioning_organization_id");
        entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().ValueGeneratedNever();
        entity.HasIndex(item => item.NormalizedEmail).IsUnique().HasDatabaseName("ux_admin_users_normalized_email");
        entity.HasIndex(item => item.ProvisioningOrganizationId).HasDatabaseName("ix_admin_users_provisioning_organization_id");
    }

    private static void ConfigurePassword(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PasswordCredential>();
        entity.ToTable("password_credentials");
        entity.HasKey(item => item.UserId).HasName("pk_password_credentials");
        entity.Property(item => item.UserId).HasColumnName("user_id").ValueGeneratedNever();
        entity.Property(item => item.Salt).HasColumnName("salt").HasColumnType("bytea").IsRequired();
        entity.Property(item => item.Hash).HasColumnName("password_hash").HasColumnType("bytea").IsRequired();
        entity.Property(item => item.MemoryKiB).HasColumnName("memory_kib");
        entity.Property(item => item.Iterations).HasColumnName("iterations");
        entity.Property(item => item.Parallelism).HasColumnName("parallelism");
        entity.Property(item => item.Algorithm).HasColumnName("algorithm").HasMaxLength(32).IsRequired();
        entity.Property(item => item.SetAtUtc).HasColumnName("set_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.LastRehashedAtUtc).HasColumnName("last_rehashed_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().ValueGeneratedNever();
        OwnsUser(entity);
    }

    private static void ConfigureTotp(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TotpCredential>();
        entity.ToTable("totp_credentials");
        entity.HasKey(item => item.UserId).HasName("pk_totp_credentials");
        entity.Property(item => item.UserId).HasColumnName("user_id").ValueGeneratedNever();
        entity.Property(item => item.ProtectedSecret).HasColumnName("protected_secret").HasColumnType("bytea").IsRequired();
        entity.Property(item => item.ConfirmedAtUtc).HasColumnName("confirmed_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.LastAcceptedCounter).HasColumnName("last_accepted_counter");
        entity.Property(item => item.ReplacedAtUtc).HasColumnName("replaced_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().ValueGeneratedNever();
        OwnsUser(entity);
    }

    private static void ConfigureRecovery(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RecoveryCode>();
        entity.ToTable("recovery_codes");
        entity.HasKey(item => item.Id).HasName("pk_recovery_codes");
        entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(item => item.UserId).HasColumnName("user_id");
        entity.Property(item => item.BatchId).HasColumnName("batch_id");
        entity.Property(item => item.Verifier).HasColumnName("verifier").HasColumnType("bytea").IsRequired();
        entity.Property(item => item.KeyVersion).HasColumnName("key_version").HasMaxLength(32).IsRequired();
        entity.Property(item => item.IssuedAtUtc).HasColumnName("issued_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.UsedAtUtc).HasColumnName("used_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().ValueGeneratedNever();
        entity.HasIndex(item => new { item.UserId, item.UsedAtUtc }).HasDatabaseName("ix_recovery_codes_user_used_at");
        entity.HasIndex(item => new { item.UserId, item.BatchId }).HasDatabaseName("ix_recovery_codes_user_batch");
        OwnsUser(entity);
    }

    private static void ConfigureSession(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AdminSession>();
        entity.ToTable("sessions", table => table.HasCheckConstraint(
            "ck_sessions_authentication_epoch", "authentication_epoch > 0"));
        entity.HasKey(item => item.Id).HasName("pk_sessions");
        entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(item => item.PublicId).HasColumnName("public_id");
        entity.Property(item => item.Verifier).HasColumnName("verifier").HasColumnType("bytea").IsRequired();
        entity.Property(item => item.KeyVersion).HasColumnName("key_version").HasMaxLength(32).IsRequired();
        entity.Property(item => item.UserId).HasColumnName("user_id");
        entity.Property(item => item.AuthenticationEpoch).HasColumnName("authentication_epoch");
        entity.Property(item => item.ActiveOrganizationId).HasColumnName("active_organization_id");
        entity.Property(item => item.CreatedAtUtc).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.LastSeenAtUtc).HasColumnName("last_seen_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.IdleExpiresAtUtc).HasColumnName("idle_expires_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.SecondFactorVerifiedAtUtc).HasColumnName("second_factor_verified_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.RevokedAtUtc).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
        entity.Property(item => item.RevokeReason).HasColumnName("revoke_reason").HasMaxLength(80);
        entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken().ValueGeneratedNever();
        entity.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("ux_sessions_public_id");
        entity.HasIndex(item => new { item.UserId, item.RevokedAtUtc }).HasDatabaseName("ix_sessions_user_revoked_at");
        entity.HasIndex(item => new { item.IdleExpiresAtUtc, item.AbsoluteExpiresAtUtc }).HasDatabaseName("ix_sessions_expiry");
        OwnsUser(entity);
    }

    private static void ConfigureSecurityEvent(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<IdentitySecurityEvent>();
        entity.ToTable("security_events");
        entity.HasKey(item => item.Id).HasName("pk_security_events");
        entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(item => item.UserId).HasColumnName("user_id");
        entity.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
        entity.Property(item => item.OrganizationId).HasColumnName("organization_id");
        entity.Property(item => item.SessionId).HasColumnName("session_id");
        entity.Property(item => item.TraceId).HasColumnName("trace_id")
            .HasMaxLength(IdentityAuditContext.MaximumTraceIdLength)
            .IsRequired();
        entity.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(80).IsRequired();
        entity.Property(item => item.Result).HasColumnName("result").HasMaxLength(32).IsRequired();
        entity.Property(item => item.ReasonCode).HasColumnName("reason_code").HasMaxLength(80).IsRequired();
        entity.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at").HasColumnType("timestamp with time zone");
        entity.HasIndex(item => new { item.UserId, item.OccurredAtUtc }).HasDatabaseName("ix_security_events_user_occurred_at");
    }

    private static void OwnsUser<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        entity.HasOne<AdminUser>().WithMany().HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName($"fk_{entity.Metadata.GetTableName()}_admin_users_user_id");
    }

    private static string ToDatabaseValue(AdminUserStatus value) => value switch
    {
        AdminUserStatus.Provisioning => "provisioning",
        AdminUserStatus.Active => "active",
        AdminUserStatus.Suspended => "suspended",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown admin user status.")
    };

    private static AdminUserStatus ParseUserStatus(string value) => value switch
    {
        "provisioning" => AdminUserStatus.Provisioning,
        "active" => AdminUserStatus.Active,
        "suspended" => AdminUserStatus.Suspended,
        _ => throw new InvalidOperationException("Unknown persisted admin user status.")
    };
}
