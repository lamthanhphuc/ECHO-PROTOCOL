using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomSessionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoomMatchRequestId",
                table: "MatchAuthorityBindings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomSessionId",
                table: "MatchAuthorityBindings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HostExecutedAtUtc",
                table: "AdminModerationActions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoomSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FusionSessionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Region = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActualRegion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CurrentMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentMatchRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchAuthorityBindings_RoomSessionId_RoomMatchRequestId",
                table: "MatchAuthorityBindings",
                columns: new[] { "RoomSessionId", "RoomMatchRequestId" },
                unique: true,
                filter: "\"RoomMatchRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MatchAuthorityBindings_RoomSessionId_Status",
                table: "MatchAuthorityBindings",
                columns: new[] { "RoomSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomSessions_CreateRequestId",
                table: "RoomSessions",
                column: "CreateRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomSessions_FusionSessionName",
                table: "RoomSessions",
                column: "FusionSessionName",
                unique: true,
                filter: "\"ClosedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoomSessions_HostUserId",
                table: "RoomSessions",
                column: "HostUserId",
                unique: true,
                filter: "\"ClosedAtUtc\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchAuthorityBindings_RoomSessions_RoomSessionId",
                table: "MatchAuthorityBindings",
                column: "RoomSessionId",
                principalTable: "RoomSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchAuthorityBindings_RoomSessions_RoomSessionId",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropTable(
                name: "RoomSessions");

            migrationBuilder.DropIndex(
                name: "IX_MatchAuthorityBindings_RoomSessionId_RoomMatchRequestId",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropIndex(
                name: "IX_MatchAuthorityBindings_RoomSessionId_Status",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "RoomMatchRequestId",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "RoomSessionId",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "HostExecutedAtUtc",
                table: "AdminModerationActions");
        }
    }
}
