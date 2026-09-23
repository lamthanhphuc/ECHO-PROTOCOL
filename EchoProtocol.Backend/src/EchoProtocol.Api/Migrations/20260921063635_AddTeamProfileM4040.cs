using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamProfileM4040 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamProfiles",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessingRevision = table.Column<long>(type: "bigint", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProcessingReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TelemetryCompleteness = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ObjectiveTimeSeconds = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: true),
                    ObjectiveTimeStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SplitTime = table.Column<decimal>(type: "numeric", nullable: true),
                    SplitTimeStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AvgDistance = table.Column<decimal>(type: "numeric", nullable: true),
                    AvgDistanceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviveSuccess = table.Column<decimal>(type: "numeric", nullable: true),
                    ReviveSuccessStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResourceEfficiency = table.Column<decimal>(type: "numeric", nullable: true),
                    ResourceEfficiencyStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Communication = table.Column<decimal>(type: "numeric", nullable: true),
                    CommunicationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WipeRecovery = table.Column<decimal>(type: "numeric", nullable: true),
                    WipeRecoveryStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ObjectiveSpeedScore = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ObjectiveSpeedStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SurvivalScore = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    SurvivalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TeamworkScore = table.Column<decimal>(type: "numeric", nullable: true),
                    TeamworkStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResourceEfficiencyScore = table.Column<decimal>(type: "numeric", nullable: true),
                    ResourceEfficiencyScoreStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TeamPerformanceScore = table.Column<decimal>(type: "numeric", nullable: true),
                    TeamPerformanceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProfileFormulaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TeamPerformanceFormulaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PhaseRegistryVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    NormalizationConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SourceTelemetrySchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ProjectionFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamProfiles", x => x.MatchId);
                    table.CheckConstraint("CK_TeamProfiles_Deferred_Null", "\"SplitTime\" IS NULL AND \"SplitTimeStatus\" = 'Deferred' AND \"AvgDistance\" IS NULL AND \"AvgDistanceStatus\" = 'Deferred' AND \"ReviveSuccess\" IS NULL AND \"ReviveSuccessStatus\" = 'Deferred' AND \"ResourceEfficiency\" IS NULL AND \"ResourceEfficiencyStatus\" = 'Deferred' AND \"Communication\" IS NULL AND \"CommunicationStatus\" = 'Deferred' AND \"WipeRecovery\" IS NULL AND \"WipeRecoveryStatus\" = 'Deferred' AND \"TeamworkScore\" IS NULL AND \"TeamworkStatus\" = 'Deferred' AND \"ResourceEfficiencyScore\" IS NULL AND \"ResourceEfficiencyScoreStatus\" = 'Deferred'");
                    table.CheckConstraint("CK_TeamProfiles_ObjectiveSpeed_StatusValue", "(\"ObjectiveSpeedStatus\" = 'Available' AND \"ObjectiveSpeedScore\" IS NOT NULL) OR (\"ObjectiveSpeedStatus\" <> 'Available' AND \"ObjectiveSpeedScore\" IS NULL)");
                    table.CheckConstraint("CK_TeamProfiles_ObjectiveTime_NonNegative", "\"ObjectiveTimeSeconds\" IS NULL OR CAST(\"ObjectiveTimeSeconds\" AS NUMERIC) >= 0");
                    table.CheckConstraint("CK_TeamProfiles_ObjectiveTime_StatusValue", "(\"ObjectiveTimeStatus\" = 'Available' AND \"ObjectiveTimeSeconds\" IS NOT NULL) OR (\"ObjectiveTimeStatus\" <> 'Available' AND \"ObjectiveTimeSeconds\" IS NULL)");
                    table.CheckConstraint("CK_TeamProfiles_Performance_Incomplete", "\"TeamPerformanceStatus\" = 'Incomplete' AND \"TeamPerformanceScore\" IS NULL");
                    table.CheckConstraint("CK_TeamProfiles_Revision_NonNegative", "\"ProcessingRevision\" >= 0");
                    table.CheckConstraint("CK_TeamProfiles_Scores_Range", "(\"ObjectiveSpeedScore\" IS NULL OR (CAST(\"ObjectiveSpeedScore\" AS NUMERIC) >= 0 AND CAST(\"ObjectiveSpeedScore\" AS NUMERIC) <= 100)) AND (\"SurvivalScore\" IS NULL OR (CAST(\"SurvivalScore\" AS NUMERIC) >= 0 AND CAST(\"SurvivalScore\" AS NUMERIC) <= 100))");
                    table.CheckConstraint("CK_TeamProfiles_Survival_StatusValue", "(\"SurvivalStatus\" = 'Available' AND \"SurvivalScore\" IS NOT NULL) OR (\"SurvivalStatus\" <> 'Available' AND \"SurvivalScore\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_TeamProfiles_MatchResults_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchResults",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamProfiles_ProcessingStatus",
                table: "TeamProfiles",
                column: "ProcessingStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamProfiles");
        }
    }
}
