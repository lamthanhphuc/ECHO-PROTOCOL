using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteFixedBalanceV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BalanceHash",
                table: "MatchAuthorityBindings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BalanceProtocolVersion",
                table: "MatchAuthorityBindings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BalanceSource",
                table: "MatchAuthorityBindings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BalanceVersion",
                table: "MatchAuthorityBindings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchDataPolicy",
                table: "MatchAuthorityBindings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "STANDARD");

            migrationBuilder.AddColumn<string>(
                name: "ResolutionMode",
                table: "MatchAuthorityBindings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RemoteFixedBalanceVersions",
                columns: table => new
                {
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MapId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProtocolVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteFixedBalanceVersions", x => x.Version);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchAuthorityBindings_BalanceVersion",
                table: "MatchAuthorityBindings",
                column: "BalanceVersion");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings",
                sql: "COALESCE((\r\n    \"ResolutionMode\" IS NULL\r\n    AND \"BalanceSource\" IS NULL\r\n    AND \"BalanceProtocolVersion\" IS NULL\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" = 'STANDARD'\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'LOCAL'\r\n    AND \"ResolutionMode\" IN ('FIXED', 'ADAPTIVE')\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" = 'STANDARD'\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'REMOTE_FIXED'\r\n    AND \"ResolutionMode\" = 'FIXED'\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NOT NULL\r\n    AND \"BalanceHash\" IS NOT NULL\r\n    AND \"MatchDataPolicy\" = 'TEST_ONLY'\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n    AND \"MapId\" IN ('SciFi', 'Scene_A')\r\n    AND \"Difficulty\" IN ('EASY', 'NORMAL', 'HARD')\r\n), FALSE)");

            migrationBuilder.CreateIndex(
                name: "UX_RemoteFixedBalance_Active",
                table: "RemoteFixedBalanceVersions",
                columns: new[] { "MapId", "Difficulty", "ProtocolVersion" },
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchAuthorityBindings_RemoteFixedBalanceVersions_BalanceVe~",
                table: "MatchAuthorityBindings",
                column: "BalanceVersion",
                principalTable: "RemoteFixedBalanceVersions",
                principalColumn: "Version",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION rfb_guard_version()
                RETURNS trigger AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'REMOTE_FIXED_VERSION_IMMUTABLE';
                    END IF;

                    IF (to_jsonb(NEW) - 'IsActive') IS DISTINCT FROM (to_jsonb(OLD) - 'IsActive') THEN
                        RAISE EXCEPTION 'REMOTE_FIXED_VERSION_IMMUTABLE';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_rfb_guard_version
                BEFORE UPDATE OR DELETE ON "RemoteFixedBalanceVersions"
                FOR EACH ROW EXECUTE FUNCTION rfb_guard_version();
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION rfb_guard_match_context()
                RETURNS trigger AS $$
                BEGIN
                    IF OLD."StartedAtUtc" IS NOT NULL AND
                    (
                        NEW."StartedAtUtc" IS DISTINCT FROM OLD."StartedAtUtc"
                        OR NEW."MapId" IS DISTINCT FROM OLD."MapId"
                        OR NEW."Difficulty" IS DISTINCT FROM OLD."Difficulty"
                        OR NEW."ResolutionMode" IS DISTINCT FROM OLD."ResolutionMode"
                        OR NEW."BalanceSource" IS DISTINCT FROM OLD."BalanceSource"
                        OR NEW."BalanceProtocolVersion" IS DISTINCT FROM OLD."BalanceProtocolVersion"
                        OR NEW."BalanceVersion" IS DISTINCT FROM OLD."BalanceVersion"
                        OR NEW."BalanceHash" IS DISTINCT FROM OLD."BalanceHash"
                        OR NEW."MatchDataPolicy" IS DISTINCT FROM OLD."MatchDataPolicy"
                    ) THEN
                        RAISE EXCEPTION 'MATCH_BALANCE_CONTEXT_IMMUTABLE';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_rfb_guard_match_context
                BEFORE UPDATE ON "MatchAuthorityBindings"
                FOR EACH ROW EXECUTE FUNCTION rfb_guard_match_context();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_rfb_guard_match_context ON "MatchAuthorityBindings";
                DROP FUNCTION IF EXISTS rfb_guard_match_context();
                DROP TRIGGER IF EXISTS trg_rfb_guard_version ON "RemoteFixedBalanceVersions";
                DROP FUNCTION IF EXISTS rfb_guard_version();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MatchAuthorityBindings_RemoteFixedBalanceVersions_BalanceVe~",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropTable(
                name: "RemoteFixedBalanceVersions");

            migrationBuilder.DropIndex(
                name: "IX_MatchAuthorityBindings_BalanceVersion",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "BalanceHash",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "BalanceProtocolVersion",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "BalanceSource",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "BalanceVersion",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "MatchDataPolicy",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "ResolutionMode",
                table: "MatchAuthorityBindings");
        }
    }
}
