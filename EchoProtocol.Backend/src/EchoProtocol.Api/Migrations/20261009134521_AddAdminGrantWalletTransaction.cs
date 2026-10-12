using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminGrantWalletTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletTransactions_Amount_ByType",
                table: "WalletTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletTransactions_Amount_ByType",
                table: "WalletTransactions",
                sql: "(\"Type\" IN ('MATCH_REWARD', 'PAYMENT_FULFILLMENT', 'ADMIN_GRANT') AND \"Amount\" >= 0) OR (\"Type\" = 'PURCHASE' AND \"Amount\" <= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletTransactions_Amount_ByType",
                table: "WalletTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletTransactions_Amount_ByType",
                table: "WalletTransactions",
                sql: "(\"Type\" IN ('MATCH_REWARD', 'PAYMENT_FULFILLMENT') AND \"Amount\" >= 0) OR (\"Type\" = 'PURCHASE' AND \"Amount\" <= 0)");
        }
    }
}
