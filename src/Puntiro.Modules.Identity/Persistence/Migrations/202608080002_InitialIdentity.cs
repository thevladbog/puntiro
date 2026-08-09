using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Puntiro.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "admin_users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    authentication_epoch = table.Column<long>(type: "bigint", nullable: false),
                    provisioning_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_users", x => x.id);
                    table.CheckConstraint("ck_admin_users_authentication_epoch", "authentication_epoch > 0");
                    table.CheckConstraint("ck_admin_users_status", "status IN ('provisioning', 'active', 'suspended')");
                });

            migrationBuilder.CreateTable(
                name: "security_events",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_credentials",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    password_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    memory_kib = table.Column<int>(type: "integer", nullable: false),
                    iterations = table.Column<int>(type: "integer", nullable: false),
                    parallelism = table.Column<int>(type: "integer", nullable: false),
                    algorithm = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_rehashed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_credentials", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_password_credentials_admin_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recovery_codes",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verifier = table.Column<byte[]>(type: "bytea", nullable: false),
                    key_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recovery_codes", x => x.id);
                    table.ForeignKey(
                        name: "fk_recovery_codes_admin_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verifier = table.Column<byte[]>(type: "bytea", nullable: false),
                    key_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authentication_epoch = table.Column<long>(type: "bigint", nullable: false),
                    active_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idle_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    second_factor_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.CheckConstraint("ck_sessions_authentication_epoch", "authentication_epoch > 0");
                    table.ForeignKey(
                        name: "fk_sessions_admin_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "totp_credentials",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    protected_secret = table.Column<byte[]>(type: "bytea", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_accepted_counter = table.Column<long>(type: "bigint", nullable: true),
                    replaced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_totp_credentials", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_totp_credentials_admin_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_provisioning_organization_id",
                schema: "identity",
                table: "admin_users",
                column: "provisioning_organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_admin_users_normalized_email",
                schema: "identity",
                table: "admin_users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recovery_codes_user_batch",
                schema: "identity",
                table: "recovery_codes",
                columns: new[] { "user_id", "batch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recovery_codes_user_used_at",
                schema: "identity",
                table: "recovery_codes",
                columns: new[] { "user_id", "used_at" });

            migrationBuilder.CreateIndex(
                name: "ix_security_events_user_occurred_at",
                schema: "identity",
                table: "security_events",
                columns: new[] { "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_expiry",
                schema: "identity",
                table: "sessions",
                columns: new[] { "idle_expires_at", "absolute_expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_revoked_at",
                schema: "identity",
                table: "sessions",
                columns: new[] { "user_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ux_sessions_public_id",
                schema: "identity",
                table: "sessions",
                column: "public_id",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION identity.reject_security_event_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $body$
                BEGIN
                    RAISE EXCEPTION 'identity security events are append-only'
                        USING ERRCODE = '55000';
                END;
                $body$;

                CREATE TRIGGER identity_security_events_append_only
                BEFORE UPDATE OR DELETE ON identity.security_events
                FOR EACH ROW EXECUTE FUNCTION identity.reject_security_event_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS identity_security_events_append_only
                    ON identity.security_events;
                DROP FUNCTION IF EXISTS identity.reject_security_event_mutation();
                """);

            migrationBuilder.DropTable(
                name: "password_credentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "recovery_codes",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "security_events",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "totp_credentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "admin_users",
                schema: "identity");
        }
    }
}
