using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowHostMigrationActorReuse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchPlayerBindings_MatchId_FusionActorNumber",
                table: "MatchPlayerBindings");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerBindings_MatchId_FusionActorNumber",
                table: "MatchPlayerBindings",
                columns: new[] { "MatchId", "FusionActorNumber" },
                unique: true,
                filter: "\"DisconnectedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchPlayerBindings_MatchId_FusionActorNumber",
                table: "MatchPlayerBindings");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerBindings_MatchId_FusionActorNumber",
                table: "MatchPlayerBindings",
                columns: new[] { "MatchId", "FusionActorNumber" },
                unique: true);
        }
    }
}
