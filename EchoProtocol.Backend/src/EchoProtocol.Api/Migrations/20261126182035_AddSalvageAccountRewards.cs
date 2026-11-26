using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSalvageAccountRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SalvageCompletedDays",
                table: "MatchResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalvageCompletedLevels",
                table: "MatchResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SalvageUsedDebugCheats",
                table: "MatchResults",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SalvageCompletedDays",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "SalvageCompletedLevels",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "SalvageUsedDebugCheats",
                table: "MatchResults");
        }
    }
}
