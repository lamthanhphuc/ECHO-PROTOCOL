using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEconomyParticipationAndCosmetics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentFulfillments_InventoryItemId",
                table: "PaymentFulfillments");

            migrationBuilder.AddColumn<DateTime>(
                name: "EntitlementValidatedAtUtc",
                table: "PaymentOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredLevelAtPurchase",
                table: "PaymentOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequiredOwnedItemId",
                table: "PaymentOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CosmeticRewardsJson",
                table: "MatchRewardGrants",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<long>(
                name: "GameplayDisconnectedMilliseconds",
                table: "MatchPlayerBindings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ProgressionCosmeticGrants",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Equipped = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressionCosmeticGrants", x => new { x.UserId, x.RewardKey });
                    table.ForeignKey(
                        name: "FK_ProgressionCosmeticGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentFulfillments_InventoryItemId",
                table: "PaymentFulfillments",
                column: "InventoryItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgressionCosmeticGrants");

            migrationBuilder.DropIndex(
                name: "IX_PaymentFulfillments_InventoryItemId",
                table: "PaymentFulfillments");

            migrationBuilder.DropColumn(
                name: "EntitlementValidatedAtUtc",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "RequiredLevelAtPurchase",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "RequiredOwnedItemId",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "CosmeticRewardsJson",
                table: "MatchRewardGrants");

            migrationBuilder.DropColumn(
                name: "GameplayDisconnectedMilliseconds",
                table: "MatchPlayerBindings");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentFulfillments_InventoryItemId",
                table: "PaymentFulfillments",
                column: "InventoryItemId",
                unique: true);
        }
    }
}
