using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260922090000_AddPlayerLoadoutM4052")]
public sealed class AddPlayerLoadoutM4052 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "AK_InventoryItems_UserId_InventoryItemId",
            table: "InventoryItems",
            columns: new[] { "UserId", "InventoryItemId" });

        migrationBuilder.CreateTable(
            name: "PlayerLoadoutItems",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                SlotId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                EquippedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerLoadoutItems", item => new { item.UserId, item.SlotId });
                table.CheckConstraint(
                    "CK_PlayerLoadoutItems_Timestamps",
                    "\"UpdatedAtUtc\" >= \"EquippedAtUtc\"");
                table.ForeignKey(
                    name: "FK_PlayerLoadoutItems_InventoryItems_UserId_InventoryItemId",
                    columns: item => new { item.UserId, item.InventoryItemId },
                    principalTable: "InventoryItems",
                    principalColumns: new[] { "UserId", "InventoryItemId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PlayerLoadoutItems_Users_UserId",
                    column: item => item.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlayerLoadoutItems_UserId_InventoryItemId",
            table: "PlayerLoadoutItems",
            columns: new[] { "UserId", "InventoryItemId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PlayerLoadoutItems");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_InventoryItems_UserId_InventoryItemId",
            table: "InventoryItems");
    }
}
