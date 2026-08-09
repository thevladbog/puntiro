using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Integrations.Domain;

namespace Puntiro.Modules.Integrations.Persistence;

internal static class IntegrationsModelConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("integrations");
        ConfigureToken(modelBuilder);
        ConfigureScope(modelBuilder);
        ConfigureSecurityEvent(modelBuilder);
    }

    private static void ConfigureToken(ModelBuilder modelBuilder)
    {
        var token = modelBuilder.Entity<IntegrationToken>();
        token.ToTable(
            "integration_tokens",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_integration_tokens_verifier_length",
                    "octet_length(secret_verifier) = 32");
                table.HasCheckConstraint(
                    "ck_integration_tokens_positive_version",
                    "version > 0");
                table.HasCheckConstraint(
                    "ck_integration_tokens_active_slot",
                    "(revoked_at IS NULL AND active_slot IN (1, 2)) OR " +
                    "(revoked_at IS NOT NULL AND active_slot IS NULL)");
                table.HasCheckConstraint(
                    "ck_integration_tokens_revoke_metadata",
                    "(revoked_at IS NULL AND revoked_by_user_id IS NULL AND revoke_reason IS NULL) OR " +
                    "(revoked_at IS NOT NULL AND revoked_by_user_id IS NOT NULL AND revoke_reason IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_integration_tokens_last_used_time",
                    "last_used_at IS NULL OR last_used_at >= created_at");
                table.HasCheckConstraint(
                    "ck_integration_tokens_revoked_time",
                    "revoked_at IS NULL OR revoked_at >= created_at");
            });
        token.HasKey(item => item.Id).HasName("pk_integration_tokens");
        token.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        token.Property(item => item.PublicId)
            .HasColumnName("public_id")
            .HasMaxLength(22)
            .IsRequired();
        token.Property(item => item.OrganizationId)
            .HasColumnName("organization_id");
        token.Property(item => item.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100)
            .IsRequired();
        token.Property(item => item.SecretVerifier)
            .HasColumnName("secret_verifier")
            .HasColumnType("bytea")
            .IsRequired();
        token.Property(item => item.KeyVersion)
            .HasColumnName("key_version")
            .HasMaxLength(32)
            .IsRequired();
        token.Property(item => item.ActiveSlot)
            .HasColumnName("active_slot");
        token.Property(item => item.CreatedByUserId)
            .HasColumnName("created_by_user_id");
        token.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        token.Property(item => item.LastUsedAtUtc)
            .HasColumnName("last_used_at")
            .HasColumnType("timestamp with time zone");
        token.Property(item => item.RevokedAtUtc)
            .HasColumnName("revoked_at")
            .HasColumnType("timestamp with time zone");
        token.Property(item => item.RevokedByUserId)
            .HasColumnName("revoked_by_user_id");
        token.Property(item => item.RevokeReason)
            .HasColumnName("revoke_reason")
            .HasMaxLength(80);
        token.Property(item => item.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        token.Ignore(item => item.ScopeValues);
        token.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_integration_tokens_public_id");
        token.HasIndex(item => new { item.OrganizationId, item.RevokedAtUtc })
            .HasDatabaseName("ix_integration_tokens_organization_revoked_at");
        token.HasIndex(item => new { item.OrganizationId, item.ActiveSlot })
            .IsUnique()
            .HasFilter("revoked_at IS NULL")
            .HasDatabaseName("ux_integration_tokens_organization_active_slot");
        token.HasMany(item => item.Scopes)
            .WithOne()
            .HasForeignKey(item => item.TokenId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_integration_token_scopes_tokens_token_id");
        token.Navigation(item => item.Scopes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureScope(ModelBuilder modelBuilder)
    {
        var scope = modelBuilder.Entity<IntegrationTokenScope>();
        scope.ToTable(
            "integration_token_scopes",
            table => table.HasCheckConstraint(
                "ck_integration_token_scopes_scope",
                "scope IN ('shipments.read', 'shipments.write')"));
        scope.HasKey(item => new { item.TokenId, item.Scope })
            .HasName("pk_integration_token_scopes");
        scope.Property(item => item.TokenId)
            .HasColumnName("token_id")
            .ValueGeneratedNever();
        scope.Property(item => item.Scope)
            .HasColumnName("scope")
            .HasConversion(
                value => IntegrationScopes.ToDatabaseValue(value),
                value => IntegrationScopes.ParseDatabaseValue(value))
            .HasMaxLength(32)
            .IsRequired();
    }

    private static void ConfigureSecurityEvent(ModelBuilder modelBuilder)
    {
        var securityEvent = modelBuilder.Entity<IntegrationSecurityEvent>();
        securityEvent.ToTable("security_events");
        securityEvent.HasKey(item => item.Id).HasName("pk_security_events");
        securityEvent.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        securityEvent.Property(item => item.OrganizationId)
            .HasColumnName("organization_id");
        securityEvent.Property(item => item.ActorUserId)
            .HasColumnName("actor_user_id");
        securityEvent.Property(item => item.TokenId)
            .HasColumnName("token_id");
        securityEvent.Property(item => item.TraceId)
            .HasColumnName("trace_id")
            .HasMaxLength(IntegrationSecurityEvent.MaximumTraceIdLength)
            .IsRequired();
        securityEvent.Property(item => item.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(80)
            .IsRequired();
        securityEvent.Property(item => item.Result)
            .HasColumnName("result")
            .HasMaxLength(32)
            .IsRequired();
        securityEvent.Property(item => item.ReasonCode)
            .HasColumnName("reason_code")
            .HasMaxLength(80)
            .IsRequired();
        securityEvent.Property(item => item.OccurredAtUtc)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        securityEvent.HasIndex(item => new { item.OrganizationId, item.OccurredAtUtc })
            .HasDatabaseName("ix_security_events_organization_occurred_at");
        securityEvent.HasOne<IntegrationToken>()
            .WithMany()
            .HasForeignKey(item => item.TokenId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_security_events_integration_tokens_token_id");
    }
}
