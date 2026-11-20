using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class DefaultRewardV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep custom V1 settings and unresolved legacy rewards intact.
            migrationBuilder.Sql("""
                UPDATE "RewardConfigurations"
                SET "PolicyKey" = 'REWARD_V2', "Participation" = 0, "Objective" = 70,
                    "Win" = 150, "Survival" = 10, "Contribution" = 70,
                    "Disconnected" = 0, "Maximum" = 300
                WHERE "Id" = 'c7e0137e-2484-4c88-9d7b-2161c6e15cef'
                  AND "PolicyKey" = 'REWARD_V1'
                  AND "Participation" = 20 AND "Objective" = 30 AND "Win" = 50
                  AND "Survival" = 20 AND "Contribution" = 10
                  AND "Disconnected" = 10 AND "Maximum" = 130
                  AND NOT EXISTS (SELECT 1 FROM "MatchAuthorityBindings"
                      WHERE "RewardPolicyVersion" IS NULL AND "Status" = 'InMatch')
                  AND NOT EXISTS (SELECT 1 FROM "MatchResults" r
                      JOIN "MatchAuthorityBindings" m ON m."MatchId" = r."MatchId"
                      WHERE m."RewardPolicyVersion" IS NULL AND r."RewardStatus" = 'Pending');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PolicyKey",
                table: "RewardConfigurations",
                type: "text",
                nullable: false,
                defaultValue: "REWARD_V2",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "REWARD_V1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PolicyKey",
                table: "RewardConfigurations",
                type: "text",
                nullable: false,
                defaultValue: "REWARD_V1",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "REWARD_V2");
        }
    }
}
