using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExpandedPlayerAIDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerAIProfiles_Deferred_Null",
                table: "PlayerAIProfiles");

            migrationBuilder.AlterColumn<decimal>(
                name: "ToolUsageScore",
                table: "PlayerAIProfiles",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ObjectiveScore",
                table: "PlayerAIProfiles",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ObjectiveSampleCount",
                table: "PlayerAIProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ObjectiveStatus",
                table: "PlayerAIProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ColdStart");

            migrationBuilder.AddColumn<string>(
                name: "ToolUsageNormalizationVersion",
                table: "PlayerAIProfiles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToolUsageSampleCount",
                table: "PlayerAIProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ToolUsageStatus",
                table: "PlayerAIProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ColdStart");

            migrationBuilder.AddColumn<string>(
                name: "FingerprintVersion",
                table: "AdaptiveInputSnapshots",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "V1");

            migrationBuilder.AddColumn<string>(
                name: "ObjectiveAggregationStatus",
                table: "AdaptiveInputSnapshots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "UNAVAILABLE");

            migrationBuilder.AddColumn<string>(
                name: "ObjectiveComparisonKey",
                table: "AdaptiveInputSnapshots",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ObjectiveMeanObservedScore",
                table: "AdaptiveInputSnapshots",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ObjectiveObservedActiveCount",
                table: "AdaptiveInputSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ToolUsageAggregationStatus",
                table: "AdaptiveInputSnapshots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "UNAVAILABLE");

            migrationBuilder.AddColumn<string>(
                name: "ToolUsageComparisonKey",
                table: "AdaptiveInputSnapshots",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ToolUsageMeanObservedScore",
                table: "AdaptiveInputSnapshots",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToolUsageObservedActiveCount",
                table: "AdaptiveInputSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerAIProfiles_Deferred_Null",
                table: "PlayerAIProfiles",
                sql: "\"TeamworkScore\" IS NULL AND \"ExplorationScore\" IS NULL AND \"NavigationScore\" IS NULL AND \"RiskScore\" IS NULL AND \"ReviveScore\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerAIProfiles_ExpandedSamples_NonNegative",
                table: "PlayerAIProfiles",
                sql: "\"ObjectiveSampleCount\" >= 0 AND \"ToolUsageSampleCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerAIProfiles_ExpandedScores_Range",
                table: "PlayerAIProfiles",
                sql: "(\"ObjectiveScore\" IS NULL OR (CAST(\"ObjectiveScore\" AS NUMERIC) >= 0 AND CAST(\"ObjectiveScore\" AS NUMERIC) <= 100)) AND (\"ToolUsageScore\" IS NULL OR (CAST(\"ToolUsageScore\" AS NUMERIC) >= 0 AND CAST(\"ToolUsageScore\" AS NUMERIC) <= 100))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerAIProfiles_Deferred_Null",
                table: "PlayerAIProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerAIProfiles_ExpandedSamples_NonNegative",
                table: "PlayerAIProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerAIProfiles_ExpandedScores_Range",
                table: "PlayerAIProfiles");

            migrationBuilder.DropColumn(
                name: "ObjectiveSampleCount",
                table: "PlayerAIProfiles");

            migrationBuilder.DropColumn(
                name: "ObjectiveStatus",
                table: "PlayerAIProfiles");

            migrationBuilder.DropColumn(
                name: "ToolUsageNormalizationVersion",
                table: "PlayerAIProfiles");

            migrationBuilder.DropColumn(
                name: "ToolUsageSampleCount",
                table: "PlayerAIProfiles");

            migrationBuilder.DropColumn(
                name: "ToolUsageStatus",
                table: "PlayerAIProfiles");

            migrationBuilder.DropColumn(
                name: "FingerprintVersion",
                table: "AdaptiveInputSnapshots");

            migrationBuilder.DropColumn(
                name: "ObjectiveAggregationStatus",
                table: "AdaptiveInputSnapshots");

            migrationBuilder.DropColumn(
                name: "ObjectiveComparisonKey",
                table: "AdaptiveInputSnapshots");

            migrationBuilder.DropColumn(
                name: "ObjectiveMeanObservedScore",
                table: "AdaptiveInputSnapshots");

            migrationBuilder.DropColumn(
                name: "ObjectiveObservedActiveCount",
                table: "AdaptiveInputSnapshots");

            migrationBuilder.DropColumn(
                name: "ToolUsageAggregationStatus",
                table: "AdaptiveInputSnapshots");

            migrationBuilder.DropColumn(
                name: "ToolUsageComparisonKey",
                table: "AdaptiveInputSnapshots");

            migrationBuilder.DropColumn(
                name: "ToolUsageMeanObservedScore",
                table: "AdaptiveInputSnapshots");

            migrationBuilder.DropColumn(
                name: "ToolUsageObservedActiveCount",
                table: "AdaptiveInputSnapshots");

            migrationBuilder.AlterColumn<decimal>(
                name: "ToolUsageScore",
                table: "PlayerAIProfiles",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)",
                oldPrecision: 9,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ObjectiveScore",
                table: "PlayerAIProfiles",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)",
                oldPrecision: 9,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerAIProfiles_Deferred_Null",
                table: "PlayerAIProfiles",
                sql: "\"ObjectiveScore\" IS NULL AND \"TeamworkScore\" IS NULL AND \"ExplorationScore\" IS NULL AND \"NavigationScore\" IS NULL AND \"ToolUsageScore\" IS NULL AND \"RiskScore\" IS NULL AND \"ReviveScore\" IS NULL");
        }
    }
}
