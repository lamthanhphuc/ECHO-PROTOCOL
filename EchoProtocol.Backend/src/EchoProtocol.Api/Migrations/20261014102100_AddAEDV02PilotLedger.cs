using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAEDV02PilotLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AEDV02Decisions",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhaseOrdinal = table.Column<int>(type: "integer", nullable: false),
                    DecisionPoint = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TargetPhaseName = table.Column<string>(type: "text", nullable: false),
                    WindowSequence = table.Column<int>(type: "integer", nullable: false),
                    WindowId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlanRevision = table.Column<int>(type: "integer", nullable: false),
                    ExpectedPlanRevision = table.Column<int>(type: "integer", nullable: false),
                    PreviousPlanFingerprint = table.Column<string>(type: "text", nullable: false),
                    PlanFingerprint = table.Column<string>(type: "text", nullable: false),
                    RosterIdentity = table.Column<string>(type: "text", nullable: false),
                    SnapshotFingerprint = table.Column<string>(type: "text", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "text", nullable: false),
                    SceneManifestFingerprint = table.Column<string>(type: "text", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "text", nullable: false),
                    PolicyVersion = table.Column<string>(type: "text", nullable: false),
                    CatalogVersion = table.Column<string>(type: "text", nullable: false),
                    PlanValuesJson = table.Column<string>(type: "text", nullable: false),
                    InitialOptionalBudget = table.Column<int>(type: "integer", nullable: false),
                    ChangedKey = table.Column<string>(type: "text", nullable: false),
                    ReasonCode = table.Column<string>(type: "text", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "text", nullable: false),
                    ApplyStatus = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedTick = table.Column<long>(type: "bigint", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEDV02Decisions", x => x.DecisionId);
                    table.ForeignKey(
                        name: "FK_AEDV02Decisions_MatchAuthorityBindings_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchAuthorityBindings",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AEDV02SceneContents",
                columns: table => new
                {
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SceneVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ManifestJson = table.Column<string>(type: "text", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEDV02SceneContents", x => x.Fingerprint);
                });

            migrationBuilder.CreateTable(
                name: "AEDV02SkillObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dimension = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ComparisonContextKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "text", nullable: false),
                    Eligible = table.Column<int>(type: "integer", nullable: false),
                    ResolvedSuccess = table.Column<int>(type: "integer", nullable: false),
                    ResolvedFailure = table.Column<int>(type: "integer", nullable: false),
                    Censored = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEDV02SkillObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AEDV02SkillObservations_MatchAuthorityBindings_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchAuthorityBindings",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AEDV02SkillObservations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AEDV02VerifiedSources",
                columns: table => new
                {
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhaseOrdinal = table.Column<int>(type: "integer", nullable: false),
                    StartTick = table.Column<long>(type: "bigint", nullable: false),
                    EndTick = table.Column<long>(type: "bigint", nullable: false),
                    EndEventSequence = table.Column<long>(type: "bigint", nullable: false),
                    RosterIdentity = table.Column<string>(type: "text", nullable: false),
                    SceneManifestFingerprint = table.Column<string>(type: "text", nullable: false),
                    ReconstructedJson = table.Column<string>(type: "text", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceEndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEDV02VerifiedSources", x => x.Fingerprint);
                    table.ForeignKey(
                        name: "FK_AEDV02VerifiedSources_MatchAuthorityBindings_MatchId",
                        column: x => x.MatchId,
                        principalTable: "MatchAuthorityBindings",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AEDV02Decisions_MatchId_PhaseOrdinal_DecisionPoint_WindowSe~",
                table: "AEDV02Decisions",
                columns: new[] { "MatchId", "PhaseOrdinal", "DecisionPoint", "WindowSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AEDV02Decisions_MatchId_PlanRevision",
                table: "AEDV02Decisions",
                columns: new[] { "MatchId", "PlanRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AEDV02Decisions_ReceiptId",
                table: "AEDV02Decisions",
                column: "ReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AEDV02SkillObservations_MatchId_UserId_Dimension_Comparison~",
                table: "AEDV02SkillObservations",
                columns: new[] { "MatchId", "UserId", "Dimension", "ComparisonContextKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AEDV02SkillObservations_UserId",
                table: "AEDV02SkillObservations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AEDV02VerifiedSources_MatchId_PhaseOrdinal_EndTick",
                table: "AEDV02VerifiedSources",
                columns: new[] { "MatchId", "PhaseOrdinal", "EndTick" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AEDV02Decisions");

            migrationBuilder.DropTable(
                name: "AEDV02SceneContents");

            migrationBuilder.DropTable(
                name: "AEDV02SkillObservations");

            migrationBuilder.DropTable(
                name: "AEDV02VerifiedSources");
        }
    }
}
