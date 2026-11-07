using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoteFixedProductionPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings",
                sql: "COALESCE((\r\n    \"ResolutionMode\" IS NULL\r\n    AND \"BalanceSource\" IS NULL\r\n    AND \"BalanceProtocolVersion\" IS NULL\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" = 'STANDARD'\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'LOCAL'\r\n    AND \"ResolutionMode\" IN ('FIXED', 'ADAPTIVE')\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" = 'STANDARD'\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'REMOTE_FIXED'\r\n    AND \"ResolutionMode\" = 'FIXED'\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NOT NULL\r\n    AND \"BalanceHash\" IS NOT NULL\r\n    AND \"MatchDataPolicy\" IN ('TEST_ONLY', 'STANDARD')\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n    AND \"MapId\" IN ('SciFi', 'Scene_A')\r\n    AND \"Difficulty\" IN ('EASY', 'NORMAL', 'HARD')\r\n), FALSE)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings",
                sql: "COALESCE((\r\n    \"ResolutionMode\" IS NULL\r\n    AND \"BalanceSource\" IS NULL\r\n    AND \"BalanceProtocolVersion\" IS NULL\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" = 'STANDARD'\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'LOCAL'\r\n    AND \"ResolutionMode\" IN ('FIXED', 'ADAPTIVE')\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" = 'STANDARD'\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'REMOTE_FIXED'\r\n    AND \"ResolutionMode\" = 'FIXED'\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NOT NULL\r\n    AND \"BalanceHash\" IS NOT NULL\r\n    AND \"MatchDataPolicy\" = 'TEST_ONLY'\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n    AND \"MapId\" IN ('SciFi', 'Scene_A')\r\n    AND \"Difficulty\" IN ('EASY', 'NORMAL', 'HARD')\r\n), FALSE)");
        }
    }
}
