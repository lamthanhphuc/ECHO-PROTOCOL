using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminModerationActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminModerationActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminModerationActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminModerationActions_TargetId_CreatedAtUtc",
                table: "AdminModerationActions",
                columns: new[] { "TargetId", "CreatedAtUtc" });

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_admin_moderation_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'AdminModerationActions are immutable';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_admin_moderation_immutable
                BEFORE UPDATE OR DELETE ON "AdminModerationActions"
                FOR EACH ROW
                EXECUTE FUNCTION prevent_admin_moderation_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_admin_moderation_immutable
                ON "AdminModerationActions";

                DROP FUNCTION IF EXISTS prevent_admin_moderation_mutation();
                """);

            migrationBuilder.DropTable(
                name: "AdminModerationActions");
        }
    }
}
