using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileGameplayTuning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileTuningVersion",
                table: "ScenarioDecisions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileTuningVersion",
                table: "MatchProfileProcessingJobs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProfileTuningVersions",
                columns: table => new
                {
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileTuningVersions", x => x.Version);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTuningVersions_Revision",
                table: "ProfileTuningVersions",
                column: "Revision",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileTuningVersions");

            migrationBuilder.DropColumn(
                name: "ProfileTuningVersion",
                table: "ScenarioDecisions");

            migrationBuilder.DropColumn(
                name: "ProfileTuningVersion",
                table: "MatchProfileProcessingJobs");
        }
    }
}
