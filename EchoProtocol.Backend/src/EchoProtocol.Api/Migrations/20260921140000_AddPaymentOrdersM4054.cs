using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260921140000_AddPaymentOrdersM4054")]
public sealed class AddPaymentOrdersM4054 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PaymentOrders",
            columns: table => new
            {
                PaymentOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ProviderOrderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ProductReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                FulfilledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ProviderTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                FulfillmentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentOrders", x => x.PaymentOrderId);
                table.CheckConstraint("CK_PaymentOrders_Amount_Positive", "\"Amount\" > 0");
                table.CheckConstraint("CK_PaymentOrders_Currency_Format", "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                table.CheckConstraint("CK_PaymentOrders_State_Data", "(\"Status\" <> 'PENDING_PAYMENT' OR \"ProviderOrderId\" IS NOT NULL) AND (\"Status\" NOT IN ('PAID', 'FULFILLED') OR (\"ProviderOrderId\" IS NOT NULL AND \"ProviderTransactionId\" IS NOT NULL AND \"PaidAtUtc\" IS NOT NULL)) AND (\"Status\" <> 'FULFILLED' OR (\"FulfillmentReference\" IS NOT NULL AND \"FulfilledAtUtc\" IS NOT NULL))");
                table.CheckConstraint("CK_PaymentOrders_Status_Allowed", "\"Status\" IN ('CREATED', 'PENDING_PAYMENT', 'PAID', 'FULFILLED', 'FAILED', 'CANCELLED', 'EXPIRED')");
                table.CheckConstraint("CK_PaymentOrders_Timestamps", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"ExpiresAtUtc\" IS NULL OR \"ExpiresAtUtc\" > \"CreatedAtUtc\") AND (\"PaidAtUtc\" IS NULL OR \"PaidAtUtc\" >= \"CreatedAtUtc\") AND (\"FulfilledAtUtc\" IS NULL OR (\"PaidAtUtc\" IS NOT NULL AND \"FulfilledAtUtc\" >= \"PaidAtUtc\"))");
                table.ForeignKey(
                    name: "FK_PaymentOrders_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentOrders_FulfillmentReference",
            table: "PaymentOrders",
            column: "FulfillmentReference",
            unique: true,
            filter: "\"FulfillmentReference\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentOrders_Provider_ProviderOrderId",
            table: "PaymentOrders",
            columns: new[] { "Provider", "ProviderOrderId" },
            unique: true,
            filter: "\"ProviderOrderId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentOrders_Provider_ProviderTransactionId",
            table: "PaymentOrders",
            columns: new[] { "Provider", "ProviderTransactionId" },
            unique: true,
            filter: "\"ProviderTransactionId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentOrders_UserId_CreatedAtUtc",
            table: "PaymentOrders",
            columns: new[] { "UserId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentOrders_UserId_IdempotencyKey",
            table: "PaymentOrders",
            columns: new[] { "UserId", "IdempotencyKey" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "PaymentOrders");
}
