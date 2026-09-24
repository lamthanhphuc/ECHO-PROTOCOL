using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseInventoryM4014M4015 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletTransactions_Amount_NonNegative",
                table: "WalletTransactions");

            migrationBuilder.CreateTable(
                name: "PurchaseTransactions",
                columns: table => new
                {
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PriceAtPurchase = table.Column<int>(type: "integer", nullable: false),
                    WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseTransactions", x => x.PurchaseId);
                    table.CheckConstraint("CK_PurchaseTransactions_PriceAtPurchase_NonNegative", "\"PriceAtPurchase\" >= 0");
                    table.CheckConstraint("CK_PurchaseTransactions_Status_Completed", "\"Status\" = 'COMPLETED'");
                    table.ForeignKey(
                        name: "FK_PurchaseTransactions_ShopItems_ShopItemId",
                        column: x => x.ShopItemId,
                        principalTable: "ShopItems",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseTransactions_WalletTransactions_WalletTransactionId",
                        column: x => x.WalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcquiredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.InventoryItemId);
                    table.ForeignKey(
                        name: "FK_InventoryItems_PurchaseTransactions_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "PurchaseTransactions",
                        principalColumn: "PurchaseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_ShopItems_ShopItemId",
                        column: x => x.ShopItemId,
                        principalTable: "ShopItems",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletTransactions_Amount_ByType",
                table: "WalletTransactions",
                sql: "(\"Type\" = 'MATCH_REWARD' AND \"Amount\" >= 0) OR (\"Type\" = 'PURCHASE' AND \"Amount\" <= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_PurchaseId",
                table: "InventoryItems",
                column: "PurchaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ShopItemId",
                table: "InventoryItems",
                column: "ShopItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_UserId_ShopItemId",
                table: "InventoryItems",
                columns: new[] { "UserId", "ShopItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseTransactions_ShopItemId",
                table: "PurchaseTransactions",
                column: "ShopItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseTransactions_UserId_IdempotencyKey",
                table: "PurchaseTransactions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseTransactions_WalletTransactionId",
                table: "PurchaseTransactions",
                column: "WalletTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "PurchaseTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletTransactions_Amount_ByType",
                table: "WalletTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletTransactions_Amount_NonNegative",
                table: "WalletTransactions",
                sql: "\"Amount\" >= 0");
        }
    }
}
