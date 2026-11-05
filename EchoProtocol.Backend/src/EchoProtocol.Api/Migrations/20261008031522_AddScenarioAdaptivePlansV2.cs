using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioAdaptivePlansV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScenarioAdaptivePlansV2",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhaseOrdinal = table.Column<int>(type: "integer", nullable: false),
                    DecisionPoint = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BaselineVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreviousPlanFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultingPlanFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangedKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreviousValue = table.Column<double>(type: "double precision", nullable: false),
                    AppliedValue = table.Column<double>(type: "double precision", nullable: false),
                    AdaptationIntent = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RosterIdentity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlanValuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CommitStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApplyStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CommittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioAdaptivePlansV2", x => x.DecisionId);
                    table.ForeignKey(
                        name: "FK_ScenarioAdaptivePlansV2_AdaptiveInputSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "AdaptiveInputSnapshots",
                        principalColumn: "SnapshotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioAdaptivePlansV2_MatchAuthorityBindings_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchAuthorityBindings",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioAdaptivePlansV2_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioAdaptivePlansV2_HostUserId",
                table: "ScenarioAdaptivePlansV2",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioAdaptivePlansV2_MatchId_PhaseOrdinal",
                table: "ScenarioAdaptivePlansV2",
                columns: new[] { "MatchId", "PhaseOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioAdaptivePlansV2_SnapshotId",
                table: "ScenarioAdaptivePlansV2",
                column: "SnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScenarioAdaptivePlansV2");
        }
    }
}
