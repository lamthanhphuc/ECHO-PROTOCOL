using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressionMilestones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LevelAfter",
                table: "MatchRewardGrants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LevelBefore",
                table: "MatchRewardGrants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlockedItemsJson",
                table: "MatchRewardGrants",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "AdminExperienceGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    ExperienceBefore = table.Column<long>(type: "bigint", nullable: false),
                    ExperienceAfter = table.Column<long>(type: "bigint", nullable: false),
                    LevelBefore = table.Column<int>(type: "integer", nullable: false),
                    LevelAfter = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminExperienceGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminExperienceGrants_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminExperienceGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminExperienceGrants_AdminUserId",
                table: "AdminExperienceGrants",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminExperienceGrants_UserId_CreatedAtUtc",
                table: "AdminExperienceGrants",
                columns: new[] { "UserId", "CreatedAtUtc" });
            migrationBuilder.Sql("""
                UPDATE "ShopItems" SET "RequiredLevel" = CASE "ItemName"
                    WHEN 'Core Stabilizer' THEN 4 WHEN 'Field Scanner' THEN 6
                    WHEN 'Soul Eater' THEN 12 WHEN 'Usurper' THEN 5 END
                WHERE "ItemName" IN ('Core Stabilizer', 'Field Scanner', 'Soul Eater', 'Usurper');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ShopItems" SET "RequiredLevel" = CASE "ItemName"
                    WHEN 'Core Stabilizer' THEN 1 WHEN 'Field Scanner' THEN 1
                    WHEN 'Soul Eater' THEN 5 WHEN 'Usurper' THEN 12 END
                WHERE "ItemName" IN ('Core Stabilizer', 'Field Scanner', 'Soul Eater', 'Usurper');
                """);
            migrationBuilder.DropTable(
                name: "AdminExperienceGrants");

            migrationBuilder.DropColumn(
                name: "LevelAfter",
                table: "MatchRewardGrants");

            migrationBuilder.DropColumn(
                name: "LevelBefore",
                table: "MatchRewardGrants");

            migrationBuilder.DropColumn(
                name: "UnlockedItemsJson",
                table: "MatchRewardGrants");
        }
    }
}
