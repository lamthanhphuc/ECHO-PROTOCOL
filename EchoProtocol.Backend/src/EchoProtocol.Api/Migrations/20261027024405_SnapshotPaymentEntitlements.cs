using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotPaymentEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FulfillmentKind",
                table: "PaymentOrders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FulfillmentShopItemId",
                table: "PaymentOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WalletCreditAmount",
                table: "PaymentOrders",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfillmentKind",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "FulfillmentShopItemId",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "WalletCreditAmount",
                table: "PaymentOrders");
        }
    }
}
