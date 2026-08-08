using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Tenancy.Domain;

namespace Puntiro.Modules.Tenancy.Persistence;

internal static class TenancyModelConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenancy");
        ConfigureOrganization(modelBuilder);
        ConfigureMembership(modelBuilder);
        ConfigureSecurityEvent(modelBuilder);
    }

    private static void ConfigureOrganization(ModelBuilder modelBuilder)
    {
        var organization = modelBuilder.Entity<Organization>();
        organization.ToTable("organizations");
        organization.HasKey(item => item.Id).HasName("pk_organizations");
        organization.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        organization.Property(item => item.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();
        organization.Property(item => item.Slug)
            .HasColumnName("slug")
            .HasMaxLength(63)
            .IsRequired();
        organization.Property(item => item.Status)
            .HasColumnName("status")
            .HasConversion(
                status => ToDatabaseValue(status),
                value => ParseOrganizationStatus(value))
            .HasMaxLength(32)
            .IsRequired();
        organization.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        organization.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        organization.Property(item => item.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        organization.HasIndex(item => item.Slug)
            .IsUnique()
            .HasDatabaseName("ux_organizations_slug");
    }

    private static void ConfigureMembership(ModelBuilder modelBuilder)
    {
        var membership = modelBuilder.Entity<Membership>();
        membership.ToTable("memberships");
        membership.HasKey(item => item.Id).HasName("pk_memberships");
        membership.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        membership.Property(item => item.OrganizationId).HasColumnName("organization_id");
        membership.Property(item => item.UserId).HasColumnName("user_id");
        membership.Property(item => item.Role)
            .HasColumnName("role")
            .HasConversion(
                role => ToDatabaseValue(role),
                value => ParseMembershipRole(value))
            .HasMaxLength(32)
            .IsRequired();
        membership.Property(item => item.Status)
            .HasColumnName("status")
            .HasConversion(
                status => ToDatabaseValue(status),
                value => ParseMembershipStatus(value))
            .HasMaxLength(32)
            .IsRequired();
        membership.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        membership.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        membership.Property(item => item.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        membership.HasIndex(item => new { item.OrganizationId, item.UserId })
            .IsUnique()
            .HasDatabaseName("ux_memberships_organization_user");
        membership.HasIndex(item => item.UserId)
            .HasDatabaseName("ix_memberships_user_id");
        membership.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(item => item.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_memberships_organizations_organization_id");
    }

    private static void ConfigureSecurityEvent(ModelBuilder modelBuilder)
    {
        var securityEvent = modelBuilder.Entity<TenancySecurityEvent>();
        securityEvent.ToTable("security_events");
        securityEvent.HasKey(item => item.Id).HasName("pk_security_events");
        securityEvent.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        securityEvent.Property(item => item.OrganizationId).HasColumnName("organization_id");
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
        securityEvent.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(item => item.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_security_events_organizations_organization_id");
    }

    private static string ToDatabaseValue(OrganizationStatus status)
    {
        return status switch
        {
            OrganizationStatus.Provisioning => "provisioning",
            OrganizationStatus.Active => "active",
            OrganizationStatus.Suspended => "suspended",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown organization status.")
        };
    }

    private static OrganizationStatus ParseOrganizationStatus(string value)
    {
        return value switch
        {
            "provisioning" => OrganizationStatus.Provisioning,
            "active" => OrganizationStatus.Active,
            "suspended" => OrganizationStatus.Suspended,
            _ => throw new InvalidOperationException("Unknown persisted organization status.")
        };
    }

    private static string ToDatabaseValue(MembershipRole role)
    {
        return role switch
        {
            MembershipRole.Owner => "owner",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown membership role.")
        };
    }

    private static MembershipRole ParseMembershipRole(string value)
    {
        return value switch
        {
            "owner" => MembershipRole.Owner,
            _ => throw new InvalidOperationException("Unknown persisted membership role.")
        };
    }

    private static string ToDatabaseValue(MembershipStatus status)
    {
        return status switch
        {
            MembershipStatus.Active => "active",
            MembershipStatus.Revoked => "revoked",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown membership status.")
        };
    }

    private static MembershipStatus ParseMembershipStatus(string value)
    {
        return value switch
        {
            "active" => MembershipStatus.Active,
            "revoked" => MembershipStatus.Revoked,
            _ => throw new InvalidOperationException("Unknown persisted membership status.")
        };
    }
}
