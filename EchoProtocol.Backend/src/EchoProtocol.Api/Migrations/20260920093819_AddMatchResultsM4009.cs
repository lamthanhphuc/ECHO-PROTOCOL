using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchResultsM4009 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchPlayerBindings_MatchId_UserId",
                table: "MatchPlayerBindings");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "MatchAuthorityBindings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MatchPlayerBindings_MatchId_UserId",
                table: "MatchPlayerBindings",
                columns: new[] { "MatchId", "UserId" });

            migrationBuilder.CreateTable(
                name: "MatchResults",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    ObjectiveCompletion = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    PlayerCount = table.Column<int>(type: "integer", nullable: false),
                    PayloadHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RewardStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.MatchId);
                    table.CheckConstraint("CK_MatchResults_DurationSeconds_Range", "\"DurationSeconds\" >= 60 AND \"DurationSeconds\" <= 900");
                    table.CheckConstraint("CK_MatchResults_Outcome_Allowed", "\"Outcome\" IN ('WIN', 'LOSE')");
                    table.CheckConstraint("CK_MatchResults_ObjectiveCompletion_Range", "CAST(\"ObjectiveCompletion\" AS NUMERIC) >= 0 AND CAST(\"ObjectiveCompletion\" AS NUMERIC) <= 1");
                    table.CheckConstraint("CK_MatchResults_PlayerCount_Range", "\"PlayerCount\" >= 1 AND \"PlayerCount\" <= 4");
                    table.CheckConstraint("CK_MatchResults_RewardStatus_Pending", "\"RewardStatus\" = 'Pending'");
                    table.CheckConstraint("CK_MatchResults_Timestamp_Order", "\"EndedAtUtc\" >= \"StartedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_MatchResults_MatchAuthorityBindings_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchAuthorityBindings",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchResults_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchResultPlayers",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Survived = table.Column<bool>(type: "boolean", nullable: false),
                    Disconnected = table.Column<bool>(type: "boolean", nullable: false),
                    DetectionCount = table.Column<int>(type: "integer", nullable: false),
                    DownedCount = table.Column<int>(type: "integer", nullable: false),
                    ReviveCount = table.Column<int>(type: "integer", nullable: false),
                    ObjectiveContribution = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResultPlayers", x => new { x.MatchId, x.UserId });
                    table.CheckConstraint("CK_MatchResultPlayers_DetectionCount_NonNegative", "\"DetectionCount\" >= 0");
                    table.CheckConstraint("CK_MatchResultPlayers_DownedCount_NonNegative", "\"DownedCount\" >= 0");
                    table.CheckConstraint("CK_MatchResultPlayers_ObjectiveContribution_NonNegative", "\"ObjectiveContribution\" >= 0");
                    table.CheckConstraint("CK_MatchResultPlayers_ReviveCount_NonNegative", "\"ReviveCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_MatchResultPlayers_MatchPlayerBindings_MatchId_UserId",
                        columns: x => new { x.MatchId, x.UserId },
                        principalTable: "MatchPlayerBindings",
                        principalColumns: new[] { "MatchId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchResultPlayers_MatchResults_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchResults",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_SubmittedAtUtc",
                table: "MatchResults",
                column: "SubmittedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_SubmittedByUserId",
                table: "MatchResults",
                column: "SubmittedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchResultPlayers");

            migrationBuilder.DropTable(
                name: "MatchResults");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MatchPlayerBindings_MatchId_UserId",
                table: "MatchPlayerBindings");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "MatchAuthorityBindings");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerBindings_MatchId_UserId",
                table: "MatchPlayerBindings",
                columns: new[] { "MatchId", "UserId" },
                unique: true);
        }
    }
}
