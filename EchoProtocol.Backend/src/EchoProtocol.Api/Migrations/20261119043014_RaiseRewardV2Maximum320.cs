using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class RaiseRewardV2Maximum320 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "RewardConfigurations"
                SET "Maximum" = 320
                WHERE "Id" = 'c7e0137e-2484-4c88-9d7b-2161c6e15cef'
                  AND "PolicyKey" = 'REWARD_V2' AND "Maximum" = 300;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
