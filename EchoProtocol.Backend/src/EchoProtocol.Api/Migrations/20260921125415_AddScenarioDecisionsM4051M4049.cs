using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioDecisionsM4051M4049 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScenarioConfigs_ScenarioConfigVersion",
                table: "ScenarioConfigs");

            migrationBuilder.CreateTable(
                name: "AdaptiveInputSnapshots",
                columns: table => new
                {
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionPoint = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SnapshotContentFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RosterIdentity = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    TeamSize = table.Column<int>(type: "integer", nullable: false),
                    Validity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReasonCodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProfileFormulaSemanticId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SurvivalComparisonKey = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    NoiseComparisonKey = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    SurvivalAggregationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NoiseAggregationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SurvivalMeanObservedScore = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    NoiseMeanObservedScore = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    SurvivalObservedActiveCount = table.Column<int>(type: "integer", nullable: false),
                    NoiseObservedActiveCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdaptiveInputSnapshots", x => x.SnapshotId);
                    table.CheckConstraint("CK_AdaptiveSnapshots_Means_Range", "(\"SurvivalMeanObservedScore\" IS NULL OR (CAST(\"SurvivalMeanObservedScore\" AS NUMERIC) >= 0 AND CAST(\"SurvivalMeanObservedScore\" AS NUMERIC) <= 100)) AND (\"NoiseMeanObservedScore\" IS NULL OR (CAST(\"NoiseMeanObservedScore\" AS NUMERIC) >= 0 AND CAST(\"NoiseMeanObservedScore\" AS NUMERIC) <= 100))");
                    table.CheckConstraint("CK_AdaptiveSnapshots_TeamSize_Positive", "\"TeamSize\" >= 1 AND \"TeamSize\" <= 4");
                    table.ForeignKey(
                        name: "FK_AdaptiveInputSnapshots_MatchAuthorityBindings_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchAuthorityBindings",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdaptiveInputSnapshotPlayers",
                columns: table => new
                {
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    ProfileLineageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfileRevision = table.Column<long>(type: "bigint", nullable: true),
                    ProfileFormulaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MatchScoreFormulaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    NormalizationConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ProfileNoiseFilterVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AlphaConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SurvivalScore = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    SurvivalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SurvivalSampleCount = table.Column<int>(type: "integer", nullable: true),
                    SurvivalComparisonKey = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    NoiseScore = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    NoiseStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NoiseSampleCount = table.Column<int>(type: "integer", nullable: true),
                    NoiseComparisonKey = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    DeferredDimensionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdaptiveInputSnapshotPlayers", x => new { x.SnapshotId, x.UserId });
                    table.CheckConstraint("CK_AdaptiveSnapshotPlayers_DeferredCanonical", "\"DeferredDimensionsJson\" IS NOT NULL");
                    table.CheckConstraint("CK_AdaptiveSnapshotPlayers_Scores_Range", "(\"SurvivalScore\" IS NULL OR (CAST(\"SurvivalScore\" AS NUMERIC) >= 0 AND CAST(\"SurvivalScore\" AS NUMERIC) <= 100)) AND (\"NoiseScore\" IS NULL OR (CAST(\"NoiseScore\" AS NUMERIC) >= 0 AND CAST(\"NoiseScore\" AS NUMERIC) <= 100))");
                    table.ForeignKey(
                        name: "FK_AdaptiveInputSnapshotPlayers_AdaptiveInputSnapshots_Snapsho~",
                        column: x => x.SnapshotId,
                        principalTable: "AdaptiveInputSnapshots",
                        principalColumn: "SnapshotId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdaptiveInputSnapshotPlayers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioDecisions",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolutionMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecisionPoint = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExperimentCondition = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    DecisionSemanticFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScenarioConfigId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ScenarioConfigFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PolicyConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EvidencePolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ParameterRegistryVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ContentWhitelistVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FallbackConfigId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FallbackConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UsedFixedFallback = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CandidateValidationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ResolutionResult = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CommittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioDecisions", x => x.DecisionId);
                    table.ForeignKey(
                        name: "FK_ScenarioDecisions_AdaptiveInputSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "AdaptiveInputSnapshots",
                        principalColumn: "SnapshotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioDecisions_MatchAuthorityBindings_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchAuthorityBindings",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioDecisions_ScenarioConfigs_ScenarioConfigId_Scenario~",
                        columns: x => new { x.ScenarioConfigId, x.ScenarioConfigVersion },
                        principalTable: "ScenarioConfigs",
                        principalColumns: new[] { "ScenarioConfigId", "ScenarioConfigVersion" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioDecisions_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioApplyReceipts",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedScenarioConfigId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AppliedScenarioConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AppliedScenarioConfigFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioApplyReceipts", x => x.DecisionId);
                    table.ForeignKey(
                        name: "FK_ScenarioApplyReceipts_ScenarioDecisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "ScenarioDecisions",
                        principalColumn: "DecisionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioApplyReceipts_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioConfigs_ScenarioConfigVersion",
                table: "ScenarioConfigs",
                column: "ScenarioConfigVersion");

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveInputSnapshotPlayers_UserId",
                table: "AdaptiveInputSnapshotPlayers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveInputSnapshots_MatchId_CreatedAtUtc",
                table: "AdaptiveInputSnapshots",
                columns: new[] { "MatchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioApplyReceipts_MatchId",
                table: "ScenarioApplyReceipts",
                column: "MatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioApplyReceipts_ReportedByUserId",
                table: "ScenarioApplyReceipts",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioDecisions_HostUserId",
                table: "ScenarioDecisions",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioDecisions_ScenarioConfigId_ScenarioConfigVersion",
                table: "ScenarioDecisions",
                columns: new[] { "ScenarioConfigId", "ScenarioConfigVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioDecisions_SnapshotId",
                table: "ScenarioDecisions",
                column: "SnapshotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ScenarioDecisions_CurrentMatch",
                table: "ScenarioDecisions",
                column: "MatchId",
                unique: true,
                filter: "\"IsCurrent\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdaptiveInputSnapshotPlayers");

            migrationBuilder.DropTable(
                name: "ScenarioApplyReceipts");

            migrationBuilder.DropTable(
                name: "ScenarioDecisions");

            migrationBuilder.DropTable(
                name: "AdaptiveInputSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ScenarioConfigs_ScenarioConfigVersion",
                table: "ScenarioConfigs");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioConfigs_ScenarioConfigVersion",
                table: "ScenarioConfigs",
                column: "ScenarioConfigVersion",
                unique: true);
        }
    }
}
