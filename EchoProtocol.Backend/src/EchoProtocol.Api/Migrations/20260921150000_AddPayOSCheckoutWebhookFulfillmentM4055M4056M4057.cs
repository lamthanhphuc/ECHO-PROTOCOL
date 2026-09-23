using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EchoProtocol.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260921150000_AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057")]
public sealed class AddPayOSCheckoutWebhookFulfillmentM4055M4056M4057 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_WalletTransactions_Amount_ByType",
            table: "WalletTransactions");
        migrationBuilder.AddCheckConstraint(
            name: "CK_WalletTransactions_Amount_ByType",
            table: "WalletTransactions",
            sql: "(\"Type\" IN ('MATCH_REWARD', 'PAYMENT_FULFILLMENT') AND \"Amount\" >= 0) OR (\"Type\" = 'PURCHASE' AND \"Amount\" <= 0)");

        migrationBuilder.CreateTable(
            name: "PaymentCheckouts",
            columns: table => new
            {
                CheckoutSequenceId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                PaymentOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ProviderOrderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProviderPaymentLinkId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CheckoutUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ReservedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReadyAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentCheckouts", x => x.CheckoutSequenceId);
                table.CheckConstraint("CK_PaymentCheckouts_Ready_Data", "\"Status\" <> 'READY' OR (\"ProviderPaymentLinkId\" IS NOT NULL AND \"CheckoutUrl\" IS NOT NULL AND \"ReadyAtUtc\" IS NOT NULL)");
                table.CheckConstraint("CK_PaymentCheckouts_Status_Allowed", "\"Status\" IN ('RESERVED', 'READY')");
                table.ForeignKey(
                    name: "FK_PaymentCheckouts_PaymentOrders_PaymentOrderId",
                    column: x => x.PaymentOrderId,
                    principalTable: "PaymentOrders",
                    principalColumn: "PaymentOrderId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PaymentProviderEvents",
            columns: table => new
            {
                PaymentProviderEventId = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ProviderEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PaymentOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                ProviderOrderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                NormalizedStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                SemanticFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                VerificationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ProcessingOutcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentProviderEvents", x => x.PaymentProviderEventId);
                table.CheckConstraint("CK_PaymentProviderEvents_Amount_Positive", "\"Amount\" > 0");
                table.CheckConstraint("CK_PaymentProviderEvents_Verified", "\"VerificationStatus\" = 'VERIFIED'");
                table.ForeignKey(
                    name: "FK_PaymentProviderEvents_PaymentOrders_PaymentOrderId",
                    column: x => x.PaymentOrderId,
                    principalTable: "PaymentOrders",
                    principalColumn: "PaymentOrderId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PaymentFulfillments",
            columns: table => new
            {
                PaymentOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                FulfillmentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentFulfillments", x => x.PaymentOrderId);
                table.CheckConstraint("CK_PaymentFulfillments_Target", "(\"Kind\" = 'WALLET_CREDIT' AND \"WalletTransactionId\" IS NOT NULL AND \"InventoryItemId\" IS NULL) OR (\"Kind\" = 'INVENTORY_ITEM' AND \"WalletTransactionId\" IS NULL AND \"InventoryItemId\" IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_PaymentFulfillments_InventoryItems_InventoryItemId",
                    column: x => x.InventoryItemId,
                    principalTable: "InventoryItems",
                    principalColumn: "InventoryItemId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentFulfillments_PaymentOrders_PaymentOrderId",
                    column: x => x.PaymentOrderId,
                    principalTable: "PaymentOrders",
                    principalColumn: "PaymentOrderId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentFulfillments_WalletTransactions_WalletTransactionId",
                    column: x => x.WalletTransactionId,
                    principalTable: "WalletTransactions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentCheckouts_PaymentOrderId",
            table: "PaymentCheckouts",
            column: "PaymentOrderId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PaymentCheckouts_Provider_ProviderOrderId",
            table: "PaymentCheckouts",
            columns: new[] { "Provider", "ProviderOrderId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PaymentProviderEvents_PaymentOrderId",
            table: "PaymentProviderEvents",
            column: "PaymentOrderId");
        migrationBuilder.CreateIndex(
            name: "IX_PaymentProviderEvents_Provider_ProviderEventId",
            table: "PaymentProviderEvents",
            columns: new[] { "Provider", "ProviderEventId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PaymentFulfillments_FulfillmentReference",
            table: "PaymentFulfillments",
            column: "FulfillmentReference",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PaymentFulfillments_InventoryItemId",
            table: "PaymentFulfillments",
            column: "InventoryItemId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PaymentFulfillments_WalletTransactionId",
            table: "PaymentFulfillments",
            column: "WalletTransactionId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PaymentCheckouts");
        migrationBuilder.DropTable(name: "PaymentProviderEvents");
        migrationBuilder.DropTable(name: "PaymentFulfillments");
        migrationBuilder.DropCheckConstraint(
            name: "CK_WalletTransactions_Amount_ByType",
            table: "WalletTransactions");
        migrationBuilder.AddCheckConstraint(
            name: "CK_WalletTransactions_Amount_ByType",
            table: "WalletTransactions",
            sql: "(\"Type\" = 'MATCH_REWARD' AND \"Amount\" >= 0) OR (\"Type\" = 'PURCHASE' AND \"Amount\" <= 0)");
    }
}
