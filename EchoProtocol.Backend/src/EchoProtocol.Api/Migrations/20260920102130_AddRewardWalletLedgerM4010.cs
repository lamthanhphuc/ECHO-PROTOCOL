using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardWalletLedgerM4010 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchResults_RewardStatus_Pending",
                table: "MatchResults");

            migrationBuilder.CreateTable(
                name: "MatchRewardGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyAmount = table.Column<int>(type: "integer", nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchRewardGrants", x => x.Id);
                    table.CheckConstraint("CK_MatchRewardGrants_CurrencyAmount_NonNegative", "\"CurrencyAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_MatchRewardGrants_MatchResultPlayers_MatchId_UserId",
                        columns: x => new { x.MatchId, x.UserId },
                        principalTable: "MatchResultPlayers",
                        principalColumns: new[] { "MatchId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchRewardGrants_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    BalanceBefore = table.Column<int>(type: "integer", nullable: false),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.Id);
                    table.CheckConstraint("CK_WalletTransactions_Amount_NonNegative", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_WalletTransactions_BalanceEquation", "\"BalanceAfter\" = \"BalanceBefore\" + \"Amount\"");
                    table.CheckConstraint("CK_WalletTransactions_Balances_NonNegative", "\"BalanceBefore\" >= 0 AND \"BalanceAfter\" >= 0");
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchResults_RewardStatus_Allowed",
                table: "MatchResults",
                sql: "\"RewardStatus\" IN ('Pending', 'Completed')");

            migrationBuilder.CreateIndex(
                name: "IX_MatchRewardGrants_MatchId_UserId",
                table: "MatchRewardGrants",
                columns: new[] { "MatchId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchRewardGrants_WalletId",
                table: "MatchRewardGrants",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ReferenceId",
                table: "WalletTransactions",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId_Type_ReferenceId",
                table: "WalletTransactions",
                columns: new[] { "WalletId", "Type", "ReferenceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchRewardGrants");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchResults_RewardStatus_Allowed",
                table: "MatchResults");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchResults_RewardStatus_Pending",
                table: "MatchResults",
                sql: "\"RewardStatus\" = 'Pending'");
        }
    }
}
