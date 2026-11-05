using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAEDPhase8DirectorApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AEDDirectorDecisionsV1",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedPhaseOrdinal = table.Column<int>(type: "integer", nullable: false),
                    DirectorKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RosterIdentity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ApprovalScope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpectedRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AbortedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEDDirectorDecisionsV1", x => x.DecisionId);
                });

            migrationBuilder.CreateTable(
                name: "AEDPhase8VerifiedSourcesV1",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedPhaseOrdinal = table.Column<int>(type: "integer", nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RosterIdentity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanonicalVerified = table.Column<bool>(type: "boolean", nullable: false),
                    HistoricalSkillVerified = table.Column<bool>(type: "boolean", nullable: false),
                    PressureVerified = table.Column<bool>(type: "boolean", nullable: false),
                    SupplyLedgerVerified = table.Column<bool>(type: "boolean", nullable: false),
                    MinionPopulationVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VerifiedContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AEDPhase8VerifiedSourcesV1", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AEDDirectorDecisionsV1_MatchId_CompletedPhaseOrdinal_Direct~",
                table: "AEDDirectorDecisionsV1",
                columns: new[] { "MatchId", "CompletedPhaseOrdinal", "DirectorKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AEDDirectorDecisionsV1_MatchId_DirectorKind_Status",
                table: "AEDDirectorDecisionsV1",
                columns: new[] { "MatchId", "DirectorKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AEDPhase8VerifiedSourcesV1_MatchId_CompletedPhaseOrdinal_So~",
                table: "AEDPhase8VerifiedSourcesV1",
                columns: new[] { "MatchId", "CompletedPhaseOrdinal", "SourceVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AEDDirectorDecisionsV1");

            migrationBuilder.DropTable(
                name: "AEDPhase8VerifiedSourcesV1");
        }
    }
}
