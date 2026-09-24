using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerAIProfileMatchScoreM4027M4038M4039 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerAIProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileLineageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileRevision = table.Column<long>(type: "bigint", nullable: false),
                    ProfileFormulaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MatchScoreFormulaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizationConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ProfileNoiseFilterVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AlphaConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SurvivalScore = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false, defaultValue: 50m),
                    SurvivalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SurvivalSampleCount = table.Column<int>(type: "integer", nullable: false),
                    SurvivalLastMatchEndTs = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SurvivalLastMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    SurvivalLastUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NoiseScore = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false, defaultValue: 50m),
                    NoiseStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NoiseSampleCount = table.Column<int>(type: "integer", nullable: false),
                    NoiseLastMatchEndTs = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NoiseLastMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    NoiseLastUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObjectiveScore = table.Column<decimal>(type: "numeric", nullable: true),
                    TeamworkScore = table.Column<decimal>(type: "numeric", nullable: true),
                    ExplorationScore = table.Column<decimal>(type: "numeric", nullable: true),
                    NavigationScore = table.Column<decimal>(type: "numeric", nullable: true),
                    ToolUsageScore = table.Column<decimal>(type: "numeric", nullable: true),
                    RiskScore = table.Column<decimal>(type: "numeric", nullable: true),
                    ReviveScore = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAIProfiles", x => x.UserId);
                    table.UniqueConstraint("AK_PlayerAIProfiles_ProfileLineageId", x => x.ProfileLineageId);
                    table.CheckConstraint("CK_PlayerAIProfiles_Deferred_Null", "\"ObjectiveScore\" IS NULL AND \"TeamworkScore\" IS NULL AND \"ExplorationScore\" IS NULL AND \"NavigationScore\" IS NULL AND \"ToolUsageScore\" IS NULL AND \"RiskScore\" IS NULL AND \"ReviveScore\" IS NULL");
                    table.CheckConstraint("CK_PlayerAIProfiles_NoiseScore_Range", "CAST(\"NoiseScore\" AS NUMERIC) >= 0 AND CAST(\"NoiseScore\" AS NUMERIC) <= 100");
                    table.CheckConstraint("CK_PlayerAIProfiles_Revision_NonNegative", "\"ProfileRevision\" >= 0");
                    table.CheckConstraint("CK_PlayerAIProfiles_SampleCounts_NonNegative", "\"SurvivalSampleCount\" >= 0 AND \"NoiseSampleCount\" >= 0");
                    table.CheckConstraint("CK_PlayerAIProfiles_SurvivalScore_Range", "CAST(\"SurvivalScore\" AS NUMERIC) >= 0 AND CAST(\"SurvivalScore\" AS NUMERIC) <= 100");
                    table.ForeignKey(
                        name: "FK_PlayerAIProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileLineageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dimension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Score = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    MatchEndTs = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MatchScoreFormulaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizationConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProfileNoiseFilterVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AlphaConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceTelemetrySchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceEvidenceFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SemanticFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ContributionStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RetractionReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetractedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchScores", x => x.Id);
                    table.CheckConstraint("CK_MatchScores_Score_Range", "CAST(\"Score\" AS NUMERIC) >= 0 AND CAST(\"Score\" AS NUMERIC) <= 100");
                    table.ForeignKey(
                        name: "FK_MatchScores_MatchResultPlayers_MatchId_UserId",
                        columns: x => new { x.MatchId, x.UserId },
                        principalTable: "MatchResultPlayers",
                        principalColumns: new[] { "MatchId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchScores_PlayerAIProfiles_ProfileLineageId",
                        column: x => x.ProfileLineageId,
                        principalTable: "PlayerAIProfiles",
                        principalColumn: "ProfileLineageId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchScores_MatchId_UserId",
                table: "MatchScores",
                columns: new[] { "MatchId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchScores_ProfileLineageId",
                table: "MatchScores",
                column: "ProfileLineageId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchScores_ReplayOrder",
                table: "MatchScores",
                columns: new[] { "UserId", "ProfileLineageId", "Dimension", "MatchEndTs", "MatchId" });

            migrationBuilder.CreateIndex(
                name: "UX_MatchScores_ApplyKey",
                table: "MatchScores",
                columns: new[] { "UserId", "MatchId", "ProfileLineageId", "Dimension" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchScores");

            migrationBuilder.DropTable(
                name: "PlayerAIProfiles");
        }
    }
}
