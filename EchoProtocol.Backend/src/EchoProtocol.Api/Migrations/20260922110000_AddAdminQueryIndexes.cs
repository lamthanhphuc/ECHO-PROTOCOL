using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260922110000_AddAdminQueryIndexes")]
public sealed class AddAdminQueryIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Admin_PaymentOrders_Created",
            table: "PaymentOrders",
            columns: new[] { "CreatedAtUtc", "PaymentOrderId" },
            descending: new[] { true, true });
        migrationBuilder.CreateIndex(
            name: "IX_Admin_PaymentOrders_Status_Created",
            table: "PaymentOrders",
            columns: new[] { "Status", "CreatedAtUtc", "PaymentOrderId" },
            descending: new[] { false, true, true });
        migrationBuilder.CreateIndex(
            name: "IX_Admin_PaymentOrders_Provider_Created",
            table: "PaymentOrders",
            columns: new[] { "Provider", "CreatedAtUtc", "PaymentOrderId" },
            descending: new[] { false, true, true });
        migrationBuilder.CreateIndex(
            name: "IX_Admin_PaymentOrders_Product_Created",
            table: "PaymentOrders",
            columns: new[] { "ProductReference", "CreatedAtUtc", "PaymentOrderId" },
            descending: new[] { false, true, true });

        migrationBuilder.CreateIndex(
            name: "IX_Admin_WalletTransactions_Created",
            table: "WalletTransactions",
            columns: new[] { "CreatedAtUtc", "Id" },
            descending: new[] { true, true });
        migrationBuilder.CreateIndex(
            name: "IX_Admin_WalletTransactions_Wallet_Created",
            table: "WalletTransactions",
            columns: new[] { "WalletId", "CreatedAtUtc", "Id" },
            descending: new[] { false, true, true });
        migrationBuilder.CreateIndex(
            name: "IX_Admin_WalletTransactions_Type_Created",
            table: "WalletTransactions",
            columns: new[] { "Type", "CreatedAtUtc", "Id" },
            descending: new[] { false, true, true });

        migrationBuilder.CreateIndex(
            name: "IX_Admin_Purchases_Created",
            table: "PurchaseTransactions",
            columns: new[] { "CreatedAtUtc", "PurchaseId" },
            descending: new[] { true, true });
        migrationBuilder.CreateIndex(
            name: "IX_Admin_Purchases_User_Created",
            table: "PurchaseTransactions",
            columns: new[] { "UserId", "CreatedAtUtc", "PurchaseId" },
            descending: new[] { false, true, true });
        migrationBuilder.CreateIndex(
            name: "IX_Admin_Purchases_Item_Created",
            table: "PurchaseTransactions",
            columns: new[] { "ShopItemId", "CreatedAtUtc", "PurchaseId" },
            descending: new[] { false, true, true });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Admin_PaymentOrders_Created", table: "PaymentOrders");
        migrationBuilder.DropIndex(name: "IX_Admin_PaymentOrders_Status_Created", table: "PaymentOrders");
        migrationBuilder.DropIndex(name: "IX_Admin_PaymentOrders_Provider_Created", table: "PaymentOrders");
        migrationBuilder.DropIndex(name: "IX_Admin_PaymentOrders_Product_Created", table: "PaymentOrders");
        migrationBuilder.DropIndex(name: "IX_Admin_WalletTransactions_Created", table: "WalletTransactions");
        migrationBuilder.DropIndex(name: "IX_Admin_WalletTransactions_Wallet_Created", table: "WalletTransactions");
        migrationBuilder.DropIndex(name: "IX_Admin_WalletTransactions_Type_Created", table: "WalletTransactions");
        migrationBuilder.DropIndex(name: "IX_Admin_Purchases_Created", table: "PurchaseTransactions");
        migrationBuilder.DropIndex(name: "IX_Admin_Purchases_User_Created", table: "PurchaseTransactions");
        migrationBuilder.DropIndex(name: "IX_Admin_Purchases_Item_Created", table: "PurchaseTransactions");
    }
}
