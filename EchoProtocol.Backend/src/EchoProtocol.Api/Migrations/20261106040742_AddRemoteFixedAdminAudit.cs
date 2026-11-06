using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteFixedAdminAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemoteFixedBalanceAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MapId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProtocolVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreviousVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TargetVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteFixedBalanceAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteFixedBalanceAuditEntries_MapId_Difficulty_CreatedAtUtc",
                table: "RemoteFixedBalanceAuditEntries",
                columns: new[] { "MapId", "Difficulty", "CreatedAtUtc" });

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_remote_fixed_audit_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'RemoteFixedBalanceAuditEntries are immutable';
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER trg_remote_fixed_audit_immutable
                BEFORE UPDATE OR DELETE ON "RemoteFixedBalanceAuditEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_remote_fixed_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_remote_fixed_audit_immutable ON "RemoteFixedBalanceAuditEntries";
                DROP FUNCTION IF EXISTS prevent_remote_fixed_audit_mutation();
                """);
            migrationBuilder.DropTable(
                name: "RemoteFixedBalanceAuditEntries");
        }
    }
}
