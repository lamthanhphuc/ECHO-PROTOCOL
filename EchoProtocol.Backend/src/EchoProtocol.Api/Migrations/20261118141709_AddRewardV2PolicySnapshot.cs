using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardV2PolicySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LossCapCreditsPerMinute",
                table: "RewardConfigurations",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: false,
                defaultValue: 2m);

            migrationBuilder.AddColumn<string>(
                name: "PolicyKey",
                table: "RewardConfigurations",
                type: "text",
                nullable: false,
                defaultValue: "REWARD_V1");

            migrationBuilder.AddColumn<int>(
                name: "EvidenceVersion",
                table: "MatchResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EvidenceComplete",
                table: "MatchResultPlayers",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ObjectiveActions",
                table: "MatchResultPlayers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RescueActions",
                table: "MatchResultPlayers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressionPolicyVersion",
                table: "MatchAuthorityBindings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardConfigurationJson",
                table: "MatchAuthorityBindings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardPolicyVersion",
                table: "MatchAuthorityBindings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchResultPlayers_ObjectiveActions_Range",
                table: "MatchResultPlayers",
                sql: "\"ObjectiveActions\" IS NULL OR \"ObjectiveActions\" BETWEEN 0 AND 10000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchResultPlayers_RescueActions_Range",
                table: "MatchResultPlayers",
                sql: "\"RescueActions\" IS NULL OR \"RescueActions\" BETWEEN 0 AND 10000");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchResultPlayers_ObjectiveActions_Range",
                table: "MatchResultPlayers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchResultPlayers_RescueActions_Range",
                table: "MatchResultPlayers");

            migrationBuilder.DropColumn(
                name: "LossCapCreditsPerMinute",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "PolicyKey",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "EvidenceVersion",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "EvidenceComplete",
                table: "MatchResultPlayers");

            migrationBuilder.DropColumn(
                name: "ObjectiveActions",
                table: "MatchResultPlayers");

            migrationBuilder.DropColumn(
                name: "RescueActions",
                table: "MatchResultPlayers");

            migrationBuilder.DropColumn(
                name: "ProgressionPolicyVersion",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "RewardConfigurationJson",
                table: "MatchAuthorityBindings");

            migrationBuilder.DropColumn(
                name: "RewardPolicyVersion",
                table: "MatchAuthorityBindings");
        }
    }
}
