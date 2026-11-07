using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptiveAdminConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreMatchRule",
                table: "ScenarioDecisions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdaptiveConfigurationVersions",
                columns: table => new
                {
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Compatibility = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdaptiveConfigurationVersions", x => x.Version);
                });

            migrationBuilder.CreateTable(
                name: "AEDV02ClientObservations",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEDV02ClientObservations", x => new { x.DecisionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AEDV02ClientObservations_AEDV02Decisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "AEDV02Decisions",
                        principalColumn: "DecisionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveConfigurationVersions_Compatibility_Revision",
                table: "AdaptiveConfigurationVersions",
                columns: new[] { "Compatibility", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdaptiveConfigurationVersions");

            migrationBuilder.DropTable(
                name: "AEDV02ClientObservations");

            migrationBuilder.DropColumn(
                name: "PreMatchRule",
                table: "ScenarioDecisions");
        }
    }
}
