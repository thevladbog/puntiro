using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Puntiro.Modules.Integrations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integrations");

            migrationBuilder.CreateTable(
                name: "integration_tokens",
                schema: "integrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<string>(type: "character varying(22)", maxLength: 22, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    secret_verifier = table.Column<byte[]>(type: "bytea", nullable: false),
                    key_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    active_slot = table.Column<short>(type: "smallint", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_tokens", x => x.id);
                    table.CheckConstraint("ck_integration_tokens_active_slot", "(revoked_at IS NULL AND active_slot IN (1, 2)) OR (revoked_at IS NOT NULL AND active_slot IS NULL)");
                    table.CheckConstraint("ck_integration_tokens_last_used_time", "last_used_at IS NULL OR last_used_at >= created_at");
                    table.CheckConstraint("ck_integration_tokens_positive_version", "version > 0");
                    table.CheckConstraint("ck_integration_tokens_revoke_metadata", "(revoked_at IS NULL AND revoked_by_user_id IS NULL AND revoke_reason IS NULL) OR (revoked_at IS NOT NULL AND revoked_by_user_id IS NOT NULL AND revoke_reason IS NOT NULL)");
                    table.CheckConstraint("ck_integration_tokens_revoked_time", "revoked_at IS NULL OR revoked_at >= created_at");
                    table.CheckConstraint("ck_integration_tokens_verifier_length", "octet_length(secret_verifier) = 32");
                });

            migrationBuilder.CreateTable(
                name: "integration_token_scopes",
                schema: "integrations",
                columns: table => new
                {
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_token_scopes", x => new { x.token_id, x.scope });
                    table.CheckConstraint("ck_integration_token_scopes_scope", "scope IN ('shipments.read', 'shipments.write')");
                    table.ForeignKey(
                        name: "fk_integration_token_scopes_tokens_token_id",
                        column: x => x.token_id,
                        principalSchema: "integrations",
                        principalTable: "integration_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_events",
                schema: "integrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trace_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_security_events_integration_tokens_token_id",
                        column: x => x.token_id,
                        principalSchema: "integrations",
                        principalTable: "integration_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_tokens_organization_revoked_at",
                schema: "integrations",
                table: "integration_tokens",
                columns: new[] { "organization_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ux_integration_tokens_organization_active_slot",
                schema: "integrations",
                table: "integration_tokens",
                columns: new[] { "organization_id", "active_slot" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_integration_tokens_public_id",
                schema: "integrations",
                table: "integration_tokens",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_security_events_organization_occurred_at",
                schema: "integrations",
                table: "security_events",
                columns: new[] { "organization_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_security_events_token_id",
                schema: "integrations",
                table: "security_events",
                column: "token_id");

            migrationBuilder.Sql(
                """
                CREATE FUNCTION integrations.reject_security_event_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $body$
                BEGIN
                    RAISE EXCEPTION 'integration security events are append-only'
                        USING ERRCODE = '55000';
                END;
                $body$;

                CREATE TRIGGER integrations_security_events_append_only
                BEFORE UPDATE OR DELETE ON integrations.security_events
                FOR EACH ROW EXECUTE FUNCTION integrations.reject_security_event_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS integrations_security_events_append_only
                    ON integrations.security_events;
                DROP FUNCTION IF EXISTS integrations.reject_security_event_mutation();
                """);

            migrationBuilder.DropTable(
                name: "integration_token_scopes",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "security_events",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "integration_tokens",
                schema: "integrations");
        }
    }
}
