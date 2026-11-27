using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSalvageServerCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalvageCheckpoints",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    StateJson = table.Column<string>(type: "character varying(262144)", maxLength: 262144, nullable: false),
                    ResultJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    SavedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Failed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalvageCheckpoints", x => x.MatchId);
                    table.ForeignKey(
                        name: "FK_SalvageCheckpoints_MatchAuthorityBindings_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchAuthorityBindings",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalvageCheckpoints_OwnerId_SavedAtUtc",
                table: "SalvageCheckpoints",
                columns: new[] { "OwnerId", "SavedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalvageCheckpoints");
        }
    }
}
