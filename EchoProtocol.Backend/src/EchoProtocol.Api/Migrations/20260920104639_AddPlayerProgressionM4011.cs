using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerProgressionM4011 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ExperiencePoints",
                table: "PlayerProfiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "PlayerProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "ExperiencePointsAwarded",
                table: "MatchRewardGrants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ProgressionPolicyVersion",
                table: "MatchRewardGrants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "LEGACY_UNVERSIONED");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerProfiles_ExperiencePoints_NonNegative",
                table: "PlayerProfiles",
                sql: "\"ExperiencePoints\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerProfiles_Level_Positive",
                table: "PlayerProfiles",
                sql: "\"Level\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchRewardGrants_ExperiencePointsAwarded_NonNegative",
                table: "MatchRewardGrants",
                sql: "\"ExperiencePointsAwarded\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerProfiles_ExperiencePoints_NonNegative",
                table: "PlayerProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerProfiles_Level_Positive",
                table: "PlayerProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchRewardGrants_ExperiencePointsAwarded_NonNegative",
                table: "MatchRewardGrants");

            migrationBuilder.DropColumn(
                name: "ExperiencePoints",
                table: "PlayerProfiles");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "PlayerProfiles");

            migrationBuilder.DropColumn(
                name: "ExperiencePointsAwarded",
                table: "MatchRewardGrants");

            migrationBuilder.DropColumn(
                name: "ProgressionPolicyVersion",
                table: "MatchRewardGrants");
        }
    }
}
