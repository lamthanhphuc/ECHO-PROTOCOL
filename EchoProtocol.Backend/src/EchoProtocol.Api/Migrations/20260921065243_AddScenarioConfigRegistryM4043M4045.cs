using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioConfigRegistryM4043M4045 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScenarioConfigs",
                columns: table => new
                {
                    ScenarioConfigId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConfigSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MapId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MonsterType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ObjectiveSpawnSetId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SupportItemBudget = table.Column<int>(type: "integer", nullable: false),
                    DetectionFillRate = table.Column<double>(type: "double precision", nullable: false),
                    DetectionDecayRate = table.Column<double>(type: "double precision", nullable: false),
                    ChaseSpeed = table.Column<double>(type: "double precision", nullable: false),
                    SearchDuration = table.Column<double>(type: "double precision", nullable: false),
                    RouteModifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EscapeDoorTimerSeconds = table.Column<double>(type: "double precision", nullable: false),
                    FallbackConfigId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FallbackConfigVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ContentWhitelistVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UnityCompatibilityVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsProductionApproved = table.Column<bool>(type: "boolean", nullable: false),
                    IsFixedFallback = table.Column<bool>(type: "boolean", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioConfigs", x => new { x.ScenarioConfigId, x.ScenarioConfigVersion });
                    table.CheckConstraint("CK_ScenarioConfigs_EscapeTimer_Range", "\"EscapeDoorTimerSeconds\" >= 45 AND \"EscapeDoorTimerSeconds\" <= 60");
                    table.CheckConstraint("CK_ScenarioConfigs_Fallback_IsFixed", "NOT \"IsFixedFallback\" OR \"ConfigSource\" = 'Fixed'");
                    table.CheckConstraint("CK_ScenarioConfigs_Numerics_NonNegative", "\"DetectionFillRate\" >= 0 AND \"DetectionDecayRate\" >= 0 AND \"ChaseSpeed\" >= 0 AND \"SearchDuration\" >= 0");
                    table.CheckConstraint("CK_ScenarioConfigs_Source_Allowed", "\"ConfigSource\" IN ('Fixed', 'Adaptive')");
                    table.CheckConstraint("CK_ScenarioConfigs_SupportBudget_NonNegative", "\"SupportItemBudget\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ScenarioContentDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContentWhitelistVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UnityCompatibilityVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsProductionApproved = table.Column<bool>(type: "boolean", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioContentDefinitions", x => x.Id);
                    table.CheckConstraint("CK_ScenarioContentDefinitions_Type_Allowed", "\"ContentType\" IN ('Map', 'Monster', 'ObjectiveSpawnSet', 'RouteModifier')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioConfigs_ScenarioConfigVersion",
                table: "ScenarioConfigs",
                column: "ScenarioConfigVersion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ScenarioConfigs_ProductionFallbackCompatibility",
                table: "ScenarioConfigs",
                column: "UnityCompatibilityVersion",
                unique: true,
                filter: "\"IsActive\" AND \"IsProductionApproved\" AND \"IsFixedFallback\"");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioContentDefinitions_IsActive_IsProductionApproved_Un~",
                table: "ScenarioContentDefinitions",
                columns: new[] { "IsActive", "IsProductionApproved", "UnityCompatibilityVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_ScenarioContent_IdentityVersion",
                table: "ScenarioContentDefinitions",
                columns: new[] { "ContentType", "ContentId", "ContentWhitelistVersion", "UnityCompatibilityVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScenarioConfigs");

            migrationBuilder.DropTable(
                name: "ScenarioContentDefinitions");
        }
    }
}
