using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableRewardV2Rules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContributionExperience",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 25);

            migrationBuilder.AddColumn<int>(
                name: "FullTeamPercent",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "LossExperiencePerMinute",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ObjectiveActionCap",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "ObjectiveExperience",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "OneActionTeamPercent",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "RescueActionCap",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "SurvivalExperience",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 25);

            migrationBuilder.AddColumn<int>(
                name: "TwoActionTeamPercent",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 75);

            migrationBuilder.AddColumn<int>(
                name: "WinExperience",
                table: "RewardConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 75);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContributionExperience",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "FullTeamPercent",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "LossExperiencePerMinute",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "ObjectiveActionCap",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "ObjectiveExperience",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "OneActionTeamPercent",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "RescueActionCap",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "SurvivalExperience",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "TwoActionTeamPercent",
                table: "RewardConfigurations");

            migrationBuilder.DropColumn(
                name: "WinExperience",
                table: "RewardConfigurations");
        }
    }
}
