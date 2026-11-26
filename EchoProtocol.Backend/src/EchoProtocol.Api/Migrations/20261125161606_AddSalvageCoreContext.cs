using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSalvageCoreContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchAuthorityBindings_LeaderboardContext",
                table: "MatchAuthorityBindings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchAuthorityBindings_LeaderboardContext",
                table: "MatchAuthorityBindings",
                sql: "(\"MapId\" IS NULL AND \"Difficulty\" IS NULL) OR (\"MapId\" IS NOT NULL AND \"Difficulty\" IS NOT NULL AND \"MapId\" IN ('SciFi', 'Scene_A', 'Scene_B', 'Salvage_SciFi') AND \"Difficulty\" IN ('EASY', 'NORMAL', 'HARD'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchAuthorityBindings_LeaderboardContext",
                table: "MatchAuthorityBindings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchAuthorityBindings_LeaderboardContext",
                table: "MatchAuthorityBindings",
                sql: "(\"MapId\" IS NULL AND \"Difficulty\" IS NULL) OR (\"MapId\" IS NOT NULL AND \"Difficulty\" IS NOT NULL AND \"MapId\" IN ('SciFi', 'Scene_A', 'Scene_B') AND \"Difficulty\" IN ('EASY', 'NORMAL', 'HARD'))");
        }
    }
}
