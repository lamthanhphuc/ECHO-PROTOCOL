using EchoProtocol.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoProtocol.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261019000000_AddLeaderboards")]
public sealed class AddLeaderboards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MapId",
            table: "MatchAuthorityBindings",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "Difficulty",
            table: "MatchAuthorityBindings",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RosterKey",
            table: "MatchResults",
            type: "character varying(131)",
            maxLength: 131,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_MatchAuthorityBindings_LeaderboardContext",
            table: "MatchAuthorityBindings",
            sql: "(\"MapId\" IS NULL AND \"Difficulty\" IS NULL) OR " +
                "(\"MapId\" IS NOT NULL AND \"Difficulty\" IS NOT NULL AND " +
                "\"MapId\" IN ('SciFi', 'Scene_A', 'Scene_B') AND " +
                "\"Difficulty\" IN ('EASY', 'NORMAL', 'HARD'))");

        migrationBuilder.CreateIndex(
            name: "IX_MatchAuthorityBindings_MapId_Difficulty",
            table: "MatchAuthorityBindings",
            columns: new[] { "MapId", "Difficulty" });
        migrationBuilder.CreateIndex(
            name: "IX_MatchResults_Outcome_PlayerCount_DurationSeconds_EndedAtUtc",
            table: "MatchResults",
            columns: new[] { "Outcome", "PlayerCount", "DurationSeconds", "EndedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_MatchResults_RosterKey",
            table: "MatchResults",
            column: "RosterKey");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MatchAuthorityBindings_MapId_Difficulty",
            table: "MatchAuthorityBindings");
        migrationBuilder.DropIndex(
            name: "IX_MatchResults_Outcome_PlayerCount_DurationSeconds_EndedAtUtc",
            table: "MatchResults");
        migrationBuilder.DropIndex(
            name: "IX_MatchResults_RosterKey",
            table: "MatchResults");
        migrationBuilder.DropCheckConstraint(
            name: "CK_MatchAuthorityBindings_LeaderboardContext",
            table: "MatchAuthorityBindings");
        migrationBuilder.DropColumn(name: "MapId", table: "MatchAuthorityBindings");
        migrationBuilder.DropColumn(name: "Difficulty", table: "MatchAuthorityBindings");
        migrationBuilder.DropColumn(name: "RosterKey", table: "MatchResults");
    }
}
