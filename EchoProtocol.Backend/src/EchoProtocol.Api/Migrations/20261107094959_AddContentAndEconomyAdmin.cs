using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentAndEconomyAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings");

            migrationBuilder.CreateTable(
                name: "ShopPriceProposal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousPrice = table.Column<int>(type: "integer", nullable: false),
                    ProposedPrice = table.Column<int>(type: "integer", nullable: false),
                    ItemUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProposedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopPriceProposal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopPriceProposal_ShopItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ShopItems",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchAuthorityBindings_BalancePolicy",
                table: "MatchAuthorityBindings",
                sql: "COALESCE((\r\n    \"ResolutionMode\" IS NULL\r\n    AND \"BalanceSource\" IS NULL\r\n    AND \"BalanceProtocolVersion\" IS NULL\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" = 'STANDARD'\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'LOCAL'\r\n    AND \"ResolutionMode\" IN ('FIXED', 'ADAPTIVE')\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NULL\r\n    AND \"BalanceHash\" IS NULL\r\n    AND \"MatchDataPolicy\" = 'STANDARD'\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n)\r\nOR\r\n(\r\n    \"BalanceSource\" = 'REMOTE_FIXED'\r\n    AND \"ResolutionMode\" = 'FIXED'\r\n    AND \"BalanceProtocolVersion\" = 'RFB-1'\r\n    AND \"BalanceVersion\" IS NOT NULL\r\n    AND \"BalanceHash\" IS NOT NULL\r\n    AND \"MatchDataPolicy\" = 'TEST_ONLY'\r\n    AND \"MapId\" IS NOT NULL\r\n    AND \"Difficulty\" IS NOT NULL\r\n    AND \"MapId\" IN ('SciFi', 'Scene_A', 'Scene_B')\r\n    AND \"Difficulty\" IN ('EASY', 'NORMAL', 'HARD')\r\n), FALSE)");

            migrationBuilder.CreateIndex(
                name: "IX_ShopPriceProposal_ItemId",
                table: "ShopPriceProposal",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopPriceProposal_Status_ProposedAtUtc",
                table: "ShopPriceProposal",
                columns: new[] { "Status", "ProposedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopPriceProposal");

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
