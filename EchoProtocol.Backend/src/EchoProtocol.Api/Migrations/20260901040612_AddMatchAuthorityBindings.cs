using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchAuthorityBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchAuthorityBindings",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FusionSessionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchAuthorityBindings", x => x.MatchId);
                    table.ForeignKey(
                        name: "FK_MatchAuthorityBindings_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayerBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FusionActorNumber = table.Column<int>(type: "integer", nullable: false),
                    JoinProofId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayerBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchPlayerBindings_MatchAuthorityBindings_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchAuthorityBindings",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchPlayerBindings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchAuthorityBindings_FusionSessionName_Status",
                table: "MatchAuthorityBindings",
                columns: new[] { "FusionSessionName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchAuthorityBindings_HostUserId",
                table: "MatchAuthorityBindings",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerBindings_JoinProofId",
                table: "MatchPlayerBindings",
                column: "JoinProofId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerBindings_MatchId_FusionActorNumber",
                table: "MatchPlayerBindings",
                columns: new[] { "MatchId", "FusionActorNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerBindings_MatchId_UserId",
                table: "MatchPlayerBindings",
                columns: new[] { "MatchId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerBindings_UserId",
                table: "MatchPlayerBindings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPlayerBindings");

            migrationBuilder.DropTable(
                name: "MatchAuthorityBindings");
        }
    }
}
