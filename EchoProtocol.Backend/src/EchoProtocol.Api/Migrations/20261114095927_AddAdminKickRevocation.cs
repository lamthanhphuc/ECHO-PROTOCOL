using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminKickRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "KickedAtUtc",
                table: "MatchPlayerBindings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetUserId",
                table: "AdminModerationActions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminModerationActions_TargetId_TargetUserId_Action",
                table: "AdminModerationActions",
                columns: new[] { "TargetId", "TargetUserId", "Action" },
                unique: true,
                filter: "\"Action\" = 'KICK_PLAYER'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdminModerationActions_TargetId_TargetUserId_Action",
                table: "AdminModerationActions");

            migrationBuilder.DropColumn(
                name: "KickedAtUtc",
                table: "MatchPlayerBindings");

            migrationBuilder.DropColumn(
                name: "TargetUserId",
                table: "AdminModerationActions");
        }
    }
}
